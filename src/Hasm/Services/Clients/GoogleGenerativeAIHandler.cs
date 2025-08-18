using GenerativeAI;
using Hasm.Models;
using Hasm.Services.Interfaces;
using Microsoft.Data.Sqlite;

namespace Hasm.Services.Clients;

public class GoogleGenerativeAIClientHandler(Client _client, VariableService _variableService) : IClientHandler
{
    public class ClientProperties
    {
        public string? Model { get; set; }
        public string? ApiKey { get; set; }
    }

    private ClientProperties _clientProperties = new();
 
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
        if (!string.IsNullOrWhiteSpace(_client.Data))
        {
            _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(_client.Data) ?? new();
        }

        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Client client)
    {
        _client = client;
        await StartAsync();
    }

    public async Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        if (!_client.Enabled || variableId == null || string.IsNullOrWhiteSpace(_clientProperties.Model) || string.IsNullOrWhiteSpace(_clientProperties.ApiKey))
        {
            return false;
        }

        var result = false;
        switch (command.ToLower())
        {
            case "generatecontent":
                if (parameter1 != null)
                {
                    var googleAI = new GoogleAi(_clientProperties.ApiKey);
                    var googleModel = googleAI.CreateGenerativeModel($"models/{_clientProperties.Model}");
                    var googleResponse = await googleModel.GenerateContentAsync(parameter1.ToString()!);
                    result = await _variableService.SetVariableValuesAsync([(variableId.Value, googleResponse.Text())]);
                }
                break;
            case "startgeneratecontent":
                if (parameter1 != null)
                {
                    StartGenerateContent(variableId.Value, parameter1);
                    result = true;
                }
                break;
        }
        return result;
    }

    private void StartGenerateContent(int variableId, object parameter1)
    {
        Task.Factory.StartNew(async () =>
        {
            try
            {
                var googleAI = new GoogleAi(_clientProperties.ApiKey);
                var googleModel = googleAI.CreateGenerativeModel($"models/{_clientProperties.Model}");
                var googleResponse = await googleModel.GenerateContentAsync(parameter1.ToString()!);
                await _variableService.SetVariableValuesAsync([(variableId, googleResponse.Text())]);
            }
            catch
            {
                //nothing we can do
            }
        });
    }
}
