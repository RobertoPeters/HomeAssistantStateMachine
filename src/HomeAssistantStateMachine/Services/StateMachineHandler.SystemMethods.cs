using HomeAssistantStateMachine.Models;
using Jint.Native;
using System.Collections.Generic;
using System.Text;

namespace HomeAssistantStateMachine.Services;

public partial class StateMachineHandler
{
    private SystemMethods NewSystemMethods => new SystemMethods(_variableService, this, _haClientService);

    public class SystemMethods
    {
        private readonly VariableService _variableService;
        private readonly StateMachineHandler _stateMachineHandler;
        private readonly HAClientService _haClientService;

        public SystemMethods(VariableService variableService, StateMachineHandler stateMachineHandler, HAClientService haClientService)
        {
            _variableService = variableService;
            _stateMachineHandler = stateMachineHandler;
            _haClientService = haClientService;
        }

        public void log(object? data)
        {
            _stateMachineHandler.AddLog(data);
        }
        public void gotoState(string stateName)
        {
            _stateMachineHandler.SetEngineRequestToState(stateName);
        }

        public string getCurrentState()
        {
            return _stateMachineHandler.GetEngineState();
        }

        public bool createHAVariable(string? clientName, string name, string? data)
        {
            var result = false;
            var haClientHandler = _haClientService.GetClientHandler(clientName);
            if (haClientHandler != null)
            {
                name = $"__HA_{haClientHandler.HAClient.Id}__{name}";
                return haClientHandler.CreateVariableAsync(name, data).Result != null;
            }
            return result;
        }

        public object? getHAVariable(string? clientName, string name)
        {
            var haClientHandler = _haClientService.GetClientHandler(clientName);
            if (haClientHandler != null)
            {
                name = $"__HA_{haClientHandler.HAClient.Id}__{name}";
                return _variableService.GetVariableValue(name);
            }
            return null;
        }

        public bool setHAStateChanged(string? clientName, string name, Func<JsValue, JsValue[], JsValue> callback)
        {
            var haClientHandler = _haClientService.GetClientHandler(clientName);
            if (haClientHandler != null)
            {
                name = $"__HA_{haClientHandler.HAClient.Id}__{name}";
                var variable = _variableService.GetVariable(name);
                if (variable != null)
                {
                    return _stateMachineHandler.SetHAStateChanged(haClientHandler, variable, callback);
                }
            }
            return false;
        }

        public bool createVariable(string name)
        {
            name = $"__SM_{_stateMachineHandler.StateMachine.Id}__{name}";
            return _variableService.CreateVariableAsync(name, null, null, _stateMachineHandler.StateMachine.Id, null).Result != null;
        }

        public bool setVariable(string name, string? v)
        {
            name = $"__SM_{_stateMachineHandler.StateMachine.Id}__{name}";
            return _variableService.UpdateVariableValueAsync(name, v).Result;
        }

        public object? getVariable(string name)
        {
            name = $"__SM_{_stateMachineHandler.StateMachine.Id}__{name}";
            return _variableService.GetVariableValue(name);
        }

        public bool createGlobalVariable(string name)
        {
            return _variableService.CreateVariableAsync(name, null, null, (int?)null, null).Result != null;
        }

        public bool setGlobalVariable(string name, string? v)
        {
            return _variableService.UpdateVariableValueAsync(name, v).Result;
        }

        public object? getGlobalVariable(string name)
        {
            return _variableService.GetVariableValue(name);
        }

        public bool createCountdownTimer(string name, int seconds)
        {
            return _variableService.CreateCountdownTimer(_stateMachineHandler.StateMachine.Id, name, seconds);
        }

        public bool countdownTimerExpired(string name)
        {
            return _variableService.GetCountdownTimerExpired(_stateMachineHandler.StateMachine.Id, name);
        }

        public bool haClientCallService(string? clientName, string name, string service, object? data = null)
        {
            return _haClientService.CallServiceAsync(clientName, name, service, data).Result;
        }

        public bool haClientCallServiceForEntities(string? clientName, string name, string service, params string[] entityIds)
        {
            return _haClientService.CallServiceForEntitiesAsync(clientName, name, service, entityIds).Result;
        }
    }

    public const string SystemScript = """"
        log = function(data) {
            return system.log(data);
        }

        gotoState = function(state) {
            return system.gotoState(state);
        }

        getCurrentState = function() {
            return system.getCurrentState();
        }
                
        getValue = function(name) {
            return system.getVariableValue(name);
        }

        createTimer = function(name, seconds) {
            return system.createCountdownTimer(name, seconds);
        }

        startTimer = createTimer
        
        timerExpired = function(name) {
            return system.countdownTimerExpired(name);
        }

        createGlobalVariable = function(name) {
            //e.g. createGlobalVariable('test'); this will create a persistant variable accessible from any state machine
            return system.createGlobalVariable(name);
        }
        
        setGlobalVariable = function(name, newValue) {
            //e.g. setGlobalVariable('test', 10); update the value of the given (global) variable
            return system.setGlobalVariable(name, newValue);
        }
        
        getGlobalVariable = function(name) {
            //e.g. getGlobalVariable('test'); get the value of the given (global) variable
            return system.getGlobalVariable(name);
        }

        createVariable = function(name) {
            //e.g. createVariable('test'); this will create a persistant variable accessible from current state machine
            return system.createVariable(name);
        }

        setVariable = function(name, newValue) {
            //e.g. setVariable('test', 10); update the value of the given (state machine) variable
            return system.setVariable(name, newValue);
        }

        getVariable = function(name) {
            //e.g. getVariable('test'); get the value of the given (state machine) variable
            return system.getVariable(name);
        }
        
        createHAVariable = function(clientname, name, entityId) {
            //e.g. createHAVariable(null, 'kitchenLight', 'light.my_light');
            return system.createHAVariable(clientname, name, entityId);
        }

        getHAVariable = function(clientname, name) {
            //e.g. getHAVariable(clientname, 'test'); get the value of the given (HA) variable
            return system.getHAVariable(clientname, name);
        }
 
        setHAStateChanged = function(clientname, name, functionRef) {
            //e.g. setHAStateChanged(null, 'kitchenLight', function(data) { log(data) });
            return system.setHAStateChanged(clientname, name, functionRef);
        }
                
        haClientCallService = function(clientname, name, service, data) {
            //e.g. haClientCallService(null, 'light', 'turn_on', { entity_id = "light.my_light", brightness_pct = 20});
            return system.haClientCallService(clientname, name, service, data);
        }
        
        haClientCallServiceForEntities = function(clientname, name, service, entityIds) {
            //e.g. haClientCallServiceForEntities(null, 'light', 'toggle', ["light.my_light1", "light.my_light2"]);
            return system.haClientCallServiceForEntities(clientname, name, service, entityIds);
        }

        """";
}

