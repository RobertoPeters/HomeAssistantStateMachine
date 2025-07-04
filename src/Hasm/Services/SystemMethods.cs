using Jint.Native;
using System.Collections.Concurrent;

namespace Hasm.Services;

public class SystemMethods
{
    private readonly VariableService _variableService;
    private readonly StateMachineHandler _stateMachineHandler;
    private readonly ClientService _clientService;
    private readonly ConcurrentDictionary<int, Models.Client> _clients;

    public SystemMethods(ClientService clientService, DataService dataService, VariableService variableService, StateMachineHandler stateMachineHandler)
    {

        _variableService = variableService;
        _stateMachineHandler = stateMachineHandler;
        _clientService = clientService;
        _clients = new ConcurrentDictionary<int, Models.Client>(dataService.GetClients().ToDictionary(c => c.Id));
    }

    public void log(string instanceId, object? message)
    {
        _stateMachineHandler.AddLogAsync(instanceId, message).Wait();
    }

    public int createVariable(string name, int clientId, bool isStateMachineVariable, bool persistant, JsValue? data, JsValue[]? mockingOptions)
    {
        return _variableService.CreateVariableAsync(name, clientId, isStateMachineVariable ? _stateMachineHandler.StateMachine.Id : null, persistant, data?.ToString(), null).Result ?? -1;
    }

    public bool setVariableValue(int variableId, string? value)
    {
        return _variableService.SetVariableValuesAsync([(variableId, value)]).Result;
    }

    public string? getVariableValue(int variableId)
    {
        return _variableService.GetVariable(variableId)?.Value;
    }

    public int getClientId(string name)
    {
        var client = _clients.Values.FirstOrDefault(c => c.Name == name);
        return client?.Id ?? -1;
    }

    public int[] getClientIdsByType(int id)
    {
        return _clients.Values.Where(c => (int)c.ClientType == id).Select(x => x.Id).ToArray();
    }

    public bool clientExecute(int clientId, int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        return _clientService.ExecuteAsync(clientId, variableId, command, parameter1, parameter2, parameter3).Result;
    }

    public void setRunningStateToFinished(string instanceId)
    {
        _stateMachineHandler.SetRunningStateFinished(instanceId);
    }

    public bool isSubStateMachineRunning(string instanceId)
    {
        return _stateMachineHandler.IsSubStateMachineRunning(instanceId);
    }

    public void setCurrentState(string stateName)
    {
        _stateMachineHandler.CurrentState = stateName;
    }

    public void startSubStateMachine(string stateId, string instanceId)
    {
        _stateMachineHandler.StartSubStateMachine(stateId, instanceId);
    }

    public readonly static string SystemScript = $$""""

    var {{string.Join("\r\nvar ", (((Models.ClientType[])Enum.GetValues(typeof(Models.ClientType))).Select(x => $"client_{Enum.GetName(x)} = {(int)x}").ToList()))}}

    log = function(message) {
        system.log(instanceId, message)
    }
    
    //check if sub statemachine is running
    subStateMachineRunning = function() {
        return system.isSubStateMachineRunning(instanceId)
    }
    subStateMachineFinished = function() {
        return !subStateMachineRunning()
    }

    startSubStateMachine = function(externalStateId, instanceId) {
        system.startSubStateMachine(externalStateId, instanceId)
    }
           
    // returns the client id or -1 if not found
    getClientId = function(name) {
        return system.getClientId(name)
    }

    getClientIdsByType = function(value) {
        return system.getClientIdsByType(value)
    }

    //execute specific client commands (-1 if it fails)
    executeOnClient = function(clientId, variableId, command, parameter1, parameter2, parameter3) {
        return system.clientExecute(clientId, variableId, command, parameter1, parameter2, parameter3)
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
    var timerClientId = getClientId('Timer')
    
    //====================================================================================
    // GENERIC CLIENT HELPER METHODS
    //====================================================================================
    createGenericVariable = function(name, value, mockingOptions) {
        return createVariable(name, genericClientId, true, true, value, mockingOptions)
    }

    //====================================================================================
    // TIMER CLIENT HELPER METHODS
    //====================================================================================
        
    createTimerVariable = function(name, seconds) {
        return createVariable(name, timerClientId, true, false, seconds, [0, 10])
    }

    startTimer = function(variableId) {
        return executeOnClient(timerClientId, variableId, 'start')
    }

    cancelTimer = function(variableId) {
        return executeOnClient(timerClientId, variableId, 'stop')
    }
    stopTimer = cancelTimer
    
    //====================================================================================
    // HOME ASSISTANT CLIENT HELPER METHODS
    //====================================================================================

    haClientCallService = function(clientname, name, service, data) {
        //e.g. haClientCallService(null, 'light', 'turn_on', { "entity_id": "light.my_light", "brightness_pct": 20})
        var client = null
        if (clientname != null) {
            client = getClientId(clientname)
            if (client < 0) {
                log('Error: Client not found: ' + clientname)
                return false
            }
        }
        else {
            var haClientIds = getClientIdsByType('client_HomeAssistant')
            if (haClientIds == null || haClientIds.length != 1) {
                log('Error: None of multiple Home Assistant clients found')
                return false
            }
            client = haClientIds[0]
        }
        log(client)
        return executeOnClient(client, null, 'callservice', name, service, data)
    }

    haClientCallServiceForEntities = function(clientname, name, service, entities) {
        //e.g. haClientCallService(null, 'light', 'turn_off', [ "light.my_light" ])
        var client = null
        if (clientname != null) {
            client = getClientId(clientname)
            if (client < 0) {
                log('Error: Client not found: ' + clientname)
                return false
            }
        }
        else {
            var haClientIds = getClientIdsByType('client_HomeAssistant')
            if (haClientIds == null || haClientIds.length != 1) {
                log('Error: None of multiple Home Assistant clients found')
                return false
            }
            client = haClientIds[0]
        }
        log(client)
        return executeOnClient(client, null, 'callserviceforentities', name, service, entities)
    }
    """";
}
