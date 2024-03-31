using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class StateMachineService : ServiceDbBase
{
    private ConcurrentDictionary<int, StateMachineHandler> _handlers = [];
    private readonly HAClientService _haClientService;

    private bool _started = false;

    public StateMachineService(IDbContextFactory<HasmDbContext> dbFactory, HAClientService haClientService) : base(dbFactory)
    {
        _haClientService = haClientService;
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            _started = true;
            //load data and create state maching handlers
            await ExecuteOnDbContextAsync(null, async (context) =>
            {
                var sms = await context.StateMachines.ToListAsync();
                foreach (var sm in sms)
                {
                    _handlers.TryAdd(sm.Id, new StateMachineHandler(this, sm));
                }
                return true;
            });
        }
    }

    public async Task<StateMachineHandler?> CreateMachineStateAsync(Guid handle, string name, bool enabled, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            StateMachineHandler? result = null;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                var sm = new StateMachine
                {
                    Name = name,
                    Enabled = enabled
                };
                await context.AddAsync(sm);
                await context.SaveChangesAsync();
                result = new StateMachineHandler(this, sm);
                _handlers.TryAdd(sm.Id, result);
            });
            return result;
        });
    }
}
