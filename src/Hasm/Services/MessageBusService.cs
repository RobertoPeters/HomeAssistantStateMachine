using Wolverine;

namespace Hasm.Services;

public class MessageBusService(IServiceScopeFactory _serviceScopeFactory)
{
    private bool _isStarted = false;

    public ValueTask PublishAsync<T>(T message, DeliveryOptions? options = null)
    {
        if (!_isStarted) return ValueTask.CompletedTask;

        using var scope = _serviceScopeFactory.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return bus.PublishAsync(message, options);
    }

    public Task StartAsync()
    {
        if (!_isStarted)
        {
            _isStarted = true;
        }
        return Task.CompletedTask;
    }
}
