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

        public bool createHAVariable(string? clientName, string name, string? data)
        {
            var result = false;
            var haClientHandler = _haClientService.GetClientHandler(clientName);
            if (haClientHandler != null)
            {
                return haClientHandler.CreateVariableAsync(name, data).Result != null;
            }
            return result;
        }

        public bool createVariable(string name)
        {
            return _variableService.CreateVariableAsync(name, null, (int?)null, null, null).Result != null;
        }

        public bool setVariable(string name, string? v)
        {
            return _variableService.UpdateVariableValueAsync(name, v).Result;
        }

        public object? getVariableValue(string variable)
        {
            return _variableService.GetVariableValue(variable);
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
        getValue = function(variable) {
            return system.getVariableValue(variable);
        }

        createTimer = function(name, seconds) {
            return system.createCountdownTimer(name, seconds);
        }

        startTimer = createTimer
        
        timerExpired = function(name) {
            return system.countdownTimerExpired(name);
        }

        createVariable = function(name) {
            //e.g. createVariable('test'); this will create a persistant variable
            return system.createVariable(name);
        }

        setVariable = function(name, newValue) {
            //e.g. setVariable('test', 10); update the value of the given variable
            return system.setVariable(name, newValue);
        }
        
        createHAVariable = function(clientname, name, entityId) {
            //e.g. createHAVariable(null, 'kitchenLight', 'light.my_light');
            return system.createHAVariable(clientname, name, entityId);
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

