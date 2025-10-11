using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using Hasm.Models;
using Hasm.Services.Interfaces;
using Microsoft.Extensions.AI;


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
                    return await GenerateContentAsync(variableId.Value, parameter1.ToString()!);
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

    private async Task<bool> GenerateContentAsync(int variableId, string prompt)
    {
        var geminiClient = new GeminiChatClient(new GeminiClientOptions { ApiKey = _clientProperties.ApiKey!, ModelId = _clientProperties.Model });

        IChatClient client = new ChatClientBuilder(geminiClient)
            .UseFunctionInvocation()
            .Build();

        List<ChatMessage> messages =
        [
            new(ChatRole.User, prompt)
        ];

        var options = new ChatOptions
        {
            //Tools = [AIFunctionFactory.Create(GetCurrentWeather, nameof(GetCurrentWeather))]
        };

        var response = await client.GetResponseAsync(messages, options, default);
        return await _variableService.SetVariableValuesAsync([(variableId, response?.Text)]);
    }

    private void StartGenerateContent(int variableId, object parameter1)
    {
        Task.Factory.StartNew(async () =>
        {
            try
            {
                await GenerateContentAsync(variableId, parameter1.ToString()!);
            }
            catch
            {
                //nothing we can do
            }
        });
    }
}