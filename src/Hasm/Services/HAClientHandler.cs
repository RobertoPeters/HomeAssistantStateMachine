using System.Collections.Concurrent;
using Hasm.Models;
using HassClient.Models;
using HassClient.WS;
using Wolverine;

namespace Hasm.Services;

public class HAClientHandler(Client _client, VariableService _variableService, MessageBusService _messageBusService) : IClientHandler
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

    public ConnectionStates ConnectionState => _wsApi?.ConnectionState ?? ConnectionStates.Disconnected;
    public Client Client => _client;

    public Task<bool> SetVariableValueAsync(int variableId, string value)
    {
        return Task.FromResult(false);
    }


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
        foreach(var variable in variables)
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

    public Task<bool> ExecuteAsync(int? variableId, string command, string? parameter)
    {
        return Task.FromResult(false);
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
        await _messageBusService.PublishAsync(this);

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

    private Task UpdateVariableValue(string eventId, string state)
    {
        return Task.CompletedTask;
    }

    public Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        return Task.CompletedTask;
    }

    public Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        return Task.CompletedTask;
    }
}
