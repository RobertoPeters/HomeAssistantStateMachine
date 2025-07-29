using System.Collections.Concurrent;
using Hasm.Models;
using Hasm.Services.Automations;
using Hasm.Services.Interfaces;

namespace Hasm.Services;

public class AutomationService(DataService _dataService, ClientService _clientService, VariableService _variableService, MessageBusService _messageBusService)
{
    private readonly ConcurrentDictionary<int, IAutomationHandler> _handlers = [];
    private Timer? _slowTriggerTimer;

    public Task StartAsync()
    {
        var automations = _dataService.GetAutomations();
        foreach(var automation in automations)
        {
            AddAutomation(automation);
        }
        _slowTriggerTimer = new Timer((state) =>
        {
            foreach (var item in _handlers)
            {
                try
                {
                    item.Value.TriggerProcess();
                }
                catch 
                {
                    //nothing
                }
            }
            _slowTriggerTimer?.Change(3000, Timeout.Infinite);
        }, null, 5000, Timeout.Infinite);

        return Task.CompletedTask;
    }

    public List<IAutomationHandler> GetAutomations()
    {
        return _handlers.Values.ToList();
    }

    public IAutomationHandler GetAutomation(int id)
    {
        return _handlers[id];
    }

    public async Task Handle(List<VariableService.VariableInfo> variableInfos)
    {
        foreach(var automationHandler in _handlers.Values.ToList())
        {
            await automationHandler.Handle(variableInfos);
        }
    }

    public async Task Handle(List<VariableService.VariableValueInfo> variableValueInfos)
    {
        foreach (var automationHandler in _handlers.Values.ToList())
        {
            await automationHandler.Handle(variableValueInfos);
        }
    }

    public async Task Handle(Automation automation)
    {
        IAutomationHandler? automationHandler = null;
        if (automation.Id < 0)
        {
            automationHandler = RemoveAutomationHandler(-automation.Id);
            if (automationHandler != null)
            {
                automationHandler.Automation.Id = automation.Id;
            }
        }
        else if (_handlers.TryGetValue(automation.Id, out automationHandler))
        {
            await automationHandler.UpdateAsync(automation);
        }
        else
        {
            automationHandler = AddAutomation(automation);
        }

        if (automationHandler != null)
        {
             await _messageBusService.PublishAsync(automationHandler!);
        }
    }

    private IAutomationHandler? RemoveAutomationHandler(int id)
    {
        IAutomationHandler? automationHandler = null;
        if (_handlers.TryRemove(id, out automationHandler))
        {
            automationHandler.Dispose();
        }
        return automationHandler;
    }

    private IAutomationHandler? AddAutomation(Automation automation)
    {
        IAutomationHandler? automationHandler = null;
        switch (automation.AutomationType)
        {
            case Models.AutomationType.StateMachine:
                automationHandler = new StateMachineHandler(automation, _clientService, _dataService, _variableService, _messageBusService);
                break;
            case Models.AutomationType.Flow:
                automationHandler = new FlowHandler(automation, _clientService, _dataService, _variableService, _messageBusService);
                break;
        }
        if (automationHandler != null)
        {
            if (!_handlers.TryAdd(automation.Id, automationHandler))
            {
                automationHandler = null;
            }
            if (automationHandler != null && !automationHandler.Automation.IsSubAutomation)
            {
                automationHandler.Start();
            }
        }
        return automationHandler;
    }
}

public static class AutomationServiceMessageHandler
{
    public static async Task Handle(Automation automation, AutomationService automationService)
    {
        await automationService.Handle(automation);
    }

    public static async Task Handle(VariableService.VariableInfo variableInfo, AutomationService automationService)
    {
        await automationService.Handle([variableInfo]);
    }

    public static async Task Handle(List<VariableService.VariableInfo> variableInfos, AutomationService automationService)
    {
        await automationService.Handle(variableInfos);
    }

    public static async Task Handle(List<VariableService.VariableValueInfo> variableValueInfos, AutomationService automationService)
    {
        await automationService.Handle(variableValueInfos);
    }
}
