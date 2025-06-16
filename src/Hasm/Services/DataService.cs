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

    public async Task StartAsync()
    {
        await _dataRepository.SetupAsync();

        var clients = await _dataRepository.GetClientsAsync();
        foreach(var client in clients)
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
            _variableValues.TryAdd(variableValue.Id, variableValue);
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
        await _messageBusService.SendAsync(client);
    }

    public async Task DeleteClientAsync(Client client)
    {
        var variableIds = _variables.Values.Where(v => v.ClientId == client.Id)
            .Select(x => x.Id)
            .ToList();

        var variableValueIds = _variableValues.Values.Where(v => variableIds.Contains(v.VariableId))
            .Select(x => x.Id)
            .ToList();

        await _dataRepository.DeleteVariableValuesAsync(variableValueIds);
        await _dataRepository.DeleteVariablesAsync(variableIds);
        await _dataRepository.DeleteClientAsync(client);
        foreach (var variableValue in _variableValues.Values.Where(v => variableValueIds.Contains(v.Id)).ToList())
        {
            _variableValues.TryRemove(variableValue.Id, out _);
        }
        foreach (var variable in _variables.Values.Where(v => variableIds.Contains(v.Id)).ToList())
        {
            _variables.TryRemove(variable.Id, out _);
        }
        _clients.TryRemove(-client.Id, out _);
        await _messageBusService.SendAsync(client);
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
         await _messageBusService.SendAsync(stateMachine);
    }

    public async Task DeleteStateMachineAsync(StateMachine stateMachine)
    {
        var variableIds = _variables.Values.Where(v => v.StateMachineId == stateMachine.Id)
            .Select(x => x.Id)
            .ToList();

        var variableValueIds = _variableValues.Values.Where(v => variableIds.Contains(v.VariableId))
            .Select(x => x.Id)
            .ToList();

        await _dataRepository.DeleteVariableValuesAsync(variableValueIds);
        await _dataRepository.DeleteVariablesAsync(variableIds);
        await _dataRepository.DeleteStateMachineAsync(stateMachine);
        foreach (var variableValue in _variableValues.Values.Where(v => variableValueIds.Contains(v.Id)).ToList())
        {
            _variableValues.TryRemove(variableValue.Id, out _);
        }
        foreach (var variable in _variables.Values.Where(v => variableIds.Contains(v.Id)).ToList())
        {
            _variables.TryRemove(variable.Id, out _);
        }
        _stateMachines.TryRemove(-stateMachine.Id, out _);
        await _messageBusService.SendAsync(stateMachine);
    }

}
