using Hasm.Services;

namespace Hasm.Components;

public class UIEventRegistration
{
    public event EventHandler<IClientHandler>? ClientHandlerChanged;
    public event EventHandler<StateMachineHandler>? StateMachineHandlerChanged;
    public event EventHandler<StateMachineHandler.LogEntry>? LogEntryAdded;
    public event EventHandler<StateMachineHandler.StateMachineHandlerInfo>? StateMachineHandlerInfoChanged;
    public event EventHandler<List<VariableService.VariableInfo>>? VariablesChanged;
    public event EventHandler<List<VariableService.VariableValueInfo>>? VariableValuesChanged;
    public event EventHandler<ClientConnectionInfo>? ClientConnectionInfoChanged;    

    public void Handle(IClientHandler clientHandler)
    {
        ClientHandlerChanged?.Invoke(this, clientHandler);
    }

    public void Handle(StateMachineHandler stateMachineHandler)
    {
        StateMachineHandlerChanged?.Invoke(this, stateMachineHandler);
    }

    public void Handle(StateMachineHandler.LogEntry logEntry)
    {
        LogEntryAdded?.Invoke(this, logEntry);
    }

    public void Handle(StateMachineHandler.StateMachineHandlerInfo stateMachineHandlerInfo)
    {
        StateMachineHandlerInfoChanged?.Invoke(this, stateMachineHandlerInfo);
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
}

public class UIEventRegistrationMessageHandler
{
    public void Handle(HAClientHandler clientHandler, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(clientHandler);
    }

    public void Handle(StateMachineHandler stateMachineHandler, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(stateMachineHandler);
    }

    public void Handle(StateMachineHandler.LogEntry logEntry, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(logEntry);
    }

    public void Handle(StateMachineHandler.StateMachineHandlerInfo stateMachineHandlerInfo, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(stateMachineHandlerInfo);
    }

    public void Handle(VariableService.VariableInfo variable, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle([variable]);
    }

    public void Handle(List<VariableService.VariableInfo> variables, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(variables);
    }

    public void Handle(List<VariableService.VariableValueInfo> variableValues, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(variableValues);
    }

    public void Handle(ClientConnectionInfo clientConnectionInfo, UIEventRegistration uiEventRegistration)
    {
        uiEventRegistration.Handle(clientConnectionInfo);
    }
    

}
