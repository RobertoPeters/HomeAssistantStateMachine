using HomeAssistantStateMachine.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeAssistantStateMachine.Services;

public static class Startup
{
    public static void Init(IServiceProvider serviceProvider)
    {
        var dbFactory = serviceProvider.GetRequiredService<IDbContextFactory<HasmDbContext>>();
        using var context = dbFactory.CreateDbContext();
        context.Database.Migrate();

        //todo check contents (add missging)

        //now load state machine
        serviceProvider.GetRequiredService<StateMachineService>();
    }
}
