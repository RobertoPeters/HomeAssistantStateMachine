using HassClient.Models;
using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using System.Collections.Concurrent;
using System.Linq;

namespace HomeAssistantStateMachine.Services;

public class HAClientHandler : IAsyncDisposable
{
    private sealed class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue? VariableValue { get; set; }
    }

    public HAClientService HAClientService { get; private set; }
    public HAClient HAClient { get; private set; }
    public VariableService VariableService { get; private set; }

    private HassWSApi? _wsApi;
    private bool _started = false;
    private Timer? _reconnectTimer = null;
    private readonly ConcurrentDictionary<string, List<VariableInfo>> _variables = [];
    private readonly ConcurrentDictionary<string, Dictionary<int, Action<object>>> _variablesChangeCallBack = [];

    public HAClientHandler(HAClientService haClientService, HAClient haClient, VariableService variableService)
    {
        HAClientService = haClientService;
        HAClient = haClient;
        VariableService = variableService;
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            var variables = VariableService.GetScopedVariables(null, HAClient, null, null);
            foreach (var variable in variables)
            {
                if (!string.IsNullOrWhiteSpace(variable.variable.Data))
                {
                    if (!_variables.TryGetValue(variable.variable.Data, out var variableInfos))
                    {
                        variableInfos = [];
                        _variables.TryAdd(variable.variable.Data, variableInfos);
                    }
                    var v = new VariableInfo()
                    {
                        Variable = variable.variable,
                        VariableValue = variable.variableValue
                    };
                    variableInfos.Add(v);
                }
            }
            await CreateHassWSApiAsync();
        }
    }

    private async Task CreateHassWSApiAsync()
    {
        _wsApi = new HassWSApi();
        _wsApi.ConnectionStateChanged += _wsApi_ConnectionStateChanged;
        if (HAClient.Enabled && !string.IsNullOrWhiteSpace(HAClient.Token) && !string.IsNullOrWhiteSpace(HAClient.Host))
        {
            await ConnectAsync();
            foreach (var variable in _variables)
            {
                _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(variable.Key, EventHandlerEventStateChanged);
            }
            _started = true;
        }
    }

    public async Task DisposeHassApiAsync()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
        if (_wsApi != null)
        {
            _wsApi.ConnectionStateChanged -= _wsApi_ConnectionStateChanged;
            try
            {
                await _wsApi.CloseAsync();
            }
            catch
            {
                //nothing
            }
            _wsApi = null;
        }
    }

    public async Task UpdateHAClientAsync(HAClient haCliennt, HasmDbContext? ctx = null)
    {
        _started = false;
        await DisposeHassApiAsync();
        HAClient = haCliennt;
        await CreateHassWSApiAsync();
    }

    public async Task DeleteVariableAsync(string name, HasmDbContext? ctx = null)
    {
        var existingVariable = VariableService.GetVariable(name);
        if (existingVariable != null)
        {
            var haEntityId = existingVariable.Data ?? existingVariable.Name;
            if (_variables.TryGetValue(haEntityId, out var variableInfos))
            {
                var varibleInfo = variableInfos.Find(v => v.Variable.Id == existingVariable.Id);
                if (varibleInfo != null)
                {
                    variableInfos.Remove(varibleInfo);
                    if (!variableInfos.Any())
                    {
                        _variables.TryRemove(haEntityId, out _);
                        if (_wsApi != null)
                        {
                            try
                            {
                                _wsApi.StateChagedEventListener.UnsubscribeEntityStatusChanged(haEntityId, EventHandlerEventStateChanged);
                            }
                            catch
                            {
                                //nothing
                            }
                        }
                    }
                    await VariableService.DeleteVariableAsync(varibleInfo.Variable.Name, ctx);
                }
            }
        }
    }

    public bool SetStateChangedCallback(int registrarId, Variable variable, Action<object> callback)
    {
        var result = false;
        if (variable != null)
        {
            var haEntityId = variable.Data ?? variable.Name;
            if (!_variablesChangeCallBack.TryGetValue(haEntityId, out var callbacks))
            {
                callbacks = [];
                _variablesChangeCallBack.TryAdd(haEntityId, callbacks);
            }
            if (callbacks.TryGetValue(registrarId, out _))
            {
                callbacks.Remove(registrarId);
            }
            callbacks.TryAdd(registrarId, callback);
            return true;
        }
        return result;
    }

    public void RemoveRegistrarFromStateChangedCallback(int registrarId)
    {
        foreach (var item in from item in _variablesChangeCallBack.Values.ToList()
                             where item.ContainsKey(registrarId)
                             select item)
        {
            item.Remove(registrarId);
        }
    }

    public async Task<Variable?> CreateVariableAsync(string name, string? data, HasmDbContext? ctx = null)
    {
        Variable? result = null;
        var haEntityId = data ?? name;

        var existingVariable = VariableService.GetVariable(name);
        if (existingVariable != null && existingVariable.Data == data && existingVariable.HAClientId == HAClient.Id)
        {
            return existingVariable;
        }

        //todo: check data has changed!

        result = await VariableService.CreateVariableAsync(name, data, HAClient, null, null, null, ctx);
        if (result != null && !string.IsNullOrWhiteSpace(result.Data))
        {
            VariableValue? vv = null;
            if (!_variables.TryGetValue(haEntityId, out var variableInfos))
            {
                variableInfos = [];
                _variables.TryAdd(haEntityId, variableInfos);
            }
            else
            {
                vv = variableInfos.Find(v => v.VariableValue != null)?.VariableValue;
            }
            var v = new VariableInfo()
            {
                Variable = result,
                VariableValue = null
            };
            variableInfos.Add(v);

            if (vv != null && vv.Value != null)
            {
                await UpdateVariableValue(haEntityId, vv.Value);
            }
            else if (vv == null && ConnectionState == ConnectionStates.Connected)
            {
                _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(haEntityId, EventHandlerEventStateChanged);
                Task.Factory.StartNew(async () =>
                {
                    var states = await _wsApi!.GetStatesAsync();
                    var state = states.FirstOrDefault(s => s.EntityId == haEntityId);
                    if (state != null)
                    {
                        await UpdateVariableValue(state.EntityId, state.State);
                    }
                });
            }
        }
        return result;
    }

    public async Task<bool> CallServiceAsync(string name, string service, object? data = null)
    {
        var result = false;
        if (ConnectionState == ConnectionStates.Connected)
        {
            try
            {
                var callResult = await _wsApi!.CallServiceAsync(name, service, data, new CancellationTokenSource(1000).Token);
                result = callResult != null;
            }
            catch
            {//ignore
            }
        }
        return result;
    }

    public async Task<bool> CallServiceForEntitiesAsync(string name, string service, params string[] entityIds)
    {
        var result = false;
        if (ConnectionState == ConnectionStates.Connected)
        {
            try
            {
                result = await _wsApi!.CallServiceForEntitiesAsync(name, service, new CancellationTokenSource(1000).Token, entityIds);
            }
            catch
            {//ignore
            }
        }
        return result;
    }

    public ConnectionStates ConnectionState => _wsApi?.ConnectionState ?? ConnectionStates.Disconnected;

    private async void _wsApi_ConnectionStateChanged(object? sender, ConnectionStates e)
    {
        if (e == ConnectionStates.Connected && _variables.Any())
        {
            var states = await _wsApi!.GetStatesAsync();
            foreach (var state in states)
            {
                await UpdateVariableValue(state.EntityId, state.State);
            }
        }
        else if (e == ConnectionStates.Disconnected)
        {
            //ok, we need to try again in a few seconds
            _reconnectTimer?.Dispose();
            _reconnectTimer = new Timer(async (state) =>
            {
                if (_wsApi != null)
                {
                    try
                    {
                        var connectionParameters = ConnectionParameters.CreateFromInstanceBaseUrl(HAClient.Host, HAClient.Token);
                        await _wsApi.ConnectAsync(connectionParameters);
                    }
                    catch
                    {
                        //ignore
                    }
                }
            }, null, 5000, Timeout.Infinite);
            
        }
        HAClientService.ClientHandlerConnectionStateChanged(this, e);
    }

    private async Task ConnectAsync()
    {
        if (_wsApi == null) return;
        try
        {
            var connectionParameters = ConnectionParameters.CreateFromInstanceBaseUrl(HAClient.Host, HAClient.Token);
            await _wsApi.ConnectAsync(connectionParameters);
        }
        catch
        {
            //todo
        }
    }

    private async void EventHandlerEventStateChanged(object? sender, StateChangedEvent stateChangedArgs)
    {
        await UpdateVariableValue(stateChangedArgs.EntityId, stateChangedArgs.NewState.State);
        if (_variablesChangeCallBack.TryGetValue(stateChangedArgs.EntityId, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    callback.Value(stateChangedArgs);
                }
                catch
                {
                    //ignore
                }
            }
        }
    }

    private async Task UpdateVariableValue(string eventId, string state)
    {
        if (_variables.TryGetValue(eventId, out var variableInfos))
        {
            foreach (var variableInfo in variableInfos)
            {
                if (variableInfo.VariableValue == null)
                {
                    variableInfo.VariableValue = new VariableValue()
                    {
                        Variable = variableInfo.Variable,
                        Value = state
                    };
                    await VariableService.CreateVariableValueAsync(variableInfo.VariableValue);
                }
                else
                {
                    await VariableService.UpdateVariableValueAsync(variableInfo.VariableValue, state);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await DisposeHassApiAsync();
        }
    }
}
