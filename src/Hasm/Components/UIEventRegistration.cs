using Hasm.Services;
using Hasm.Services.Automations.Flow;
using Hasm.Services.Automations.Script;
using Hasm.Services.Automations.StateMachine;
using Hasm.Services.Clients;
using Hasm.Services.Interfaces;

namespace Hasm.Components;

public class UIEventRegistration
{
    public event EventHandler<IClientHandler>? ClientHandlerChanged;
    public event EventHandler<IAutomationHandler>? AutomationHandlerChanged;
    public event EventHandler<LogEntry>? LogEntryAdded;
    public event EventHandler<StateMachineHandler.StateMachineHandlerInfo>? StateMachineHandlerInfoChanged;
    public event EventHandler<FlowHandler.FlowHandlerInfo>? FlowHandlerInfoChanged;
    public event EventHandler<ScriptHandler.ScriptHandlerInfo>? ScriptHandlerInfoChanged;
    public event EventHandler<List<VariableService.VariableInfo>>? VariablesChanged;
    public event EventHandler<List<VariableService.VariableValueInfo>>? VariableValuesChanged;
    public event EventHandler<ClientConnectionInfo>? ClientConnectionInfoChanged;
    public event EventHandler<AutomationInfo>? AutomationInfoChanged;

    public void Handle(IClientHandler clientHandler)
    {
        ClientHandlerChanged?.Invoke(this, clientHandler);
    }

    public void Handle(IAutomationHandler automationHandler)
    {
        AutomationHandlerChanged?.Invoke(this, automationHandler);
    }

    public void Handle(LogEntry logEntry)
    {
        LogEntryAdded?.Invoke(this, logEntry);
    }

    public void Handle(StateMachineHandler.StateMachineHandlerInfo stateMachineHandlerInfo)
    {
        StateMachineHandlerInfoChanged?.Invoke(this, stateMachineHandlerInfo);
    }

    public void Handle(FlowHandler.FlowHandlerInfo flowHandlerInfo)
    {
        FlowHandlerInfoChanged?.Invoke(this, flowHandlerInfo);
    }

    public void Handle(ScriptHandler.ScriptHandlerInfo scriptHandlerInfo)
    {
        ScriptHandlerInfoChanged?.Invoke(this, scriptHandlerInfo);
    }

    public void Handle(List<VariableService.VariableInfo> variables)
    {
        VariablesChanged?.Invoke(this, variables);
    }

    public void Handle(List<VariableService.VariableValueInfo> variableValues)
    {
        VariableValuesChanged?.Invoke(this, variableValues);
    }

    public void Handle(ClientConnectionInfo clientConnectionInfo)
    {
        ClientConnectionInfoChanged?.Invoke(this, clientConnectionInfo);
    }

    public void Handle(AutomationInfo automationInfo)
    {
        AutomationInfoChanged?.Invoke(this, automationInfo);
    }
}

public static class UIEventRegistrationMessageHandler
{
    public static void Handle(HAClientHandler clientHandler, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(clientHandler);
    }

    public static void Handle(StateMachineHandler stateMachineHandler, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(stateMachineHandler);
    }

    public static void Handle(FlowHandler flowHandler, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(flowHandler);
    }

    public static void Handle(ScriptHandler scriptHandler, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(scriptHandler);
    }

    public static void Handle(LogEntry logEntry, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(logEntry);
    }

    public static void Handle(StateMachineHandler.StateMachineHandlerInfo stateMachineHandlerInfo, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(stateMachineHandlerInfo);
    }

    public static void Handle(FlowHandler.FlowHandlerInfo flowHandlerInfo, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(flowHandlerInfo);
    }

    public static void Handle(VariableService.VariableInfo variable, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle([variable]);
    }

    public static void Handle(List<VariableService.VariableInfo> variables, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(variables);
    }

    public static void Handle(List<VariableService.VariableValueInfo> variableValues, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(variableValues);
    }

    public static void Handle(ClientConnectionInfo clientConnectionInfo, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(clientConnectionInfo);
    }

    public static void Handle(AutomationInfo automationInfo, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(automationInfo);
    }    
}
