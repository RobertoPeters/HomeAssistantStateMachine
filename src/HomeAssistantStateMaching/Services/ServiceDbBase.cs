using HomeAssistantStateMaching.Data;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace HomeAssistantStateMaching.Services;

public class ServiceDbBase
{
    private readonly IDbContextFactory<HasmDbContext> _contextFactory;
 
    public ServiceDbBase(IDbContextFactory<HasmDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private HasmDbContext GetDbContext()
    {
        var context = _contextFactory.CreateDbContext();
        return context;
    }

    private async Task<HasmDbContext> GetDbContextAsync()
    {
        var context = await _contextFactory.CreateDbContextAsync();
        return context;
    }

    protected bool ExecuteWithinTransaction(HasmDbContext context, Action dbAction, bool throwException = false)
    {
        var result = true;

        var transactionId = context.BeginTransaction();
        try
        {
            dbAction();
            context.EndTransaction(transactionId, false);
        }
        catch
        {
            result = false;
            context.EndTransaction(transactionId, true);
            //in case of nested transaction, we should throw an exception anyway preventing continuing the outer transaction
            if (throwException || transactionId > 1)
            {
                throw;
            }
        }
        return result;
    }

    protected async Task<bool> ExecuteOnDbContextWithinTransactionAsync(HasmDbContext? ctx, Func<HasmDbContext, Task> dbAction, bool throwException = true)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            return await ExecuteWithinTransactionAsync(context, async () => await dbAction(context), throwException);
        });
    }

    protected bool ExecuteOnDbContextWithinTransaction(HasmDbContext? ctx, Action<HasmDbContext> dbAction, bool throwException = true)
    {
        return ExecuteOnDbContext(ctx, (context) =>
        {
            return ExecuteWithinTransaction(context, () => dbAction(context), throwException);
        });
    }

    protected async Task<bool> ExecuteWithinTransactionAsync(HasmDbContext context, Func<Task> dbAction, bool throwException = false)
    {
        var result = true;
        var transactionId = await context.BeginTransactionAsync();
        try
        {
            await dbAction();
            await context.EndTransactionAsync(transactionId, false);
        }
        catch
        {
            result = false;
            await context.EndTransactionAsync(transactionId, true);
            //in case of nested transaction, we should throw an exception anyway preventing continuing the outer transaction
            if (throwException || transactionId > 1)
            {
                throw;
            }
        }
        return result;
    }

    protected TResult ExecuteOnDbContext<TResult>(HasmDbContext? context, Func<HasmDbContext, TResult> dbAction, [CallerMemberName] string name = "")
    {
        TResult? result;
        if (context == null)
        {
            using var localContext = GetDbContext();
            result = dbAction(localContext);
         }
        else
        {
            result = dbAction(context);
        }
        return result;
    }

    protected async Task<TResult> ExecuteOnDbContextAsync<TResult>(HasmDbContext? context, Func<HasmDbContext, Task<TResult>> dbAction, [CallerMemberName] string name = "")
    {
        TResult? result;
        if (context == null)
        {
            using var localContext = await GetDbContextAsync();
            result = await dbAction(localContext);
        }
        else
        {
            result = await dbAction(context);
        }
        return result;
    }
}
