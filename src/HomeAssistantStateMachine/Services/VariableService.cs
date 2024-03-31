using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class VariableService : ServiceDbBase
{
    private ConcurrentDictionary<int, Variable> _variables = [];
    private ConcurrentDictionary<int, VariableValue> _variableValues = [];
 
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
                    _variables.TryAdd(variable.Id, variable);
                }
                var variableValues = await context.VariableValues
                    .Include(v => v.Variable)
                    .ToListAsync();
                foreach (var variableValue in variableValues)
                {
                    _variableValues.TryAdd(variableValue.Variable!.Id, variableValue);
                }
                return true;
            });
        }
    }

    public async Task<Variable?> CreateVariableAsync(string name, string? data, HAClient? haClient, StateMachine? stateMachine, State? state, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            Variable? result = null;
            await ExecuteWithinTransactionAsync(context, async () =>
            {
                result = new Variable
                {
                    Name = name,
                    Data = data,
                    HAClientId = haClient?.Id,
                    StateMachineId = stateMachine?.Id,
                    StateId = state?.Id
                };
                await context.Variables.AddAsync(result);
                await context.SaveChangesAsync();

                _variables.TryAdd(result.Id, result);
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
                var v = variableValue.Variable;
                variableValue.Variable = null;
                variableValue.VariableId = v.Id;
                variableValue.Update = DateTime.UtcNow;
                await context.AddAsync(variableValue);
                await context.SaveChangesAsync();
                variableValue.Variable = v;
                _variableValues.TryAdd(variableValue.Variable!.Id, variableValue);
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
            _variableValues.TryGetValue(variable.Id, out var variableValue);
            result.Add((variable, variableValue));
        }

        return result;
    }

    public List<(Variable variable, VariableValue? variableValue)> GetScopedVariables(HAClient? haClient, StateMachine? stateMachine, State? state)
    {
        List<(Variable variable, VariableValue? variableValue)> result = [];

        var allVariables = _variables.Values
            .Where(x => haClient?.Id == x.HAClient?.Id && stateMachine?.Id == x.StateMachine?.Id && state?.Id == x.State?.Id)
            .ToList();

        foreach(var variable in allVariables)
        {
            _variableValues.TryGetValue(variable.Id, out var variableValue);
            result.Add((variable, variableValue));
        }

        return result;
    }
}
