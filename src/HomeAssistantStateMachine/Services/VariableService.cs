using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HomeAssistantStateMachine.Services;

public partial class VariableService : ServiceDbBase
{
    private ConcurrentDictionary<int, Variable> _variables = [];
    private ConcurrentDictionary<int, VariableValue> _variableValues = [];
    private ConcurrentDictionary<string, int> _variableNameToString = [];

    private bool _started = false;

    public event EventHandler<VariableValue>? VariableValueChanged;
    public event EventHandler<EventArgs>? VariableCollectionChanged;

    public VariableService(IDbContextFactory<HasmDbContext> dbFactory) : base(dbFactory)
    {
    }

    public object? GetVariableValue(string name)
    {
        if (_variableNameToString.TryGetValue(name, out var id) && _variableValues.TryGetValue(id, out var variableValue))
        {
            return variableValue.Value;
        }
        return null;
    }

    public Variable? GetVariable(string name)
    {
        if (_variableNameToString.TryGetValue(name, out var id) && _variables.TryGetValue(id, out var variable))
        {
            return variable;
        }
        return null;
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
                    .Include(v => v.MqttClient)
                    .Include(v => v.StateMachine)
                    .Include(v => v.State)
                    .ToListAsync();
                foreach (var variable in variables)
                {
                    _variables.TryAdd(variable.Id, variable);
                    _variableNameToString.TryAdd(variable.Name, variable.Id);
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

    public async Task DeleteVariableAsync(string name, HasmDbContext? ctx = null)
    {
        if (await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            return await ExecuteWithinTransactionAsync(context, async () =>
            {
                if (_variableNameToString.TryGetValue(name, out var id))
                {
                    var variable = await context.Variables.FirstAsync(x => x.Id == id);
                    var variableValue = await context.VariableValues.FirstOrDefaultAsync(x => x.Id == id);
                    if (variableValue != null)
                    {
                        context.Remove(variableValue);
                    }
                    context.Remove(variable);
                    await context.SaveChangesAsync();

                    _variables.TryRemove(id, out var _);
                    if (variableValue != null)
                    {
                        _variableValues.TryRemove(id, out var _);
                    }

                    _variableNameToString.TryRemove(name, out var _);
                }
            });
        }))
        {
            VariableCollectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task DeleteVariablesAsync(int? haClientId, int? stateMachineId, int? stateId, HasmDbContext? ctx = null)
    {
        if (await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            return await ExecuteWithinTransactionAsync(context, async () =>
            {
                var variables = await context.Variables
                    .Where(x => x.HAClientId == haClientId && x.StateMachineId == stateMachineId && x.StateId == stateId)
                    .ToListAsync();
                foreach (var variable in variables)
                {
                    var id = variable.Id;
                    var variableValue = await context.VariableValues.FirstOrDefaultAsync(x => x.Id == id);
                    if (variableValue != null)
                    {
                        context.Remove(variableValue);
                    }
                    context.Remove(variable);
                    await context.SaveChangesAsync();

                    _variables.TryRemove(id, out var _);
                    if (variableValue != null)
                    {
                        _variableValues.TryRemove(id, out var _);
                    }

                    _variableNameToString.TryRemove(variable.Name, out var _);
                }
            });
        }))
        {
            VariableCollectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<Variable?> CreateVariableAsync(string name, string? data, HAClient? haClient, MqttClient? mqttClient, StateMachine? stateMachine, State? state, HasmDbContext? ctx = null)
    {
        return await CreateVariableAsync(name, data, haClient?.Id, mqttClient?.Id, stateMachine?.Id, state?.Id, ctx);
    }

    public async Task<Variable?> CreateVariableAsync(string name, string? data, int? haClientId, int? mqttClientId, int? stateMachineId, int? stateId, HasmDbContext? ctx = null)
    {
        return await ExecuteOnDbContextAsync(ctx, async (context) =>
        {
            Variable? result = _variables.Values.FirstOrDefault(x => x.Name == name);
            if (result != null)
            {
                if (result.HAClientId != haClientId || result.MqttClientId != mqttClientId || result.StateMachineId != stateMachineId || result.StateId != stateId || result.Data != data)
                {
                    return null;
                }
                return result;
            }
            else
            {
                await ExecuteWithinTransactionAsync(context, async () =>
                {
                    result = new Variable
                    {
                        Name = name,
                        Data = data,
                        HAClientId = haClientId,
                        MqttClientId = mqttClientId,
                        StateMachineId = stateMachineId,
                        StateId = stateId
                    };
                    await context.Variables.AddAsync(result);
                    await context.SaveChangesAsync();

                    _variables.TryAdd(result.Id, result);
                    _variableNameToString.TryAdd(result.Name, result.Id);
                });
            }
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

    public async Task<bool> UpdateVariableValueAsync(string name, string? newValue, HasmDbContext? ctx = null)
    {
        var result = true;
        if (_variableNameToString.TryGetValue(name, out var id))
        {
            if (_variableValues.TryGetValue(id, out var variableValue))
            {
                if (variableValue.Value != newValue)
                {
                    result = await UpdateVariableValueAsync(variableValue, newValue, ctx);
                }
            }
            else
            {
                variableValue = new VariableValue
                {
                    Value = newValue,
                    Variable = _variables[id],
                    VariableId = id
                };
                result = await CreateVariableValueAsync(variableValue, ctx);
            }
        }
        else
        {
            result = false;
        }
        return result;
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

    public List<(Variable variable, VariableValue? variableValue)> GetScopedVariables(MqttClient? mqttClient, HAClient? haClient, StateMachine? stateMachine, State? state)
    {
        List<(Variable variable, VariableValue? variableValue)> result = [];

        var allVariables = _variables.Values
            .Where(x => mqttClient?.Id == x.MqttClient?.Id && haClient?.Id == x.HAClient?.Id && stateMachine?.Id == x.StateMachine?.Id && state?.Id == x.State?.Id)
            .ToList();

        foreach (var variable in allVariables)
        {
            _variableValues.TryGetValue(variable.Id, out var variableValue);
            result.Add((variable, variableValue));
        }

        return result;
    }
}
