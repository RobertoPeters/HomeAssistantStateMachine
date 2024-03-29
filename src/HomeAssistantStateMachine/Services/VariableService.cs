using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class VariableService : ServiceDbBase
{
    private ConcurrentDictionary<Guid, Variable> _variables = [];
    private ConcurrentDictionary<Guid, VariableValue> _variableValues = [];
 
    private bool _started = false;

    public event EventHandler<VariableValue>? VariableValueChanged;

    public VariableService(IDbContextFactory<HasmDbContext> dbFactory) : base(dbFactory)
    {
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            _started = true;
            await ExecuteOnDbContextAsync(null, async (context) =>
            {
                var variables = await context.Variables
                    .Include(v => v.HAClient)
                    .Include(v => v.StateMachine)
                    .Include(v => v.State)
                    .ToListAsync();
                foreach (var variable in variables)
                {
                    _variables.TryAdd(variable.Handle, variable);
                }
                var variableValues = await context.VariableValues
                    .Include(v => v.Variable)
                    .ToListAsync();
                foreach (var variableValue in variableValues)
                {
                    _variableValues.TryAdd(variableValue.Variable.Handle, variableValue);
                }
                return true;
            });
        }
    }

    public async Task<Variable?> CreateVariableAsync(Guid handle, string name, string? data, HAClient? haClient, StateMachine? stateMachine, State? state, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            Variable? result = null;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                result = new Variable
                {
                    Handle = handle,
                    Name = name,
                    Data = data,
                    HAClient = haClient,
                    StateMachine = stateMachine,
                    State = state
                };
                await context.AddAsync(result);
                await context.SaveChangesAsync();
                _variables.TryAdd(handle, result);
            });
            return result;
        });
    }

    public async Task<bool> CreateVariableValueAsync(VariableValue variableValue, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            return await ExecuteWithinTransactionAsync(context, async () =>
            {
                variableValue.Update = DateTime.UtcNow;
                await context.AddAsync(variableValue);
                await context.SaveChangesAsync();
                _variableValues.TryAdd(variableValue.Variable.Handle, variableValue);
                VariableValueChanged?.Invoke(this, variableValue);
            });
         });
    }

    public async Task<bool> UpdateVariableValueAsync(VariableValue variableValue, string? newValue, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            return await ExecuteWithinTransactionAsync(context, async () =>
            {
                if (variableValue.Value != newValue)
                {
                    context.VariableValues.Attach(variableValue);
                    variableValue.Value = newValue;
                    variableValue.Update = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    VariableValueChanged?.Invoke(this, variableValue);
                }
            });
        });
    }

    public List<(Variable variable, VariableValue? variableValue)> GetVariables()
    {
        List<(Variable variable, VariableValue? variableValue)> result = [];
        var allVariables = _variables.Values.ToList();

        foreach (var variable in allVariables)
        {
            _variableValues.TryGetValue(variable.Handle, out var variableValue);
            result.Add((variable, variableValue));
        }

        return result;
    }

    public List<(Variable variable, VariableValue? variableValue)> GetScopedVariables(HAClient? haClient, StateMachine? stateMachine, State? state)
    {
        List<(Variable variable, VariableValue? variableValue)> result = [];

        var allVariables = _variables.Values
            .Where(x => haClient?.Handle == x.HAClient?.Handle && stateMachine?.Handle == x.StateMachine?.Handle && state?.Handle == x.State?.Handle)
            .ToList();

        foreach(var variable in allVariables)
        {
            _variableValues.TryGetValue(variable.Handle, out var variableValue);
            result.Add((variable, variableValue));
        }

        return result;
    }
}
