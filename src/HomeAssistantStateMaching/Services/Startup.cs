using HomeAssistantStateMaching.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeAssistantStateMaching.Services;

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
