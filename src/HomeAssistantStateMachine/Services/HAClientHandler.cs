using HassClient.Models;
using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.Extensions.Logging;
using Mono.TextTemplating;
using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<string, VariableInfo> _variables = [];

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
            var variables = VariableService.GetScopedVariables(HAClient, null, null);
            foreach (var variable in variables)
            {
                var v = new VariableInfo()
                {
                    Variable = variable.variable,
                    VariableValue = variable.variableValue
                };
                _variables.TryAdd(variable.variable.Name, v);
            }
            _wsApi = new HassWSApi();
            _wsApi.ConnectionStateChanged += _wsApi_ConnectionStateChanged;
            if (HAClient.Enabled && !string.IsNullOrWhiteSpace(HAClient.Token) && !string.IsNullOrWhiteSpace(HAClient.Host))
            {
                await ConnectAsync();
                foreach (var variable in _variables.Values.ToList())
                {
                    _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(variable.Variable.Name, EventHandlerEventStateChanged);
                }
                _started = true;
            }
        }
    }

    public async Task<Variable?> CreateVariableAsync(string name, string? data, HasmDbContext? ctx = null)
    {
        Variable? result = null;
        if (!_variables.TryGetValue(name, out var _))
        {
            result = await VariableService.CreateVariableAsync(Guid.NewGuid(), name, data, HAClient, null, null, ctx);
            if (result != null)
            {
                var v = new VariableInfo();
                v.Variable = result;
                _variables.TryAdd(name, v);

                if (ConnectionState == ConnectionStates.Connected)
                {
                    _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(result.Name, EventHandlerEventStateChanged);
                    var states = await _wsApi!.GetStatesAsync();
                    var state = states.FirstOrDefault(s => s.EntityId == result.Name);
                    if (state != null)
                    {
                        await UpdateVariableValue(state.EntityId, state.State);
                    }
                }
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
    }

    private async Task UpdateVariableValue(string eventId, string state)
    {
        if (_variables.TryGetValue(eventId, out var variable))
        {
            if (variable.VariableValue == null)
            {
                variable.VariableValue = new VariableValue()
                {
                    Handle = Guid.NewGuid(),
                    Variable = variable.Variable,
                    Value = state
                };
                await VariableService.CreateVariableValueAsync(variable.VariableValue);
            }
            else
            {
                await VariableService.UpdateVariableValueAsync(variable.VariableValue, state);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started && _wsApi != null)
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
        }
    }
}
