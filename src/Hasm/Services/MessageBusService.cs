using Wolverine;

namespace Hasm.Services;

public class MessageBusService(IServiceScopeFactory _serviceScopeFactory)
{
    public ValueTask SendAsync<T>(T message, DeliveryOptions? options = null)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return bus.SendAsync(message, options);
    }

}
