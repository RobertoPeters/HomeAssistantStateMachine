using Jint.Native;
using System.Collections.Concurrent;

namespace Hasm.Services;

public class SystemMethods
{
    private readonly VariableService _variableService;
    private readonly StateMachineHandler _stateMachineHandler;
    private readonly ConcurrentDictionary<int, Models.Variable> _variables;
    private readonly ConcurrentDictionary<int, Models.Client> _clients;

    public SystemMethods(ClientService clientService, DataService dataService, VariableService variableService, StateMachineHandler stateMachineHandler)
    {

        _variableService = variableService;
        _stateMachineHandler = stateMachineHandler;
        var variables = dataService.GetVariables().Where(x => x.StateMachineId == null || x.StateMachineId == stateMachineHandler.StateMachine.Id);
        _variables = new ConcurrentDictionary<int, Models.Variable>(variables.ToDictionary(v => v.Id));
        _clients = new ConcurrentDictionary<int, Models.Client>(dataService.GetClients().ToDictionary(c => c.Id));
    }

    public int createVariable(string name, int clientId, bool isStateMachineVariable, bool persistant, string? data, JsValue[]? mockingOptions)
    {
        if (!_clients.TryGetValue(clientId, out var client) || !client.Enabled)
        {
            return -1;
        }

        return _variableService.CreateVariableAsync(name, client.Id, isStateMachineVariable ? _stateMachineHandler.StateMachine.Id : null, persistant, data, null).Result ?? -1;
    }

    public bool setVariableValue(int variableId, string? value)
    {
        return _variableService.SetVariableValueAsync(variableId, value).Result;
    }

    public string? getVariableValue(int variableId)
    {
        return _variableService.GetVariable(variableId)?.Value;
    }

    public int GetClientId(string name)
    {
        var client = _clients.Values.FirstOrDefault(c => c.Name == name);
        return client?.Id ?? -1;
    }

    public const string SystemScript = """"

    // returns the client id or -1 if not found
    getClientId = function(name) {
        return system.getClientId(name)
    }

    // creates a variable and returns the variable id (-1 if it fails)
    createVariable = function(name, clientId, isStateMachineVariable, persistant, data, mockingOptions) {
        return system.createVariable(name, clientId, isStateMachineVariable, persistant, data, mockingOptions)
    }

    // returns the variable value
    getVariableValue = function(variableId) {
        return system.getVariableValue(variableId)
    }

    // sets the variable value (returns true if successful, false otherwise)
    getVariableValue = setVariableValue(variableId, variableValue) {
        return system.getVariableValue(variableId, variableValue)
    }
    
    var genericClientId = getClientId('Generic')
    
    """";
}
