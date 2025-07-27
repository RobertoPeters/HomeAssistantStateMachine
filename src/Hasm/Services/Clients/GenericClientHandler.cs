using Hasm.Models;
using Hasm.Services.Interfaces;
using System.Collections.Concurrent;

namespace Hasm.Services.Clients;

public class GenericClientHandler(Client _client) : IClientHandler
{
    public Client Client => _client;

    public Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        return Task.CompletedTask;
    }

    public Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
     }

    public async Task UpdateAsync(Client client)
    {
        _client = client;
        await StartAsync();
    }

    public Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        return Task.FromResult(false);
    }

}
