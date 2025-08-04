using Hasm.Models;
using Hasm.Services.Interfaces;

namespace Hasm.Services.Automations.Script;

public class ScriptHandler : IAutomationHandler
{
    public class AutomationProperties
    {
        public string Script { get; set; } = null!;
    }

    public enum ScriptRunningState
    {
        NotActive,
        Active,
        Error
    }

    public class ScriptHandlerInfo
    {
        public int AutomationId { get; set; }
        public ScriptRunningState? RunningState { get; set; }
    }

    public class ScriptEngineInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Automation Automation { get; set; } = null!;
        public Jint.Engine Engine { get; set; } = null!;
    }

    private Automation _automation;
    private readonly ClientService _clientService;
    private readonly DataService _dataService;
    private readonly VariableService _variableService;
    private readonly MessageBusService _messageBusService;

    private AutomationProperties _automationProperties = new();

    public Automation Automation => _automation;

    private readonly object _lockEngineObject = new object();
    private List<ScriptEngineInfo> _engines = [];
    private SystemMethods? _systemMethods = null;
    private bool _readyForTriggers = false;

    public static string SystemScript => SystemMethods.SystemScript(AutomationType.Script);

    private static System.Text.Json.JsonSerializerOptions logJsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true
    };

    public string? ErrorMessage { get; private set; }

    private ScriptRunningState _runningState = ScriptRunningState.NotActive;
    public ScriptRunningState RunningState
    {
        get => _runningState;
        set
        {
            if (_runningState != value)
            {
                _runningState = value;
                PublishScriptHandlerInfo();
            }
        }
    }

    public ScriptHandler(Automation automation, ClientService clientService, DataService dataService, VariableService variableService, MessageBusService messageBusService)
    {
        _automation = automation;
        _clientService = clientService;
        _dataService = dataService;
        _variableService = variableService;
        _messageBusService = messageBusService;

        _automationProperties = GetAutomationProperties(automation.Data);
    }

    public static AutomationProperties GetAutomationProperties(string? data)
    {
        if (!string.IsNullOrWhiteSpace(data))
        {
            return System.Text.Json.JsonSerializer.Deserialize<AutomationProperties>(data) ?? new();
        }
        return new();
    }

    public void PublishScriptHandlerInfo()
    {
        var info = new ScriptHandlerInfo
        {
            AutomationId = Automation.Id,
            RunningState = RunningState
        };
        _messageBusService.PublishAsync(info);
    }

    public async Task AddLogAsync(string instanceId, object? logObject)
    {
        if (logObject != null)
        {
            var indexOfEngine = _engines.FindIndex(_engines => _engines.Id.ToString() == instanceId);
            var prefix = $"[{string.Join("].[", _engines.Take(indexOfEngine + 1).Select(e => e.Automation.Name))}]";
            var logEvent = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                AutomationId = Automation.Id,
            };

            if (logObject is string txt)
            {
                logEvent.Message = txt;
            }
            else
            {
                logEvent.Message = System.Text.Json.JsonSerializer.Serialize(logObject, logJsonOptions);
            }
            logEvent.Message = $"{prefix}: {logEvent.Message}";

            await _messageBusService.PublishAsync(logEvent);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void DisposeEngines()
    {
        foreach (var engine in _engines)
        {
            engine.Engine.Dispose();
        }
        _engines.Clear();
    }

    public Task Handle(List<VariableService.VariableValueInfo> variableValueInfos)
    {
        RequestTriggerFlow();
        return Task.CompletedTask;
    }

    public Task Handle(List<VariableService.VariableInfo> variableInfos)
    {
        RequestTriggerFlow();
        return Task.CompletedTask;
    }


    private long _triggering = 0;
    private readonly object _triggerLock = new object();
    private void RequestTriggerFlow()
    {
        var count = Interlocked.Read(ref _triggering);
        if (count < 3)
        {
            Interlocked.Increment(ref _triggering);
            Task.Factory.StartNew(() =>
            {
                lock (_triggerLock)
                {
                    TriggerProcess();
                }
                Interlocked.Decrement(ref _triggering);
            });
        }
    }

    public void TriggerProcess()
    {
        if (!_readyForTriggers) return;

        lock (_lockEngineObject)
        {
            if (RunningState == ScriptRunningState.Active && _engines.Any())
            {
                try
                {
                    var index = 0;
                    while (index < _engines.Count)
                    {
                        _engines[index].Engine.Invoke("schedule");
                        index++;
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = $"Error in script{e.Message}";
                    RunningState = ScriptRunningState.Error;
                }
            }
        }

        _messageBusService.PublishAsync(new AutomationInfo
        {
            AutomationId = Automation.Id
        });
    }

    public void Start()
    {
        ErrorMessage = null;
        _readyForTriggers = false;
        if (Automation.Enabled || Automation.IsSubAutomation)
        {
            lock (_lockEngineObject)
            {
                var engine = new ScriptEngineInfo()
                {
                    Engine = new Jint.Engine(),
                    Automation = Automation
                };
                _engines.Add(engine);
                _systemMethods = new SystemMethods(_clientService, _dataService, _variableService, this);
                engine.Engine.SetValue("system", _systemMethods);

                try
                {
                    engine.Engine.Execute(EngineScriptBuilderScript.BuildEngineScript(_automationProperties, engine.Id, null));
                    RunningState = ScriptRunningState.Active;
                    RequestTriggerFlow();
                }
                catch (Exception e)
                {
                    DisposeEngines();
                    ErrorMessage = $"Error initializing flow: {e.Message}";
                    RunningState = ScriptRunningState.Error;
                }
            }
            _readyForTriggers = true;
        }
        else
        {
            RunningState = ScriptRunningState.NotActive;
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        _readyForTriggers = false;
        lock (_lockEngineObject)
        {
            RunningState = ScriptRunningState.NotActive;
            DisposeEngines();
        }
    }

    public Task UpdateAsync(Automation automation)
    {
        Stop();
        _automation = automation;
        _automationProperties = GetAutomationProperties(automation.Data);
        if (!_automation.IsSubAutomation)
        {
            Start();
        }
        return Task.CompletedTask;
    }
   
    public string? ExecuteScript(string script)
    {
        string? result = null;
        if (_engines.Any())
        {
            var autoResetEvent = new AutoResetEvent(false);
            Task.Run(() =>
            {
                lock (_lockEngineObject)
                {
                    try
                    {
                        var jsValue = _engines[0].Engine.Evaluate(script);
                        result = jsValue.JsValueToString();
                    }
                    catch (Exception e)
                    {
                        result = $"Error: {e.Message}";
                    }
                }
                autoResetEvent.Set();
            });
            autoResetEvent.WaitOne();
            autoResetEvent.Dispose();
        }
        return result;
    }
}
