using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class HAClientService : ServiceDbBase
{
    private readonly ConcurrentDictionary<Guid, HAClientHandler> _handlers = [];

    public event EventHandler<ConnectionStates>? ConnectionChanged;

    public HAClientService(IDbContextFactory<HasmDbContext> dbFactory, IConfiguration configuration) : base(dbFactory)
    {
        //load data and create state maching handlers
        ExecuteOnDbContext(null, (context) =>
        {
            var clients = context.HAClients.ToList();
            foreach (var client in clients)
            {
                var clientHandler = new HAClientHandler(this, client);
                _handlers.TryAdd(client.Handle, clientHandler);
            }

            // TEMP CODE FOR DEVELEOPMENT
            //testcode when database is recreated preventing to always add this manually
            // as soon as we don't reacrteate the database anymore, we will remove this code
            var testClientHost = configuration.GetValue<string?>("TestHAClientHost", null);
            var testClientToken = configuration.GetValue<string>("TestHAClientToken");
            if (!string.IsNullOrWhiteSpace(testClientHost) 
                && !string.IsNullOrWhiteSpace(testClientToken)
                && !clients.Exists(x => x.Host == testClientHost))
            {
                ExecuteWithinTransaction(context, () =>
                {
                    var client = new HAClient
                    {
                        Handle = Guid.NewGuid(),
                        Name = "Test",
                        Enabled = true,
                        Host = testClientHost,
                        Token = testClientToken
                    };
                    context.Add(client);
                    context.SaveChanges();
                    var clientHandler = new HAClientHandler(this, client);
                    _handlers.TryAdd(client.Handle, clientHandler);
                 });
            }
            //END TEMP CODE

            return true;
        });
    }

    public async Task StartAsync()
    {
        foreach(var handler in _handlers.Values)
        {
            await handler.StartAsync();
        }
    }

    public async Task<HAClientHandler?> CreateHAClientAsync(Guid handle, string name, bool enabled, string host, string token, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            HAClientHandler? result = null;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                var client = new HAClient
                {
                    Handle = handle,
                    Name = name,
                    Enabled = enabled,
                    Host = host,
                    Token = token
                };
                await context.AddAsync(client);
                await context.SaveChangesAsync();
                result = new HAClientHandler(this, client);
                _handlers.TryAdd(handle, result);
                await result.StartAsync();
            });
            return result;
        });
    }

    public void ClientHandlerConnectionStateChanged(HAClientHandler clientHandler, ConnectionStates state)
    {
        ConnectionChanged?.Invoke(clientHandler, state);
    }

}
