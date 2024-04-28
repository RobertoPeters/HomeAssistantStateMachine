using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HomeAssistantStateMachine.Services;

public class MqttClientService : ServiceDbBase
{
    private readonly VariableService _variableService;
    private readonly ConcurrentDictionary<string, MqttClientHandler> _handlers = [];
    private bool _started = false;

    public event EventHandler<bool>? ConnectionChanged;

    public MqttClientService(IDbContextFactory<HasmDbContext> dbFactory, VariableService variableService) : base(dbFactory)
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
                var clients = await context.MqttClients.ToListAsync();
                foreach (var client in clients)
                {
                    var clientHandler = new MqttClientHandler(this, client, _variableService);
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

    public async Task<MqttClientHandler?> CreateMqttClientAsync(MqttClient client, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            MqttClientHandler? result = null;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                await context.AddAsync(client);
                await context.SaveChangesAsync();
                result = new MqttClientHandler(this, client, _variableService);
                _handlers.TryAdd(client.Name, result);
                await result.StartAsync();
            });
            return result;
        });
    }

    public async Task DeleteMqttClientAsync(MqttClientHandler client, HasmDbContext? ctx = null)
    {
        await ExecuteOnDbContextWithinTransactionAsync(ctx, async (context) =>
        {
            _handlers.TryRemove(client.MqttClient.Name, out var _);
            client.Dispose();
            var mqttClient = await context.MqttClients.FirstAsync(x => x.Id == client.MqttClient.Id);
            await _variableService.DeleteVariablesAsync(client.MqttClient.Id, null, null, context);
            context.Remove(mqttClient);
            await context.SaveChangesAsync();
        });
    }

    public async Task UpdateMqttClientAsync(MqttClientHandler client, MqttClient mqttClient, HasmDbContext? ctx = null)
    {
        MqttClient? mClient = null;
        if (await ExecuteOnDbContextWithinTransactionAsync(ctx, async (context) =>
        {
            mClient = await context.MqttClients.FirstAsync(x => x.Id == mqttClient!.Id);
            mClient.Name = mqttClient.Name;
            mClient.Enabled = mqttClient.Enabled;
            mClient.Host = mqttClient.Host;
            mClient.Tls = mqttClient.Tls;
            mClient.Username = mqttClient.Username;
            mClient.Password = mqttClient.Password;
            mClient.WebSocket = mqttClient.WebSocket;
            await context.SaveChangesAsync();
        }))
        {
            var oldName = client.MqttClient.Name;
            _handlers.TryRemove(oldName, out var _);
            await client.UpdateMqttClientAsync(mClient!, ctx);
            _handlers.TryAdd(mqttClient.Name, client);
        }
    }

    public void ClientHandlerConnectionStateChanged(MqttClientHandler clientHandler, bool connected)
    {
        ConnectionChanged?.Invoke(clientHandler, connected);
    }

    public List<MqttClientHandler> GetClients()
    {
        return _handlers.Values.ToList();
    }

    public MqttClientHandler? GetClient(int id)
    {
        return _handlers.Values.FirstOrDefault(x => x.MqttClient.Id == id);
    }


    public MqttClientHandler? GetClientHandler(string? name)
    {
        MqttClientHandler? result = null;
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

    public async Task<bool> PublishAsync(string? clientName, string topic, string? data)
    {
        var client = GetClientHandler(clientName);
        if (client != null)
        {
            return await client.PublishAsync(topic, data);
        }
        return false;
    }
}
