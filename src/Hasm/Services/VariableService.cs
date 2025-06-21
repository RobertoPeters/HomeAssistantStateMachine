using System.Collections.Concurrent;
using Hasm.Models;


namespace Hasm.Services;

public class VariableService(DataService _dataService, MessageBusService _messageBusService)
{
    public class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue VariableValue { get; set; } = null!;
        public bool IsMocking { get; set; } = false;
        public VariableValue? MockingValue { get; set; } = null!;
        public string? Value => IsMocking ? MockingValue?.Value : VariableValue.Value;
    }

    private ConcurrentDictionary<int, VariableInfo> _variables = [];

    public Task StartAsync()
    {
        var variables = _dataService.GetVariables();
        var variableValues = _dataService.GetVariableValues().ToDictionary(x => x.VariableId, x => x);
        foreach (var variable in variables)
        {
            VariableValue? variableValue = null;
            if (variableValues.TryGetValue(variable.Id, out var value))
            {
                variableValue = value;
            }
            else
            {
                variableValue = new VariableValue
                {
                    Update = DateTime.UtcNow,
                    VariableId = variable.Id,
                    Value = null
                };
            }
            var varInfo = new VariableInfo
            {
                Variable = variable,
                VariableValue = variableValue
            };
            _variables.TryAdd(variable.Id, varInfo);
        }
        return Task.CompletedTask;
    }

    public List<VariableInfo> GetVariables()
    {
        return _variables.Values.ToList();
    }

    public VariableInfo? GetVariable(int variableId)
    {
        _variables.TryGetValue(variableId, out var variableInfo);
        return variableInfo;
    }

    public async Task<bool> SetVariableValueAsync(int variableId, string? value)
    {
        if (_variables.TryGetValue(variableId, out var variableInfo))
        {
            if (string.Compare(value, variableInfo.VariableValue.Value) == 0)
            {
                return false;
            }
            variableInfo.VariableValue.Value = value;
            variableInfo.VariableValue.Update = DateTime.UtcNow;
            await _dataService.AddOrUpdateVariableValueAsync(variableInfo.VariableValue);
            await _messageBusService.PublishAsync(variableInfo);
            return true;
        }
        return false;
    }

    public async Task DeleteVariableAsync(int variableId)
    {
        await DeleteVariablesAsync([variableId], true);
    }

    public async Task<int?> CreateVariableAsync(string name, int clientId, int? stateMachineId, bool persistant, string? data, List<string>? mockingOptions)
    {
        if (_dataService.GetClients().FirstOrDefault(x => x.Id == clientId) == null)
        {
            return null;
        }
        var variableInfo = _variables.Values.FirstOrDefault(x => x.Variable.Name == name
        && x.Variable.StateMachineId == stateMachineId
        && clientId == x.Variable.ClientId
        );

        if (variableInfo != null
            && string.Compare(data, variableInfo.Variable.Data) == 0
            && persistant == variableInfo.Variable.Persistant)
        {
            return variableInfo.Variable.Id;
        }

        var variable = variableInfo?.Variable;
        var variableValue = variableInfo?.VariableValue;

        if (variable == null)
        {
            variable = new()
            {
                ClientId = clientId,
                Name = name,
                StateMachineId = stateMachineId,
                Persistant = persistant
            };
        }
        if (variableValue != null)
        {
            await _dataService.DeleteVariableValueAsync(variableValue);
        }

        variable.Data = data;
        variable.MockingValues = mockingOptions;
        await _dataService.AddOrUpdateVariableAsync(variable);
        await AddOrUpdateVariableAsync(variable);
        return variable.Id;
    }

    public async Task Handle(Client client)
    {
        if (client.Id < 0)
        {
            await DeleteVariablesAsync(_variables.Values.Where(x => x.Variable.ClientId == -client.Id).Select(x => x.Variable.Id).ToList(), false);
        }
    }

    public async Task Handle(StateMachine stateMachine)
    {
        if (stateMachine.Id < 0)
        {
            await DeleteVariablesAsync(_variables.Values.Where(x => x.Variable.StateMachineId == -stateMachine.Id).Select(x => x.Variable.Id).ToList(), false);
        }
    }

    private async Task AddOrUpdateVariableAsync(Variable variable)
    {
        var variableInfo = new VariableInfo()
        {
            Variable = variable,
            VariableValue = _dataService.GetVariableValues().FirstOrDefault(x => x.VariableId == variable.Id) ?? new VariableValue
            {
                Update = DateTime.UtcNow,
                VariableId = variable.Id,
                Value = null
            }
        };
        if (!_variables.TryGetValue(variable.Id, out _))
        {
            _variables.TryAdd(variable.Id, variableInfo);
        }
        else
        {
            _variables.AddOrUpdate(variable.Id, variableInfo, (_, _) => variableInfo);
        }
        await _dataService.AddOrUpdateVariableValueAsync(variableInfo.VariableValue);
        await _messageBusService.PublishAsync(variableInfo);
    }

    private async Task DeleteVariablesAsync(List<int> ids, bool deleteFromRepository)
    {
        List<VariableInfo> removedVariables = [];
        foreach (var id in ids)
        {
            if (_variables.TryRemove(id, out var variableInfo))
            {
                await _dataService.DeleteVariableAsync(variableInfo.Variable);
                variableInfo.Variable.Id = -id;
                removedVariables.Add(variableInfo);
            }
        }
        if (removedVariables.Any())
        {
            await _messageBusService.PublishAsync(removedVariables);
        }
    }
}

public class VariableServiceMessageHandler
{
    public async Task Handle(Client client, VariableService variableService)
    {
        await variableService.Handle(client);
    }
    public async Task Handle(StateMachine stateMachine, VariableService variableService)
    {
        await variableService.Handle(stateMachine);
    }
}