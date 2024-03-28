using HomeAssistantStateMaching.Data;
using HomeAssistantStateMaching.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace HomeAssistantStateMaching.Services;

public class StateMachineService : ServiceDbBase
{
    private ConcurrentDictionary<Guid, StateMachineHandler> _handlers = [];
    private readonly HAClientService _haClientService;

    public StateMachineService(IDbContextFactory<HasmDbContext> dbFactory, HAClientService haClientService) : base(dbFactory)
    {
        _haClientService = haClientService;

        //load data and create state maching handlers
        ExecuteOnDbContext(null, (context) =>
        {
            var sms = context.StateMachines.ToList();
            foreach (var sm in sms)
            {
                _handlers.TryAdd(sm.Handle, new StateMachineHandler(this, sm));
            }
            return true;
        });
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
                    Handle = handle,
                    Name = name,
                    Enabled = enabled
                };
                await context.AddAsync(sm);
                await context.SaveChangesAsync();
                result = new StateMachineHandler(this, sm);
                _handlers.TryAdd(handle, result);
            });
            return result;
        });
    }
}
