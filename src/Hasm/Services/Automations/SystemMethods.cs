using Hasm.Extensions;
using Hasm.Models;
using Hasm.Services.Automations.Flow;
using Hasm.Services.Automations.StateMachine;
using Hasm.Services.Interfaces;
using Jint.Native;
using System.Collections.Concurrent;
using System.Text;

namespace Hasm.Services.Automations;

public class SystemMethods
{
    private readonly VariableService _variableService;
    private readonly IAutomationHandler _automationHandler;
    private readonly ClientService _clientService;
    private readonly ConcurrentDictionary<int, Client> _clients;

    public record DateTimeInfo(int year, int month, int day, int hour, int minute, int second, int dayOfWeek);

    public SystemMethods(ClientService clientService, DataService dataService, VariableService variableService, IAutomationHandler automationHandler)
    {

        _variableService = variableService;
        _automationHandler = automationHandler;
        _clientService = clientService;
        _clients = new ConcurrentDictionary<int, Client>(dataService.GetClients().ToDictionary(c => c.Id));
    }

    public void log(string instanceId, object? message)
    {
        _automationHandler.AddLogAsync(instanceId, message).Wait();
    }

    public DateTimeInfo getCurrentDateTime()
    {
        var now = DateTime.Now;
        return new DateTimeInfo(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, (int)now.DayOfWeek);
    }

    public bool currentTimeBetween(string startTime, string endTime, bool includeBoundary)
    {
        var currentTime = DateTime.Now.TimeOfDay;
        return currentTime.TimeOfDayBetween(startTime, endTime, includeBoundary);
    }

    public int createVariable(string name, int clientId, bool isAutomationVariable, bool persistant, JsValue? data, JsValue[]? mockingOptions)
    {
        List<string>? stringMockingOptions = null;
        if (mockingOptions?.Any() == true)
        {
            stringMockingOptions = [];
            foreach (var mockingOption in mockingOptions)
            {
                stringMockingOptions.Add(mockingOption.JsValueToString(false));
            }
        }
        return _variableService.CreateVariableAsync(name, clientId, isAutomationVariable ? _automationHandler.Automation.Id : null, persistant, data?.ToString(), stringMockingOptions).Result ?? -1;
    }

    public int? getVariableIdByName(string name, int clientId, bool isStateMachineVariable)
    {
        var variable = _variableService
                .GetVariables()
                .FirstOrDefault(v => v.Variable.Name == name
                                    && v.Variable.ClientId == clientId
                                    && (isStateMachineVariable && v.Variable.AutomationId == _automationHandler.Automation.Id || !isStateMachineVariable && v.Variable.AutomationId == null));
        return variable?.Variable.Id;
    }

    public bool setVariableValue(int variableId, string? value)
    {
        return _variableService.SetVariableValuesAsync([(variableId, value)]).Result;
    }

    public string? getVariableValue(int variableId)
    {
        return _variableService.GetVariable(variableId)?.Value;
    }

    public bool isMockingVariableActive(int variableId)
    {
        return _variableService.GetVariable(variableId)?.IsMocking ?? false;
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
        if (_automationHandler is StateMachineHandler stateMachineHandler)
        {
            stateMachineHandler.SetRunningStateFinished(instanceId);
        }
    }

    public bool isSubStateMachineRunning(string instanceId)
    {
        if (_automationHandler is StateMachineHandler stateMachineHandler)
        {
            return stateMachineHandler.IsSubStateMachineRunning(instanceId);
        }
        return false;
    }

    public void setCurrentState(string stateName)
    {
        if (_automationHandler is StateMachineHandler stateMachineHandler)
        {
            stateMachineHandler.CurrentState = stateName;
        }
    }

    public void startSubStateMachine(string stateId, string instanceId)
    {
        if (_automationHandler is StateMachineHandler stateMachineHandler)
        {
            stateMachineHandler.StartSubStateMachine(stateId, instanceId);
        }
    }

    public void UpdatePayload(string instanceId, string stepId, object? payload)
    {
        if (_automationHandler is FlowHandler flowHandler)
        {
            flowHandler.UpdatePayload(instanceId, stepId, payload);
        }
    }

    public static string SystemScript(AutomationType automationType)
    {
        var script = new StringBuilder();
        script.AppendLine(SystemScriptGeneric);
        return script.ToString();
    }

