using System.Collections.Concurrent;
using Hasm.Models;
using Wolverine;

namespace Hasm.Services;

public class ClientService(DataService _dataService, IServiceScopeFactory _serviceScopeFactory)
{
    private readonly ConcurrentDictionary<int, IClientHandler> _handlers = [];

    public async Task StartAsync()
    {
        var clients = _dataService.GetClients();
        foreach(var client in clients)
        {
            await AddClientAsync(client);
        }
    }

    public List<IClientHandler> GetClients()
    {     
        return _handlers.Values.ToList();
    }

    public List<T> GetClients<T>() where T : IClientHandler
    {
        return _handlers.Values.OfType<T>().ToList();
    }

    public async Task Handle(Client client)
    {
        IClientHandler? clientHandler = null;
        if (client.Id < 0)
        {
            clientHandler = await RemoveClientAsync(-client.Id);
            if ( (clientHandler != null))
            {
                clientHandler.Client.Id = client.Id;
            }
        }
        else if (_handlers.TryGetValue(client.Id, out clientHandler))
        {
            await clientHandler.UpdateAsync(client);
        }
        else
        {
            clientHandler = await AddClientAsync(client);
        }

        if (clientHandler != null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.SendAsync(clientHandler!);
        }
    }

    private async Task<IClientHandler?> RemoveClientAsync(int id)
    {
        IClientHandler? clientHandler = null;
        if (_handlers.TryRemove(id, out clientHandler))
        {
            await clientHandler.DisposeAsync();
        }
        return clientHandler;
    }

    private async Task<IClientHandler?> AddClientAsync(Client client)
    {
        IClientHandler? clientHandler = null;
        switch (client.ClientType)
        {
            case Models.ClientType.HomeAssistant:
                clientHandler = new HAClientHandler(client, _serviceScopeFactory);
                if (!_handlers.TryAdd(client.Id, clientHandler))
                {
                    clientHandler = null;
                }
                break;
        }
        if (clientHandler != null)
        {
            await clientHandler.StartAsync();
        }
        return clientHandler;
    }
}

public class ClientServiceMessageHandler
{
    public async Task Handle(Client client, ClientService clientService)
    {
        await clientService.Handle(client);
    }
}
