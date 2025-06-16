using System.Collections.Concurrent;
using Hasm.Models;
using Wolverine;

namespace Hasm.Services;

public class ClientService(DataService _dataService, MessageBusService _messageBusService)
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

    public async Task Handle(Variable variable)
    {
        if (!_handlers.TryGetValue(variable.ClientId, out var clientHandler))
        {
            return;
        }
        if (variable.Id < 0)
        {
            await clientHandler.DeleteVariableAsync(variable);
        }
        else
        {
            await clientHandler.AddOrUpdateVariableAsync(variable);
        }
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
            await _messageBusService.SendAsync(clientHandler!);
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
                clientHandler = new HAClientHandler(client, _messageBusService);
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

    public async Task Handle(Variable variable, ClientService clientService)
    {
        await clientService.Handle(variable);
    }
}
