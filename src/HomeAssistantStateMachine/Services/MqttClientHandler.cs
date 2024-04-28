using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using MQTTnet.Client;
using MQTTnet;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class MqttClientHandler : IDisposable
{
    private sealed class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue? VariableValue { get; set; }
    }

    public MqttClientService MqttClientService { get; private set; }
    public Models.MqttClient MqttClient { get; private set; }
    public VariableService VariableService { get; private set; }

    private IMqttClient? _mqttClient;
    private bool _started = false;
    private Timer? _reconnectTimer = null;
    private readonly ConcurrentDictionary<string, List<VariableInfo>> _variables = [];
    private readonly ConcurrentDictionary<string, Dictionary<int, Action<object>>> _variablesChangeCallBack = [];

    public bool Connected => _mqttClient?.IsConnected == true;

    public MqttClientHandler(MqttClientService mqttClientService, Models.MqttClient mqttClient, VariableService variableService)
    {
        MqttClientService = mqttClientService;
        MqttClient = mqttClient;
        VariableService = variableService;
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            var variables = VariableService.GetScopedVariables(MqttClient, null, null, null);
            foreach (var variable in variables)
            {
                if (!string.IsNullOrWhiteSpace(variable.variable.Data))
                {
                    if (!_variables.TryGetValue(variable.variable.Data, out var variableInfos))
                    {
                        variableInfos = [];
                        _variables.TryAdd(variable.variable.Data, variableInfos);
                    }
                    var v = new VariableInfo()
                    {
                        Variable = variable.variable,
                        VariableValue = variable.variableValue
                    };
                    variableInfos.Add(v);
                }
            }
            await CreateMqttClientAsync();
        }
    }

    public async Task UpdateMqttClientAsync(Models.MqttClient mqttCliennt, HasmDbContext? ctx = null)
    {
        _started = false;
        DisposeMqttClient();
        MqttClient = mqttCliennt;
        await CreateMqttClientAsync();
    }

    private async Task CreateMqttClientAsync()
    {
        var mqttFactory = new MQTTnet.MqttFactory();
        _mqttClient = mqttFactory.CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        await CheckConnectionAsync();
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
                        if (_variablesChangeCallBack.TryGetValue(topic, out var callbacks))
                        {
                            foreach (var callback in callbacks)
                            {
                                try
                                {
                                    callback.Value(args);
                                }
                                catch
                                {
                                    //ignore
                                }
                            }
                        }
                    }
                    catch
                    {
                        //yep
                    }
                    break;
            }
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
                    var parts = MqttClient.Host.Split(':', 2);
                    var mqttClientOptionsPreBuild = new MqttClientOptionsBuilder();

                    if (parts.Length == 1)
                    {
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithTcpServer(MqttClient.Host);
                    }
                    else
                    {
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithTcpServer(parts[0], int.Parse(parts[1]));
                    }

                    if (MqttClient.Tls)
                    {
                        var tlsOptions = new MqttClientTlsOptions()
                        {
                             UseTls = true
                        };
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithTlsOptions(tlsOptions);
                    }

                    if (!string.IsNullOrWhiteSpace(MqttClient.Username))
                    {
                        mqttClientOptionsPreBuild = mqttClientOptionsPreBuild.WithCredentials(MqttClient.Username, MqttClient.Password);
                    }

                    var mqttClientOptions = mqttClientOptionsPreBuild.Build();
                    using (var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
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
                MqttClientService.ClientHandlerConnectionStateChanged(this, _mqttClient.IsConnected);
            }
        }
    }

    public void DisposeMqttClient()
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

    public void Dispose()
    {
        if (_started)
        {
            DisposeMqttClient();
        }
    }

    public async Task DeleteVariableAsync(string name, HasmDbContext? ctx = null)
    {
        var existingVariable = VariableService.GetVariable(name);
        if (existingVariable != null)
        {
            var mqttEntityId = existingVariable.Data ?? existingVariable.Name;
            if (_variables.TryGetValue(mqttEntityId, out var variableInfos))
            {
                var varibleInfo = variableInfos.Find(v => v.Variable.Id == existingVariable.Id);
                if (varibleInfo != null)
                {
                    variableInfos.Remove(varibleInfo);
                    if (!variableInfos.Any())
                    {
                        _variables.TryRemove(mqttEntityId, out _);
                        if (_mqttClient != null && _mqttClient.IsConnected)
                        {
                            try
                            {
                                var options = new MqttClientUnsubscribeOptions()
                                {
                                    TopicFilters = [mqttEntityId]
                                };
                                await _mqttClient.UnsubscribeAsync(options);
                            }
                            catch
                            {
                                //nothing
                            }
                        }
                    }
                    await VariableService.DeleteVariableAsync(varibleInfo.Variable.Name, ctx);
                }
            }
        }
    }

    public bool SetStateChangedCallback(int registrarId, Variable variable, Action<object> callback)
    {
        var result = false;
        if (variable != null)
        {
            var mqttEntityId = variable.Data ?? variable.Name;
            if (!_variablesChangeCallBack.TryGetValue(mqttEntityId, out var callbacks))
            {
                callbacks = [];
                _variablesChangeCallBack.TryAdd(mqttEntityId, callbacks);
            }
            if (callbacks.TryGetValue(registrarId, out _))
            {
                callbacks.Remove(registrarId);
            }
            callbacks.TryAdd(registrarId, callback);
            return true;
        }
        return result;
    }

    public void RemoveRegistrarFromStateChangedCallback(int registrarId)
    {
        foreach (var item in from item in _variablesChangeCallBack.Values.ToList()
                             where item.ContainsKey(registrarId)
                             select item)
        {
            item.Remove(registrarId);
        }
    }

    public async Task<bool> PublishAsync(string topic, string? data)
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

    public async Task<Variable?> CreateVariableAsync(string name, string? data, HasmDbContext? ctx = null)
    {
        Variable? result = null;
        var mqttEntityId = data ?? name;

        var existingVariable = VariableService.GetVariable(name);
        if (existingVariable != null && existingVariable.Data == data && existingVariable.MqttClientId == MqttClient.Id)
        {
            return existingVariable;
        }

        //todo: check data has changed!

        result = await VariableService.CreateVariableAsync(name, data, null, MqttClient, null, null, ctx);
        if (result != null && !string.IsNullOrWhiteSpace(result.Data))
        {
            VariableValue? vv = null;
            if (!_variables.TryGetValue(mqttEntityId, out var variableInfos))
            {
                variableInfos = [];
                _variables.TryAdd(mqttEntityId, variableInfos);
            }
            else
            {
                vv = variableInfos.Find(v => v.VariableValue != null)?.VariableValue;
            }
            var v = new VariableInfo()
            {
                Variable = result,
                VariableValue = null
            };
            variableInfos.Add(v);

            if (vv != null && vv.Value != null)
            {
                await UpdateVariableValue(mqttEntityId, vv.Value);
            }
            else if (vv == null && _mqttClient?.IsConnected == true)
            {
                try
                {
                    var options = new MqttClientSubscribeOptions()
                    {
                        TopicFilters = [new MQTTnet.Packets.MqttTopicFilter() { Topic = mqttEntityId }]
                    };
                    await _mqttClient!.SubscribeAsync(options);
                }
                catch
                {
                    //nothing
                }
            }
        }
        return result;
    }

    private async Task UpdateVariableValue(string eventId, string? state)
    {
        if (_variables.TryGetValue(eventId, out var variableInfos))
        {
            foreach (var variableInfo in variableInfos)
            {
                if (variableInfo.VariableValue == null)
                {
                    variableInfo.VariableValue = new VariableValue()
                    {
                        Variable = variableInfo.Variable,
                        Value = state
                    };
                    await VariableService.CreateVariableValueAsync(variableInfo.VariableValue);
                }
                else
                {
                    await VariableService.UpdateVariableValueAsync(variableInfo.VariableValue, state);
                }
            }
        }
    }
}
