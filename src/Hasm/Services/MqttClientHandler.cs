using System.ComponentModel.DataAnnotations;
using Hasm.Models;

namespace Hasm.Services;

public class MqttClientHandler(Client _client, VariableService _variableService, MessageBusService _messageBusService) : IClientHandler, IClientConnected
{
    public class ClientProperties
    {
        public string? Host { get; set; }
        public bool Tls { get; set; }
        public bool WebSocket { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public Client Client => throw new NotImplementedException();

    public bool IsConnected => throw new NotImplementedException();

    public Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        throw new NotImplementedException();
    }

    public Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        throw new NotImplementedException();
    }

    public Task StartAsync()
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(Client client)
    {
        throw new NotImplementedException();
    }
}
