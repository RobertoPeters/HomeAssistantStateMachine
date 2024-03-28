using HassClient.WS;
using HassClient.WS.Messages;
using HomeAssistantStateMachine.Models;

namespace HomeAssistantStateMachine.Services;

public class HAClientHandler : IAsyncDisposable
{
    public HAClientService HAClientService { get; private set; }
    public HAClient HAClient { get; private set; }

    private readonly HassWSApi _wsApi;
    private bool _started = false;
  
    public HAClientHandler(HAClientService haClientService, HAClient haClient)
    {
        HAClientService = haClientService;
        HAClient = haClient;
        _wsApi = new HassWSApi();
        _wsApi.ConnectionStateChanged += _wsApi_ConnectionStateChanged;
    }

    public async Task StartAsync()
    {
        if (HAClient.Enabled && !string.IsNullOrWhiteSpace(HAClient.Token) && !string.IsNullOrWhiteSpace(HAClient.Host))
        {
            await ConnectAsync();
            await _wsApi.AddEventHandlerSubscriptionAsync(EventHandlerSubscription);
            _started = true;
        }
    }

    public ConnectionStates ConnectionState => _wsApi.ConnectionState;

    private void _wsApi_ConnectionStateChanged(object? sender, ConnectionStates e)
    {
        HAClientService.ClientHandlerConnectionStateChanged(this, e);
    }

    private async Task ConnectAsync()
    {
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

    public void EventHandlerSubscription(object? sender, EventResultInfo eventResultInfo)
    {
        //todo
    }

    public async ValueTask DisposeAsync()
    {
        _wsApi.ConnectionStateChanged -= _wsApi_ConnectionStateChanged;
        if (_started)
        {
            await _wsApi.RemoveEventHandlerSubscriptionAsync(EventHandlerSubscription);
        }
    }
}