    private readonly static string SystemScriptGeneric = $$""""

    //====================================================================================
    // SYSTEM CONSTANTS
    //====================================================================================
    var {{string.Join("\r\nvar ", ((ClientType[])Enum.GetValues(typeof(ClientType))).Select(x => $"client_{Enum.GetName(x)} = {(int)x}").ToList())}}

    //====================================================================================
    // SYSTEM METHODS
    //====================================================================================
    
    log = function(message) {
        system.log(instanceId, JSON.stringify(message))
    }

    //returns the current (local) date and time as an object with year, month, day, hour, minute, second and dayOfWeek properties
    getCurrentDateTime = function() {
        return system.getCurrentDateTime()
    }

    currentTimeBetween = function(startTime, endTime, includeBoundary) {
        //e.g. currentTimeBetween('8:00', '18:00', true)
        // currentTimeBetween('22:00', '4:00', true)
        // currentTimeBetween('22:00', '0:30', true)
        //it used the 24h clock format without leading zeros
        //returns true if the current time is between startTime and endTime (inclusive or exclusive depending on includeBoundary)
        return system.currentTimeBetween(startTime, endTime, includeBoundary)
    }
    
    // returns the client id or -1 if not found
    getClientId = function(name) {
        return system.getClientId(name)
    }

    //returns array of all client ids of the given type
    //e.g. getClientIdsByType(client_HomeAssistant) will return all Home Assistant client ids
    getClientIdsByType = function(value) {
        return system.getClientIdsByType(value)
    }

    //execute specific client commands (-1 if it fails)
    //e.g. executeOnClient(clientIdOfHomeAssistant, null, 'callservice', 'light', 'turn_on', { "entity_id": "light.my_light", "brightness_pct": 20})
    executeOnClient = function(clientId, variableId, command, parameter1, parameter2, parameter3) {
        return system.clientExecute(clientId, variableId, command, parameter1, parameter2, parameter3)
    }

    // creates a variable and returns the variable id (-1 if it fails)
    // e.g. createVariable('test', clientId, true, true, 'initialValue', ['option1', 'option2'])
    createVariableOnClient = function(name, clientId, isAutomationVariable, persistant, data, mockingOptions) {
        return system.createVariable(name, clientId, isAutomationVariable, persistant, data, mockingOptions)
    }

    // returns the variable value
    getVariableValue = function(variableId) {
        return system.getVariableValue(variableId)
    }

    // sets the variable value (returns true if successful, false otherwise)
    setVariableValue = function(variableId, variableValue) {
        return system.setVariableValue(variableId, variableValue)
    }

    //get the variable Id by name
    getVariableIdByName = function(name, clientId, isAutomationVariable) {
        return system.getVariableIdByName(name, clientId, isAutomationVariable)
    }

    isMockingVariableActive = function(variableId) {
        return system.isMockingVariableActive(variableId)
    }    
    
    //====================================================================================
    // HELPER MEHODS and VARIABLES
    //====================================================================================
    
    //the two system clients
    var genericClientId = getClientId('Generic')
    var timerClientId = getClientId('Timer')

    isFalse = function(value) {
        //e.g. isTrue('false') or isTrue(false)
        if (value === undefined || value == null || value == '') {
            return false
        }
        return value === false || value === 'false' || value === 0 || value === '0' || value === 'off'
    }

    isTrue = function(value) {
        //e.g. isTrue('true') or isTrue(true)
        if (value === undefined || value == null || value == '') {
            return false
        }
        return !isFalse(value)
    }

    
    //====================================================================================
    // GENERIC CLIENT HELPER METHODS
    //====================================================================================
    createGenericVariable = function(name, value, mockingOptions) {
        //e.g. createGenericVariable('test', 'initialValue', ['option1', 'option2'])
        return createVariableOnClient(name, genericClientId, true, true, value, mockingOptions)
    }

    //====================================================================================
    // TIMER CLIENT HELPER METHODS
    //====================================================================================
    
    //creates a timer variable and returns the variable id (-1 if it fails)
    //it does not start the timer automatically, you need to call startTimerVariable(variableId) in order to actually start the timer
    createTimerVariable = function(name, seconds) {
        return createVariableOnClient(name, timerClientId, true, false, seconds, [0, 10])
    }

    //starts the timer (-1 if it fails)
    startTimerVariable = function(variableId) {
        return executeOnClient(timerClientId, variableId, 'start')
    }

    //stops/cancels the timer (-1 if it fails)
    cancelTimerVariable = function(variableId) {
        return executeOnClient(timerClientId, variableId, 'stop')
    }
    stopTimerVariable = cancelTimerVariable
    
    //====================================================================================
    // HOME ASSISTANT CLIENT HELPER METHODS
    //====================================================================================

    getHomeAssistantClientId = function(clientname) {
        var client = null
        if (clientname != null) {
            client = getClientId(clientname)
            if (client < 0) {
                log('Error: Client not found: ' + clientname)
                return null
            }
        }
        else {
            var haClientIds = getClientIdsByType(client_HomeAssistant)
            if (haClientIds == null || haClientIds.length != 1) {
                log('Error: None of multiple Home Assistant clients found')
                return null
            }
            client = haClientIds[0]
        }
        return client
    }

    haClientCallService = function(clientname, name, service, data) {
        //e.g. haClientCallService(null, 'light', 'turn_on', { "entity_id": "light.my_light", "brightness_pct": 20})
        var client = getHomeAssistantClientId(clientname)
        if (client == null) {
            return false
        }
        return executeOnClient(client, null, 'callservice', name, service, data)
    }

    haClientCallServiceForEntities = function(clientname, name, service, entities) {
        //e.g. haClientCallService(null, 'light', 'turn_off', [ "light.my_light" ])
        var client = getHomeAssistantClientId(clientname)
        if (client == null) {
            return false
        }
        return executeOnClient(client, null, 'callserviceforentities', name, service, entities)
    }

    //====================================================================================
    // MQTT CLIENT HELPER METHODS
    //====================================================================================
    
    getMqttClientId = function(clientname) {
        var client = null
        if (clientname != null) {
            client = getClientId(clientname)
            if (client < 0) {
                log('Error: Client not found: ' + clientname)
                return null
            }
        }
        else {
            var mqttClientIds = getClientIdsByType(client_Mqtt)
            if (mqttClientIds == null || mqttClientIds.length != 1) {
                log('Error: None of multiple MQTT clients found')
                return null
            }
            client = mqttClientIds[0]
        }
        return client
    }

    mqttClientPublish = function(clientname, topic, payload) {
        //e.g. mqttClientPublish(null, 'mqttnet/samples/topic/2', 'on')
        var client = getMqttClientId(clientname)
        if (client == null) {
            return false
        }
        return executeOnClient(client, null, 'publish', topic, payload)
    }
    
    //====================================================================================
    // BACKWARDS COMPATIBILITY METHODS
    //====================================================================================
    getCurrentState = function() {
        if (currentState == null)
        {
           return null
        }
        return stateInfo[currentState].name
    }

    getValue = function(name) {
        var variableId = getVariableIdByName(name, genericClientId, true)
        return getVariableValue(variableId);
    }
    
    createTimer = function(name, seconds) {
        var timerId = createTimerVariable(name, seconds)
        startTimerVariable(timerId)
        return timerId
    }
    startTimer = createTimer
    
    timerExpired = function(name) {
        var variableId = getVariableIdByName(name, timerClientId, true)
        return getVariableValue(variableId) == '0'
    }
    
    //not suported anymore use isMockingVariableActive instead
    //mocking variables is now per variable
    isMockingVariablesActive = function() {
        return false
    }

    //not supported anymore
    gotoState = function(state) {
        return false
    }
    
    createGlobalVariableWithMockingValues = function(name, valueOptions) {
        //e.g. createGlobalVariableWithMockingValues('test', [true, false]); this will create a persistant variable accessible from any state machine
        return createVariableOnClient(name, genericClientId, false, true, null, valueOptions)
    }
    
    createGlobalVariable = function(name) {
        //e.g. createGlobalVariable('test'); this will create a persistant variable accessible from any state machine
        return createVariableOnClient(name, genericClientId, false, true, null)
    }
    
    setGlobalVariable = function(name, newValue) {
        //e.g. setGlobalVariable('test', 10); update the value of the given (global) variable
        var variableId = getVariableIdByName(name, genericClientId, false)
        return setVariableValue(variableId, newValue)
    }
    
    getGlobalVariable = function(name) {
        //e.g. getGlobalVariable('test'); get the value of the given (global) variable
        var variableId = getVariableIdByName(name, genericClientId, false)
        return getVariableValue(variableId)
    }
    
    createVariableWithMockingValues = function(name, valueOptions) {
        //e.g. createVariableWithMockingValues('test', [true, false]); this will create a persistant variable accessible from current state machine
        return createGenericVariable(name, null, valueOptions)
    }
    
    createVariable = function(name) {
        //e.g. createVariable('test'); this will create a persistant variable accessible from current state machine
        return createGenericVariable(name, null, null)
    }
    
    setVariable = function(name, newValue) {
        //e.g. setVariable('test', 10); update the value of the given (state machine) variable
        var variableId = getVariableIdByName(name, genericClientId, true)
        return setVariableValue(variableId, newValue)
    }
    

    getVariable = function(name) {
        //e.g. getVariable('test'); get the value of the given (state machine) variable
        var variableId = getVariableIdByName(name, genericClientId, true)
        return getVariableValue(variableId)
    }
    
    createHAVariableWithMockingValues = function(clientname, name, entityId, valueOptions) {
        //e.g. createHAVariableWithMockingValues(null, 'kitchenLight', 'light.my_light', ['on', 'off'])
        var client = getHomeAssistantClientId(clientname)
        if (client == null) {
            return false
        }
        return createVariableOnClient(name, client, true, true, entityId, valueOptions)
    }
    
    createHAVariable = function(clientname, name, entityId) {
        //e.g. createHAVariable(null, 'kitchenLight', 'light.my_light')
        var client = getHomeAssistantClientId(clientname)
        if (client == null) {
            return false
        }
        return createVariableOnClient(name, client, true, true, entityId)
    }
    
    getHAVariable = function(clientname, name) {
        //e.g. getHAVariable(clientname, 'test'); get the value of the given (HA) variable
        var client = getHomeAssistantClientId(clientname)
        if (client == null) {
            return null
        }
        var variableId = getVariableIdByName(name, client, true)
        return getVariableValue(variableId)
    }
    
    setHAStateChanged = function(clientname, name, functionRef) {
        //e.g. setHAStateChanged(null, 'kitchenLight', function(data) { log(data) })
        return false
    }
    
    createMqttVariableWithMockingValues = function(clientname, name, topic, valueOptions) {
        //e.g. createMqttVariableWithMockingValues(null, 'kitchenLight', 'mqttnet/samples/topic/2', ['on', 'off'])
        var client = getMqttClientId(clientname)
        if (client == null) {
            return false
        }
        return createVariableOnClient(name, client, true, true, topic, valueOptions)
    }
    
    createMqttVariable = function(clientname, name, topic) {
        //e.g. createMqttVariable(null, 'kitchenLight', 'mqttnet/samples/topic/2');
        var client = getMqttClientId(clientname)
        if (client == null) {
            return false
        }
        return createVariableOnClient(name, client, true, true, topic)
    }
    
    getMqttVariable = function(clientname, name) {
        //e.g. getMqttVariable(clientname, 'test'); get the value of the given (MQTT topic) variable
        var client = getMqttClientId(clientname)
        if (client == null) {
            return null
        }
        var variableId = getVariableIdByName(name, client, true)
        return getVariableValue(variableId)
    }
    
    setMqttStateChanged = function(clientname, name, functionRef) {
        //e.g. setMqttStateChanged(null, 'kitchenLight', function(data) { log(data) })
        return null
    }
    
    //====================================================================================
    // State Machine Automation METHODS
    //====================================================================================
    
    //check if sub statemachine is running
    subStateMachineRunning = function() {
        return system.isSubStateMachineRunning(instanceId)
    }
    
    subStateMachineFinished = function() {
        return !subStateMachineRunning()
    }
    
    // INTERNAL USE ONLY
    startSubStateMachine = function(externalStateId, instanceId) {
        system.startSubStateMachine(externalStateId, instanceId)
    }
 
    //====================================================================================
    // Flow Automation METHODS
    //====================================================================================
    
    function getSteps(stepIds) {
        return steps.filter(function(step) {
            return stepIds.includes(step.id)
        })
    }

    function logStepsData()
    {
        var allSteps = steps.map(function(step) {
                return {
                    id: step.id,
                    name: step.name,
                    currentPayload: step.currentPayload,
                    payloadUpdatedAt: step.payloadUpdatedAt
                }
            })
        log(allSteps)
    }
    
    // INTERNAL USE ONLY
    updatePayload = function(stepId, payload) {
        system.UpdatePayload(instanceId, stepId, payload)
    }

    //====================================================================================
    // Sqlite Database Automation METHODS
    //====================================================================================
 
    executeNonQuery = function(clientId, query) {
        executeOnClient(clientId, null, 'query', query)
    }    

    executeScalar = function(clientId, variableId, query) {
        executeOnClient(clientId, variableId, 'scalar', query)
    }  

    executeReader = function(clientId, variableId, query) {
        executeOnClient(clientId, variableId, 'reader', query)
    }  

    """";
}
