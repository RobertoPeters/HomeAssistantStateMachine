using System.Collections.Concurrent;
using Hasm.Models;

namespace Hasm.Services;

public class ClientService(DataService _dataService, VariableService _variableService, MessageBusService _messageBusService)
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

    public async Task Handle(List<VariableService.VariableInfo> variables)
    {
        var variablesToHandle = variables.ToLookup(x => x.Variable.ClientId, x => x);
        foreach(var variableGroup in variablesToHandle)
        {
            if (_handlers.TryGetValue(variableGroup.Key, out var clientHandler))
            {
                if (clientHandler.Client.Enabled)
                {
                    var deletedGroup = variableGroup.Where(x => x.Variable.Id < 0).ToList();
                    var addOrUpdatedGroup = variableGroup.Where(x => x.Variable.Id >= 0).ToList();
                    if (deletedGroup.Any())
                    {
                        await clientHandler.DeleteVariableInfoAsync(deletedGroup);
                    }
                    else if (addOrUpdatedGroup.Any())
                    {
                        await clientHandler.AddOrUpdateVariableInfoAsync(addOrUpdatedGroup);
                    }
                }
            }
        }
    }

    public async Task<bool> ExecuteAsync(int clientId, int? variableId, string command, string? parameter)
    {
        if (_handlers.TryGetValue(clientId, out var clientHandler))
        {
            return await clientHandler.ExecuteAsync(variableId, command, parameter);
        }
        return false;
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
            await _messageBusService.PublishAsync(clientHandler!);
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
                break;
            case Models.ClientType.Generic:
                clientHandler = new GenericClientHandler(client);
                break;
            case Models.ClientType.Timer:
                clientHandler = new TimerClientHandler(client, _variableService);
                break;
        }
        if (clientHandler != null)
        {
            if (!_handlers.TryAdd(client.Id, clientHandler))
            {
                clientHandler = null;
            }
            else
            {
                await clientHandler.StartAsync();
            }
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

    public async Task Handle(VariableService.VariableInfo variable, ClientService clientService)
    {
        await clientService.Handle([variable]);
    }

    public async Task Handle(List<VariableService.VariableInfo> variables, ClientService clientService)
    {
        await clientService.Handle(variables);
    }
}
