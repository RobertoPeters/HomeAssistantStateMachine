using Microsoft.EntityFrameworkCore;

namespace HomeAssistantStateMachine.Data;

public class HasmDbContext: DbContext
{
    private int _transactionDepth { get; set; } = 0;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _activeTransaction = null;

    public HasmDbContext(DbContextOptions<HasmDbContext> options) : base(options)
    {
    }

    public override void Dispose()
    {
        _activeTransaction?.Dispose();
        _activeTransaction = null;
        _transactionDepth = 0;
        base.Dispose();
    }

    public int BeginTransaction()
    {
        _transactionDepth++;
        //only open transaction if first call
        //check on _activeTransaction == null should not be necessary, but it should be null anyway
        if (_transactionDepth == 1 && _activeTransaction == null)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            _activeTransaction = Database.BeginTransaction();
        }
        return _transactionDepth;
    }

    public async Task<int> BeginTransactionAsync()
    {
        _transactionDepth++;
        //only open transaction if first call
        //check on _activeTransaction == null should not be necessary, but it should be null anyway
        if (_transactionDepth == 1 && _activeTransaction == null)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            _activeTransaction = await Database.BeginTransactionAsync();
        }
        return _transactionDepth;
    }

    public void EndTransaction(int transactionId, bool rollback)
    {
        if (transactionId == _transactionDepth)
        {
            _transactionDepth--;
            //if there is no transaction, there is nothing todo anyway ;-)
            if (_activeTransaction != null)
            {
                if (rollback)
                {
                    _activeTransaction.Rollback();
                    _activeTransaction.Dispose();
                    _activeTransaction = null;
                    ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                }
                else if (_transactionDepth == 0)
                {
                    _activeTransaction.Commit();
                    _activeTransaction.Dispose();
                    _activeTransaction = null;
                    ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                }
            }
        }
    }

    public async Task EndTransactionAsync(int transactionId, bool rollback)
    {
        if (transactionId == _transactionDepth)
        {
            _transactionDepth--;
            //if there is no transaction, there is nothing todo anyway ;-)
            if (_activeTransaction != null)
            {
                if (rollback)
                {
                    await _activeTransaction.RollbackAsync();
                    await _activeTransaction.DisposeAsync();
                    _activeTransaction = null;
                    ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                }
                else if (_transactionDepth == 0)
                {
                    await _activeTransaction.CommitAsync();
                    await _activeTransaction.DisposeAsync();
                    _activeTransaction = null;
                    ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                }
            }
        }
    }

    public DbSet<Models.HAClient> HAClients { get; set; } = null!;
    public DbSet<Models.State> States { get; set; } = null!;
    public DbSet<Models.Transition> Transitions { get; set; } = null!;
    public DbSet<Models.StateMachine> StateMachines { get; set; } = null!;
    public DbSet<Models.Variable> Variables { get; set; } = null!;
    public DbSet<Models.VariableValue> VariableValues { get; set; } = null!;

}
