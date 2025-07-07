using System.Collections.Concurrent;
using Hasm.Models;
using HassClient.WS;
using MQTTnet;
using MQTTnet.Client;

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

    private ClientProperties _clientProperties = new();
    private IMqttClient? _mqttClient;
    private Timer? _reconnectTimer = null;
    private readonly ConcurrentDictionary<string, List<VariableService.VariableInfo>> _variables = [];

    public Client Client => _client;

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    public async Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        foreach (var variable in variables)
        {
            var existingVariable = _variables.Values.SelectMany(x => x)
                .Where(x => x.Variable.Id == variable.Variable.Id)
                .Select(x => x)
                .FirstOrDefault();

            if (existingVariable != null)
            {
                await UpdateVariableInfoAsync(existingVariable, variable);
            }
            else if (!string.IsNullOrWhiteSpace(variable.Variable.Data))
            {
                await AddVariableInfoAsync(variable);
            }
        }
    }

    private async Task AddVariableInfoAsync(VariableService.VariableInfo variable)
    {
        if (!_variables.TryGetValue(variable.Variable.Data!, out var variableList))
        {
            variableList = [];
            _variables.TryAdd(variable.Variable.Data!, variableList);
        }
        variableList.Add(variable);
        if (variableList.Count == 1)
        {
            if (_mqttClient == null && _mqttClient?.IsConnected == true)
            {
                try
                {
                    var options = new MqttClientSubscribeOptions()
                    {
                        TopicFilters = [new MQTTnet.Packets.MqttTopicFilter() { Topic = variable.Variable.Data }]
                    };
                    await _mqttClient!.SubscribeAsync(options);
                }
                catch
                {
                    //nothing
                }
            }
        }
    }

    private async Task UpdateVariableInfoAsync(VariableService.VariableInfo orgVariable, VariableService.VariableInfo variable)
    {
        if (orgVariable.Variable.Data != variable.Variable.Data)
        {
            await DeleteVariableInfoAsync([orgVariable]);
            if (!string.IsNullOrWhiteSpace(variable.Variable.Data))
            {
                await AddVariableInfoAsync(orgVariable);
            }
        }
    }

    public async Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        foreach (var variable in variables)
        {
            if (!string.IsNullOrWhiteSpace(variable.Variable.Data) && _variables.TryGetValue(variable.Variable.Data, out var variableList))
            {
                var variableInList = variableList.FirstOrDefault(x => x.Variable.Id == Math.Abs(variable.Variable.Id));
                if (variableInList != null)
                {
                    variableList.Remove(variableInList);
                    if (!variableList.Any())
                    {
                        _variables.TryRemove(variable.Variable.Data, out _);
                        if (_mqttClient != null && _mqttClient.IsConnected)
                        {
                            try
                            {
                                var options = new MqttClientUnsubscribeOptions()
                                {
                                    TopicFilters = [variable.Variable.Data]
                                };
                                await _mqttClient.UnsubscribeAsync(options);
                            }
                            catch
                            {
                                //nothing
                            }
                        }
                    }
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeMqttClient();
        return ValueTask.CompletedTask;
    }

    public async Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        var existingVariable = variableId == null ? null : _variables.Values.SelectMany(x => x)
              .Where(x => x.Variable.Id == variableId)
              .Select(x => x)
              .FirstOrDefault();

        if (variableId != null && existingVariable == null)
        {
            return false;
        }

        var result = false;
        switch (command.ToLower())
        {
            case "publish":
                if (parameter1 != null && parameter2 != null)
                {
                    result = await PublishAsync(parameter1.ToString()!, parameter2.ToString()!);
                }
                break;
        }
        return result;
    }

    public async Task StartAsync()
    {
        DisposeMqttClient();
        _variables.Clear();
        var variables = _variableService.GetVariables()
          .Where(x => x.Variable.ClientId == _client.Id);
        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Variable.Data))
            {
                continue;
            }

            if (!_variables.TryGetValue(variable.Variable.Data, out var list))
            {
                list = new List<VariableService.VariableInfo>();
                _variables.TryAdd(variable.Variable.Data, list);
            }
            list.Add(variable);
        }

        if (!string.IsNullOrWhiteSpace(_client.Data))
        {
            _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(_client.Data) ?? new();
        }
        await CreateMqttClientAsync();
    }

    public async Task UpdateAsync(Client client)
    {
        DisposeMqttClient();
        _client = client;
        if (!string.IsNullOrWhiteSpace(client.Data))
        {
            _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(client.Data) ?? new();
        }
        else
        {
            _clientProperties = new();
        }
        await CreateMqttClientAsync();
    }

    private async Task CreateMqttClientAsync()
    {
        var mqttFactory = new MQTTnet.MqttFactory();
        _mqttClient = mqttFactory.CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        if (_client.Enabled && !string.IsNullOrWhiteSpace(_clientProperties.Host))
        {
            await CheckConnectionAsync();
        }
    }

    private async Task CheckConnectionAsync()
    {
        if (_mqttClient != null)
        {
            bool oldState = _mqttClient.IsConnected;
            try
            {
                if (!await _mqttClient.TryPingAsync())
                {
                    //try connect
                    var parts = _clientProperties.Host.Split(':', 2);
                    var mqttClientOptionsPreBuild = new MqttClientOptionsBuilder();

                    if (parts.Length == 1)
                    {
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithTcpServer(_clientProperties.Host);
                    }
                    else
                    {
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithTcpServer(parts[0], int.Parse(parts[1]));
                    }

                    if (_clientProperties.Tls)
                    {
                        var tlsOptions = new MqttClientTlsOptions()
                        {
                            UseTls = true
                        };
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithTlsOptions(tlsOptions);
                    }

                    if (!string.IsNullOrWhiteSpace(_clientProperties.Username))
                    {
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithCredentials(_clientProperties.Username, _clientProperties.Password);
                    }

                    var mqttClientOptions = mqttClientOptionsPreBuild.Build();
                    using (var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);

                        if (_variables.Any())
                        {
                            var options = new MqttClientSubscribeOptions()
                            {
                                TopicFilters = _variables.Keys.Select(x => new MQTTnet.Packets.MqttTopicFilter() { Topic = x }).ToList()
                            };
                            await _mqttClient!.SubscribeAsync(options);
                        }
                    }
                }
            }
            catch
            {
                //none
            }
            _reconnectTimer = new Timer(async (state) =>
            {
                await CheckConnectionAsync();
            }, null, 5000, Timeout.Infinite);

            if (_mqttClient != null && oldState != _mqttClient.IsConnected)
            {
                await _messageBusService.PublishAsync(new ClientConnectionInfo()
                {
                    ClientId = _client.Id,
                    IsConnected =_mqttClient.IsConnected,
                });
            }
        }
    }

    private void DisposeMqttClient()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
        if (_mqttClient != null)
        {
            _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceived;
            try
            {
                _mqttClient.Dispose();
            }
            catch
            {
                //nothing
            }
            _mqttClient = null;
        }
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage?.Topic;
        if (!string.IsNullOrWhiteSpace(topic))
        {
            switch (topic)
            {
                case "$SYS/broker/publish/messages/received":
                case "$SYS/broker/publish/messages/sent":
                case "$SYS/broker/messages/sent":
                case "$SYS/broker/messages/received":
                case "$SYS/broker/publish/bytes/received":
                case "$SYS/broker/publish/bytes/sent":
                case "$SYS/broker/bytes/sent":
                case "$SYS/broker/bytes/received":
                    break;
                default:
                    try
                    {
                        var payload = args.ApplicationMessage.ConvertPayloadToString();

                        await UpdateVariableValue(topic, payload);
                    }
                    catch
                    {
                        //yep
                    }
                    break;
            }
        }
    }

    private async Task UpdateVariableValue(string topic, string? payload)
    {
        if (_variables.TryGetValue(topic, out var variableList))
        {
            if (variableList.Any())
            {
                await _variableService.SetVariableValuesAsync(variableList.Select(x => (variableId: x.Variable.Id, value: payload)).ToList());
            }
        }
    }

    private async Task<bool> PublishAsync(string topic, string? data)
    {
        if (!_mqttClient?.IsConnected == true)
        {
            return false;
        }

        var msgBuilder = new MQTTnet.MqttApplicationMessageBuilder()
            .WithTopic(topic);

        //in future more options than string?
        msgBuilder = msgBuilder.WithPayload(data);

        var msg = msgBuilder.Build();

        try
        {
            var result = await _mqttClient!.PublishAsync(msg, CancellationToken.None);
            return result.IsSuccess;
        }
        catch
        {

            return false;
        }
    }

}
