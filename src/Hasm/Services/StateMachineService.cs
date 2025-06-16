using System.Collections.Concurrent;
using System.Collections.Generic;
using Hasm.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Hasm.Services;

public class StateMachineService(DataService _dataService, MessageBusService _messageBusService)
{
    private ConcurrentDictionary<int, StateMachineHandler> _handlers = [];

    public Task StartAsync()
    {
        var stateMachines = _dataService.GetStateMachines();
        foreach(var stateMachine in stateMachines)
        {
            AddStateMachine(stateMachine);
        }
        return Task.CompletedTask;
    }

    public List<StateMachineHandler> GetStateMachines()
    {
        return _handlers.Values.ToList();
    }

    public async Task Handle(StateMachine stateMachine)
    {
        StateMachineHandler? stateMachineHandler = null;
        if (stateMachine.Id < 0)
        {
            stateMachineHandler = RemoveStateMachineHandler(-stateMachine.Id);
            if (stateMachineHandler != null)
            {
                stateMachineHandler.StateMachine.Id = stateMachine.Id;
            }
        }
        else if (_handlers.TryGetValue(stateMachine.Id, out stateMachineHandler))
        {
            await stateMachineHandler.UpdateAsync(stateMachine);
        }
        else
        {
            stateMachineHandler = AddStateMachine(stateMachine);
        }

        if (stateMachineHandler != null)
        {
             await _messageBusService.SendAsync(stateMachineHandler!);
        }
    }

    private StateMachineHandler? RemoveStateMachineHandler(int id)
    {
        StateMachineHandler? stateMachineHandler = null;
        if (_handlers.TryRemove(id, out stateMachineHandler))
        {
            stateMachineHandler.Dispose();
        }
        return stateMachineHandler;
    }

    private StateMachineHandler? AddStateMachine(StateMachine stateMachine)
    {
        StateMachineHandler? stateMachineHandler = null;
        stateMachineHandler = new StateMachineHandler(stateMachine, _messageBusService);
        if (!_handlers.TryAdd(stateMachine.Id, stateMachineHandler))
        {
            stateMachineHandler = null;
        }
        stateMachineHandler?.Start();
        return stateMachineHandler;
    }
}

public class StateMachineServiceMessageHandler
{
    public async Task Handle(StateMachine stateMachine, StateMachineService stateMachineService)
    {
        await stateMachineService.Handle(stateMachine);
    }
}
