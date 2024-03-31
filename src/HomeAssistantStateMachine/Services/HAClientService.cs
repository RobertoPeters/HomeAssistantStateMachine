using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HomeAssistantStateMachine.Services;

public class HAClientService : ServiceDbBase
{
    private readonly IConfiguration _configuration;
    private readonly VariableService _variableService;
    private readonly ConcurrentDictionary<int, HAClientHandler> _handlers = [];
    private bool _started = false;

    public event EventHandler<ConnectionStates>? ConnectionChanged;

    public HAClientService(IDbContextFactory<HasmDbContext> dbFactory, IConfiguration configuration, VariableService variableService) : base(dbFactory)
    {
        _configuration = configuration;
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
                    _handlers.TryAdd(client.Id, clientHandler);
                }

                return true;
            });

            foreach (var handler in _handlers.Values)
            {
                await handler.StartAsync();
            }

            // TEMP CODE FOR DEVELEOPMENT
            //testcode when database is recreated preventing to always add this manually
            // as soon as we don't reacrteate the database anymore, we will remove this code
            var testClientHost = _configuration.GetValue<string?>("TestHAClientHost", null);
            var testClientToken = _configuration.GetValue<string>("TestHAClientToken");
            if (!string.IsNullOrWhiteSpace(testClientHost)
                && !string.IsNullOrWhiteSpace(testClientToken)
                && !_handlers.Values.ToList().Exists(x => x.HAClient.Name == "Test"))
            {
                var handler = await CreateHAClientAsync("Test", true, testClientHost, testClientToken);
                await handler!.CreateVariableAsync("test", "input_boolean.test");
                await handler!.CreateVariableAsync("test2", "input_boolean.test2");
            }
            //END TEMP CODE

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
                _handlers.TryAdd(client.Id, result);
                await result.StartAsync();
            });
            return result;
        });
    }

    public void ClientHandlerConnectionStateChanged(HAClientHandler clientHandler, ConnectionStates state)
    {
        ConnectionChanged?.Invoke(clientHandler, state);
    }

    public List<HAClientHandler> GetClients()
    {
        return _handlers.Values.ToList();
    }
}
