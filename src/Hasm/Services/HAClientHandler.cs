using System.Collections.Concurrent;
using Hasm.Models;
using HassClient.Models;
using HassClient.WS;
using Wolverine;

namespace Hasm.Services;

public class HAClientHandler(Client _client, MessageBusService _messageBusService) : IClientHandler
{
    public class ClientProperties
    {
        public string? Host { get; set; }
        public string? Token { get; set; }
    }

    private sealed class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue? VariableValue { get; set; }
    }

    private ClientProperties _clientProperties = new();
    private HassWSApi? _wsApi;
    private bool _started = false;
    private Timer? _reconnectTimer = null;
    private readonly ConcurrentDictionary<string, List<VariableInfo>> _variables = [];

    public string? Host => _clientProperties.Host;
    public string? Token => _clientProperties.Token;
    public ConnectionStates ConnectionState => _wsApi?.ConnectionState ?? ConnectionStates.Disconnected;
    public Client Client => _client;

    public Task<bool> SetVariableValueAsync(int variableId, string value)
    {        
        return Task.FromResult(false);
    }

    public Task AddOrUpdateVariableAsync(Variable variable)
    {
        throw new NotImplementedException();
    }

    public Task DeleteVariableAsync(Variable variable)
    {
        throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await DisposeHassApiAsync();
        }
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            if (!string.IsNullOrWhiteSpace(_client.Data))
            {
                _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(_client.Data) ?? new();
            }
            await CreateHassWSApiAsync();
        }
    }

    public async Task UpdateAsync(Client client)
    {
        _started = false;
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
            _started = true;
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
}
