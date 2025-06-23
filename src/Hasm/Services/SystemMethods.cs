using Jint.Native;
using System.Collections.Concurrent;

namespace Hasm.Services;

public class SystemMethods
{
    private readonly VariableService _variableService;
    private readonly StateMachineHandler _stateMachineHandler;
    private readonly ConcurrentDictionary<int, Models.Client> _clients;

    public SystemMethods(ClientService clientService, DataService dataService, VariableService variableService, StateMachineHandler stateMachineHandler)
    {

        _variableService = variableService;
        _stateMachineHandler = stateMachineHandler;
        _clients = new ConcurrentDictionary<int, Models.Client>(dataService.GetClients().ToDictionary(c => c.Id));
    }

    public void log(object? message)
    {
        _stateMachineHandler.AddLogAsync(message).Wait();
    }

    public int createVariable(string name, int clientId, bool isStateMachineVariable, bool persistant, JsValue? data, JsValue[]? mockingOptions)
    {
        return _variableService.CreateVariableAsync(name, clientId, isStateMachineVariable ? _stateMachineHandler.StateMachine.Id : null, persistant, data?.ToString(), null).Result ?? -1;
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

    public void setRunningStateToFinished()
    {
        _stateMachineHandler.RunningState = StateMachineHandler.StateMachineRunningState.Finished;
    }

    public void setCurrentState(string stateName)
    {
        _stateMachineHandler.CurrentState = stateName;
    }

    public const string SystemScript = """"

    log = function(message) {
        system.log(message)
    }
    
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
    setVariableValue = function(variableId, variableValue) {
        return system.setVariableValue(variableId, variableValue)
    }
    
    var genericClientId = getClientId('Generic')
    
    createGenericVariable = function(name, value, mockingOptions) {
        return createVariable(name, genericClientId, true, true, value, mockingOptions)
    }

    """";
}
