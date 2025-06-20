using Hasm.Services;

namespace Hasm.Components;

public class UIEventRegistration
{
    public event EventHandler<IClientHandler>? ClientHandlerChanged;
    public event EventHandler<StateMachineHandler>? StateMachineHandlerChanged;
    public event EventHandler<StateMachineHandler.LogEntry>? LogEntryAdded;

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
}
