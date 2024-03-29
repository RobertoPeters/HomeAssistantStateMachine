using HassClient.WS;
using HassClient.WS.Messages;
using HomeAssistantStateMachine.Models;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class HAClientHandler : IAsyncDisposable
{
    private class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue? VariableValue { get; set; }
    }

    private sealed class EventResultInfoStateChanged
    {
        public string? Entity_id { get; set; }
        public EventResultInfoState? New_state { get; set; }
     }

    private sealed class EventResultInfoState
    {
        public string? Entity_id { get; set; }
        public string? State { get; set; }
    }

    public HAClientService HAClientService { get; private set; }
    public HAClient HAClient { get; private set; }
    public VariableService VariableService { get; private set; }

    private HassWSApi? _wsApi;
    private bool _started = false;
    private bool _firstConnection = true;
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
                _started = true;
            }
        }
    }

    public ConnectionStates ConnectionState => _wsApi?.ConnectionState ?? ConnectionStates.Disconnected;

    private async void _wsApi_ConnectionStateChanged(object? sender, ConnectionStates e)
    {
        if (_firstConnection && e == ConnectionStates.Connected)
        {
            _firstConnection = false;
            await _wsApi!.AddEventHandlerSubscriptionAsync(EventHandlerSubscriptionStateChanged, HassClient.Models.KnownEventTypes.StateChanged);
        }
        HAClientService.ClientHandlerConnectionStateChanged(this, e);
    }

    private async Task ConnectAsync()
    {
        if (_wsApi == null) return;

        var endPoint = HAClient.Host;
        if (!endPoint.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) && !endPoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            endPoint = $"ws://{endPoint}";
        }
        if (!endPoint.EndsWith("/api/websocket", StringComparison.OrdinalIgnoreCase))
        {
            endPoint = $"{endPoint}/api/websocket";
        }
        try
        {
            var conPar = new ConnectionParameters()
            {
                Endpoint = new Uri(endPoint),
                AccessToken = HAClient.Token
            };
            await _wsApi.ConnectAsync(conPar);
        }
        catch
        {
            //todo
        }
    }

    public async void EventHandlerSubscriptionStateChanged(object? sender, EventResultInfo eventResultInfo)
    {
        var data = HassClient.Serialization.HassSerializer.DeserializeObject<EventResultInfoStateChanged>(eventResultInfo.Data);
        if (data?.New_state?.Entity_id != null && _variables.TryGetValue(data.New_state.Entity_id, out var variable))
        {
            if (variable.VariableValue == null)
            {
                variable.VariableValue = new VariableValue()
                {
                    Handle = Guid.NewGuid(),
                    Variable = variable.Variable,
                    Value = data.New_state.State
                };
                await VariableService.CreateVariableValueAsync(variable.VariableValue);
            }
            else
            {
                await VariableService.UpdateVariableValueAsync(variable.VariableValue, data.New_state.State);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started && _wsApi != null)
        {
            if (!_firstConnection)
            {
                _wsApi.ConnectionStateChanged -= _wsApi_ConnectionStateChanged;
            }
            await _wsApi.RemoveEventHandlerSubscriptionAsync(EventHandlerSubscriptionStateChanged);
        }
    }
}
