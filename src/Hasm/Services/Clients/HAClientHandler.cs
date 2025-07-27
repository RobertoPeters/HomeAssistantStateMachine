using System.Collections.Concurrent;
using Hasm.Models;
using Hasm.Services.Interfaces;
using HassClient.Models;
using HassClient.WS;
using Wolverine;

namespace Hasm.Services.Clients;

public class HAClientHandler(Client _client, VariableService _variableService, MessageBusService _messageBusService) : IClientHandler, IClientConnected
{
    public class ClientProperties
    {
        public string? Host { get; set; }
        public string? Token { get; set; }
    }

    private ClientProperties _clientProperties = new();
    private HassWSApi? _wsApi;
    private Timer? _reconnectTimer = null;
    private readonly ConcurrentDictionary<string, List<VariableService.VariableInfo>> _variables = [];

    public bool IsConnected => _wsApi?.ConnectionState == ConnectionStates.Connected;
    public Client Client => _client;

    public async ValueTask DisposeAsync()
    {
        await DisposeHassApiAsync();
    }

    public async Task StartAsync()
    {
        await DisposeHassApiAsync();
        _variables.Clear();
        var variables = _variableService.GetVariables()
          .Where(x => x.Variable.ClientId == _client.Id);
        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Variable.Data))
            {
                continue;
            }

            if (!_variables.TryGetValue(variable.Variable.Data, out var list))
            {
                list = new List<VariableService.VariableInfo>();
                _variables.TryAdd(variable.Variable.Data, list);
            }
            list.Add(variable);
        }

        if (!string.IsNullOrWhiteSpace(_client.Data))
        {
            _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(_client.Data) ?? new();
        }
        await CreateHassWSApiAsync();
    }

    public async Task UpdateAsync(Client client)
    {
        await DisposeHassApiAsync();
        _client = client;
        if (!string.IsNullOrWhiteSpace(client.Data))
        {
            _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(client.Data) ?? new();
        }
        else
        {
            _clientProperties = new();
        }
        await CreateHassWSApiAsync();
    }

    public async Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        var existingVariable = variableId == null ? null : _variables.Values.SelectMany(x => x)
             .Where(x => x.Variable.Id == variableId)
             .Select(x => x)
             .FirstOrDefault();

        if (variableId != null && existingVariable == null)
        {
            return false;
        }

        var result = false;
        switch (command.ToLower())
        {
            case "callservice":
                if (parameter1 != null && parameter2 != null)
                {
                    result = await CallServiceAsync(parameter1.ToString()!, parameter2.ToString()!, parameter3);
                }
                break;
            case "callserviceforentities":
                if (parameter1 != null && parameter2 != null && parameter3 != null)
                {
                    result = await CallServiceForEntitiesAsync(parameter1.ToString()!, parameter2.ToString()!, ((System.Collections.IEnumerable)parameter3).Cast<object>()
                                 .Select(x => x.ToString()!)
                                 .ToArray());
                }
                break;
        }
        return result;
    }

    private async Task<bool> CallServiceAsync(string name, string service, object? data = null)
    {
        var result = false;
        if (IsConnected)
        {
            try
            {
                using var ct = new CancellationTokenSource(1000);
                var callResult = await _wsApi!.CallServiceAsync(name, service, data, ct.Token);
                result = callResult != null;
            }
            catch
            {//ignore
            }
        }
        return result;
    }

    private async Task<bool> CallServiceForEntitiesAsync(string name, string service, params string[] entityIds)
    {
        var result = false;
        if (IsConnected)
        {
            try
            {
                using var ct = new CancellationTokenSource(1000);
                result = await _wsApi!.CallServiceForEntitiesAsync(name, service, ct.Token, entityIds);
            }
            catch
            {//ignore
            }
        }
        return result;
    }

    private async Task DisposeHassApiAsync()
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

    private async Task CreateHassWSApiAsync()
    {
        _wsApi = new HassWSApi();
        _wsApi.ConnectionStateChanged += _wsApi_ConnectionStateChanged;
        if (_client.Enabled && !string.IsNullOrWhiteSpace(_clientProperties.Token) && !string.IsNullOrWhiteSpace(_clientProperties.Host))
        {
            await ConnectAsync();
            foreach (var variable in _variables)
            {
                try
                {
                    _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(variable.Key, EventHandlerEventStateChanged);
                }
                catch
                {
                    //none
                }
            }
        }
    }

    private async void _wsApi_ConnectionStateChanged(object? sender, ConnectionStates e)
    {
        await _messageBusService.PublishAsync(new ClientConnectionInfo()
        {
            ClientId = _client.Id,
            IsConnected = e == ConnectionStates.Connected
        });

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
                        var connectionParameters = ConnectionParameters.CreateFromInstanceBaseUrl(_clientProperties.Host, _clientProperties.Token);
                        await _wsApi.ConnectAsync(connectionParameters);
                    }
                    catch
                    {
                        //ignore
                    }
                }
            }, null, 5000, Timeout.Infinite);

        }
    }

    private async Task ConnectAsync()
    {
        if (_wsApi == null) return;
        try
        {
            var connectionParameters = ConnectionParameters.CreateFromInstanceBaseUrl(_clientProperties.Host, _clientProperties.Token);
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

    private async Task UpdateVariableValue(string eventId, string? state)
    {
        if (_variables.TryGetValue(eventId, out var variableList))
        {
            if (variableList.Any())
            {
                await _variableService.SetVariableValuesAsync(variableList.Select(x => (variableId: x.Variable.Id, value: state)).ToList());
            }
        }
    }

    public async Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        foreach (var variable in variables)
        {
            await DeleteVariableInfoAsync(variable, variable.Variable.Data);
        }
    }

    public Task DeleteVariableInfoAsync(VariableService.VariableInfo variable, string? entityId)
    {
        if (!string.IsNullOrWhiteSpace(entityId) && _variables.TryGetValue(entityId, out var variableList))
        {
            var variableInList = variableList.FirstOrDefault(x => x.Variable.Id == Math.Abs(variable.Variable.Id));
            if (variableInList != null)
            {
                variableList.Remove(variableInList);
                if (!variableList.Any())
                {
                    _variables.TryRemove(entityId, out _);
                    if (_wsApi != null)
                    {
                        try
                        {
                            _wsApi.StateChagedEventListener.UnsubscribeEntityStatusChanged(entityId, EventHandlerEventStateChanged);
                        }
                        catch
                        {
                            //nothing
                        }
                    }
                }
            }
        }
        return Task.CompletedTask;
    }

    public async Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        foreach (var variable in variables)
        {
            var existingVariable = _variables.Values.SelectMany(x => x)
                .Where(x => x.Variable.Id == variable.Variable.Id)
                .Select(x => x)
                .FirstOrDefault();

            if (existingVariable != null)
            {
                await UpdateVariableInfoAsync(variable);
            }
            else if (!string.IsNullOrWhiteSpace(variable.Variable.Data))
            {
                await AddVariableInfoAsync(variable);
            }
        }
    }

    private async Task AddVariableInfoAsync(VariableService.VariableInfo variable)
    {
        if (!_variables.TryGetValue(variable.Variable.Data!, out var variableList))
        {
            variableList = [];
            _variables.TryAdd(variable.Variable.Data!, variableList);
        }
        variableList.Add(variable);
        if (variableList.Count == 1)
        {
            if (_wsApi != null)
            {
                try
                {
                    _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(variable.Variable.Data, EventHandlerEventStateChanged);
                    var states = await _wsApi!.GetStatesAsync();
                    var state = states.FirstOrDefault(x => x.EntityId == variable.Variable.Data);
                    if (state != null)
                    {
                        await UpdateVariableValue(state.EntityId, state.State);
                    }
                    else
                    {
                        await UpdateVariableValue(variable.Variable.Data!, null);
                    }
                }
                catch
                {
                    //nothing
                }
            }
        }
    }

    private async Task UpdateVariableInfoAsync(VariableService.VariableInfo variable)
    {
        if (variable.Variable.PreviousData != variable.Variable.Data)
        {
            await DeleteVariableInfoAsync(variable, variable.Variable.PreviousData);
            if (!string.IsNullOrWhiteSpace(variable.Variable.Data))
            {
                await AddVariableInfoAsync(variable);
            }
        }
    }
}
