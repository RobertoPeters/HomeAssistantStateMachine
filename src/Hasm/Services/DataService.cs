using System.Collections.Concurrent;
using Hasm.Models;
using Wolverine;

namespace Hasm.Services;

public class DataService(Repository.DataRepository _dataRepository, MessageBusService _messageBusService)
{
    private readonly ConcurrentDictionary<int, Client> _clients = [];
    private readonly ConcurrentDictionary<int, Variable> _variables = [];
    private readonly ConcurrentDictionary<int, VariableValue> _variableValues = [];
    private readonly ConcurrentDictionary<int, StateMachine> _stateMachines = [];

    private int _lastUsedNonPersistentVariableValueId = int.MaxValue;

    public async Task StartAsync()
    {
        await _dataRepository.SetupAsync();

        var clients = await _dataRepository.GetClientsAsync();
        foreach (var client in clients)
        {
            _clients.TryAdd(client.Id, client);
        }

        var variables = await _dataRepository.GetVariablesAsync();
        foreach (var variable in variables)
        {
            _variables.TryAdd(variable.Id, variable);
        }

        var variableValues = await _dataRepository.GetVariableValuesAsync();
        foreach (var variableValue in variableValues)
        {
            if (_variables.ContainsKey(variableValue.VariableId))
            {
                _variableValues.TryAdd(variableValue.Id, variableValue);
            }
            else
            {
                await _dataRepository.DeleteVariableValueAsync(variableValue);
            }
        }

        var stateMachines = await _dataRepository.GetStateMachinesAsync();
        foreach (var stateMachine in stateMachines)
        {
            _stateMachines.TryAdd(stateMachine.Id, stateMachine);
        }
    }

    public List<Client> GetClients()
    {
        return _clients.Values.ToList();
    }

    public List<Variable> GetVariables()
    {
        return _variables.Values.ToList();
    }

    public List<VariableValue> GetVariableValues()
    {
        return _variableValues.Values.ToList();
    }

    public List<StateMachine> GetStateMachines()
    {
        return _stateMachines.Values.ToList();
    }

    public async Task AddOrUpdateClientAsync(Client client)
    {
        if (client.Id == 0)
        {
            await _dataRepository.AddClientAsync(client);
        }
        else
        {
            await _dataRepository.UpdateClientAsync(client);
        }
        _clients.AddOrUpdate(client.Id, client, (_, _) => client);
        await _messageBusService.PublishAsync(client);
    }

    public async Task AddOrUpdateVariableAsync(Variable variable)
    {
        if (variable.Id == 0)
        {
            await _dataRepository.AddVariableAsync(variable);
        }
        else
        {
            await _dataRepository.UpdateVariableAsync(variable);
        }
        _variables.AddOrUpdate(variable.Id, variable, (_, _) => variable);
        await _messageBusService.PublishAsync(variable);
    }

    public async Task AddOrUpdateVariableValueAsync(VariableValue variableValue)
    {
        if (variableValue.Id == 0)
        {
            if (_variables.TryGetValue(variableValue.VariableId, out var variable))
            {
                if (variable.Persistant)
                {
                    await _dataRepository.AddVariableValueAsync(variableValue);
                }
                else
                {
                    var newId = Interlocked.Decrement(ref _lastUsedNonPersistentVariableValueId);
                    variableValue.Id = newId;
                }
            }
        }
        else if (_variables.TryGetValue(variableValue.Id, out var variable) && variable.Persistant)
        {
            await _dataRepository.UpdateVariableValueAsync(variableValue);
        }
        _variableValues.AddOrUpdate(variableValue.Id, variableValue, (_, _) => variableValue);
        await _messageBusService.PublishAsync(variableValue);
    }

    public async Task DeleteVariableAsync(Variable variable)
    {
        if (_variables.TryRemove(Math.Abs(variable.Id), out var orgVariable))
        {
            _variableValues.TryRemove(orgVariable.Id, out var variableValue);

            if (variableValue != null)
            {
                await _dataRepository.DeleteVariableValueAsync(variableValue);
            }
            await _dataRepository.DeleteVariableAsync(orgVariable);
            orgVariable.Id = -Math.Abs(orgVariable.Id);
            await _messageBusService.PublishAsync(orgVariable);
        }
    }

    public async Task DeleteVariableValueAsync(VariableValue variableValue)
    {
        if (_variableValues.TryRemove(Math.Abs(variableValue.Id), out var orgVariableValue))
        {
            await _dataRepository.DeleteVariableValueAsync(variableValue);
            orgVariableValue.Id = -Math.Abs(orgVariableValue.Id);
            await _messageBusService.PublishAsync(orgVariableValue);
        }
    }

    public async Task DeleteClientAsync(Client client)
    {
        if (_clients.TryRemove(Math.Abs(client.Id), out var orgClient))
        {
            var variableIds = _variables.Values.Where(v => v.ClientId == orgClient.Id)
            .Select(x => x.Id)
            .ToList();

            var variableValueIds = _variableValues.Values.Where(v => variableIds.Contains(v.VariableId))
                .Select(x => x.Id)
                .ToList();

            await _dataRepository.DeleteVariableValuesAsync(variableValueIds);
            await _dataRepository.DeleteVariablesAsync(variableIds);
            await _dataRepository.DeleteClientAsync(orgClient);
            foreach (var variableValue in _variableValues.Values.Where(v => variableValueIds.Contains(v.Id)).ToList())
            {
                _variableValues.TryRemove(variableValue.Id, out _);
            }
            foreach (var variable in _variables.Values.Where(v => variableIds.Contains(v.Id)).ToList())
            {
                _variables.TryRemove(variable.Id, out _);
            }
            orgClient.Id = -Math.Abs(orgClient.Id);
            await _messageBusService.PublishAsync(orgClient);
        }
    }

    public async Task AddOrUpdateStateMachineAsync(StateMachine stateMachine)
    {
        if (stateMachine.Id == 0)
        {
            await _dataRepository.AddStateMachineAsync(stateMachine);
        }
        else
        {
            await _dataRepository.UpdateStateMachineAsync(stateMachine);
        }
        _stateMachines.AddOrUpdate(stateMachine.Id, stateMachine, (_, _) => stateMachine);
        await _messageBusService.PublishAsync(stateMachine);
    }

    public async Task DeleteStateMachineAsync(StateMachine stateMachine)
    {
        if (_stateMachines.TryRemove(-stateMachine.Id, out var orgStateMachine))
        {
            var variableIds = _variables.Values.Where(v => v.StateMachineId == orgStateMachine.Id)
                .Select(x => x.Id)
                .ToList();

            var variableValueIds = _variableValues.Values.Where(v => variableIds.Contains(v.VariableId))
                .Select(x => x.Id)
                .ToList();

            await _dataRepository.DeleteVariableValuesAsync(variableValueIds);
            await _dataRepository.DeleteVariablesAsync(variableIds);
            await _dataRepository.DeleteStateMachineAsync(orgStateMachine);
            foreach (var variableValue in _variableValues.Values.Where(v => variableValueIds.Contains(v.Id)).ToList())
            {
                _variableValues.TryRemove(variableValue.Id, out _);
            }
            foreach (var variable in _variables.Values.Where(v => variableIds.Contains(v.Id)).ToList())
            {
                _variables.TryRemove(variable.Id, out _);
            }
            orgStateMachine.Id = -Math.Abs(orgStateMachine.Id);
            await _messageBusService.PublishAsync(orgStateMachine);
        }
    }

}
