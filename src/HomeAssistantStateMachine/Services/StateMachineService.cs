using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace HomeAssistantStateMachine.Services;

public class StateMachineService : ServiceDbBase
{
    private ConcurrentDictionary<int, StateMachineHandler> _handlers = [];
    private readonly HAClientService _haClientService;

    private bool _started = false;

    public StateMachineService(IDbContextFactory<HasmDbContext> dbFactory, HAClientService haClientService, VariableService variableService) : base(dbFactory)
    {
        _haClientService = haClientService;
        variableService.VariableValueChanged += VariableService_VariableValueChanged; ;
    }

    private void VariableService_VariableValueChanged(object? sender, VariableValue e)
    {
        TriggerAllStateMachines();
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            _started = true;
            //load data and create state maching handlers
            await ExecuteOnDbContextAsync(null, async (context) =>
            {
                var sms = await context.StateMachines
                    .Include(sm => sm.States)
                    .Include(sm => sm.HAClients)
                    .Include(sm => sm.Transitions)
                    .ToListAsync();
                foreach (var sm in sms)
                {
                    _handlers.TryAdd(sm.Id, new StateMachineHandler(sm));
                }
                return true;
            });

            foreach (var _handler in _handlers.Values)
            {
                _handler.StateChanged += _handler_StateChanged;
                _handler.Start();
            }
        }
    }

    private void _handler_StateChanged(object? sender, State? e)
    {
        TriggerAllStateMachines();
    }

    private long _triggering = 0;
    private readonly object _triggerLock = new object();
    void TriggerAllStateMachines()
    {
        var count = Interlocked.Read(ref _triggering);
        if (count < 3)
        {
            Interlocked.Increment(ref _triggering);
            Task.Factory.StartNew(() =>
            {
                lock (_triggerLock)
                {
                    foreach (var handler in _handlers.Values.ToList())
                    {
                        handler.TriggerProcess();
                    }
                }
                Interlocked.Decrement(ref _triggering);
            });
        }
    }

    public async Task<StateMachineHandler?> CreateMachineStateAsync(StateMachine stateMachine, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            StateMachineHandler? result = null;
            if (await ExecuteWithinTransactionAsync(context, async () =>
            {
                await context.AddAsync(stateMachine);
                await context.SaveChangesAsync();
                result = new StateMachineHandler(stateMachine);
                _handlers.TryAdd(stateMachine.Id, result);
            }))
            {
                result!.StateChanged += _handler_StateChanged;
                result.Start();
            }
            return result;
        });
    }

    public List<StateMachineHandler> GetStateMachines()
    {
        return _handlers.Values.ToList();
    }

    public async Task<StateMachine> UpdateMachineStateAsync(StateMachine stateMachine, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            StateMachine result = null!;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                StateMachine result = await context.StateMachines
                        .Include(sm => sm.States)
                        .Include(sm => sm.HAClients)
                        .Include(sm => sm.Transitions)
                        .FirstAsync(x => x.Id == stateMachine.Id);

                foreach(var state in result.States.ToList())
                {
                    if (!stateMachine.States.Any(x => x.Id == state.Id))
                    {
                        result.States.Remove(state);
                    }
                }
                foreach (var transition in result.Transitions.ToList())
                {
                    if (!stateMachine.Transitions.Any(x => x.Id == transition.Id))
                    {
                        result.Transitions.Remove(transition);
                    }
                }

                foreach(var stateMachine in stateMachine.States)
                {
                    if (stateMachine.Id == 0)
                    {
                        result.States.Add(stateMachine);
                    }
                }
                foreach (var transition in stateMachine.Transitions)
                {
                    if (transition.Id == 0)
                    {
                        result.Transitions.Add(transition);
                    }
                }

                await context.SaveChangesAsync();
            });

            result = await context.StateMachines
                        .Include(sm => sm.States)
                        .Include(sm => sm.HAClients)
                        .Include(sm => sm.Transitions)
                        .FirstAsync(x => x.Id == stateMachine.Id);

            _handlers[stateMachine.Id]!.UpdateStateMachine(result);

            return result;
        });
    }
}
