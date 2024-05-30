using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HomeAssistantStateMachine.Services;

public class HAClientService : ServiceDbBase
{
    private readonly VariableService _variableService;
    private readonly ConcurrentDictionary<string, HAClientHandler> _handlers = [];
    private bool _started = false;

    public event EventHandler<ConnectionStates>? ConnectionChanged;

    public HAClientService(IDbContextFactory<HasmDbContext> dbFactory, VariableService variableService) : base(dbFactory)
    {
        _variableService = variableService;
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            _started = true;
            //load data and create state maching handlers
            await ExecuteOnDbContextAsync(null, async (context) =>
            {
                var clients = await context.HAClients.ToListAsync();
                foreach (var client in clients)
                {
                    var clientHandler = new HAClientHandler(this, client, _variableService);
                    _handlers.TryAdd(client.Name, clientHandler);
                }

                return true;
            });

            foreach (var handler in _handlers.Values)
            {
                await handler.StartAsync();
            }
        }
    }

    public async Task<HAClientHandler?> CreateHAClientAsync(string name, bool enabled, string host, string token, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            HAClientHandler? result = null;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                var client = new HAClient
                {
                    Name = name,
                    Enabled = enabled,
                    Host = host,
                    Token = token
                };
                await context.AddAsync(client);
                await context.SaveChangesAsync();
                result = new HAClientHandler(this, client, _variableService);
                _handlers.TryAdd(client.Name, result);
                await result.StartAsync();
            });
            return result;
        });
    }

    public async Task DeleteHAClientAsync(HAClientHandler client, HasmDbContext? ctx = null)
    {
        await ExecuteOnDbContextWithinTransactionAsync(ctx, async (context) =>
        {
            _handlers.TryRemove(client.HAClient.Name, out var _);
            await client.DisposeAsync();
            var haClient = await context.HAClients.FirstAsync(x => x.Id == client.HAClient.Id);
            await _variableService.DeleteHaVariablesAsync(client.HAClient.Id, ctx: context);
            context.Remove(haClient);
            await context.SaveChangesAsync();
        });
    }

    public async Task UpdateHAClientAsync(HAClientHandler client, string name, bool enabled, string host, string token, HasmDbContext? ctx = null)
    {
        HAClient? haClient = null;
        if (await ExecuteOnDbContextWithinTransactionAsync(ctx, async (context) =>
        {
            haClient = await context.HAClients.FirstAsync(x => x.Id == client.HAClient.Id);
            haClient.Name = name;
            haClient.Enabled = enabled;
            haClient.Host = host;
            haClient.Token = token;
            await context.SaveChangesAsync();
        }))
        {
            var oldName = client.HAClient.Name;
            _handlers.TryRemove(oldName, out var _);
            await client.UpdateHAClientAsync(haClient, ctx);
            _handlers.TryAdd(name, client);
        }
    }

    public void ClientHandlerConnectionStateChanged(HAClientHandler clientHandler, ConnectionStates state)
    {
        ConnectionChanged?.Invoke(clientHandler, state);
    }

    public List<HAClientHandler> GetClients()
    {
        return _handlers.Values.ToList();
    }

    public HAClientHandler? GetClient(int id)
    {
        return _handlers.Values.FirstOrDefault(x => x.HAClient.Id == id);
    }


    public HAClientHandler? GetClientHandler(string? name)
    {
        HAClientHandler? result = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            if (_handlers.Count == 1)
            {
                return _handlers.Values.First();
            }
            return null;
        }
        _handlers.TryGetValue(name, out result);
        return result;
    }

    public async Task<bool> CallServiceAsync(string? clientName, string name, string service, object? data = null)
    {
        var client = GetClientHandler(clientName);
        if (client != null)
        {
            return await client.CallServiceAsync(name, service, data);
        }
        return false;
   }

    public async Task<bool> CallServiceForEntitiesAsync(string? clientName, string name, string service, params string[] entityIds)
    {
        var client = GetClientHandler(clientName);
        if (client != null)
        {
            return await client.CallServiceForEntitiesAsync(name, service, entityIds);
        }
        return false;
    }
}
