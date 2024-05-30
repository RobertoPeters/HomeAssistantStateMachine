using HomeAssistantStateMachine.Models;
using Jint.Native;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace HomeAssistantStateMachine.Services;

public partial class StateMachineHandler
{
    private SystemMethods NewSystemMethods => new SystemMethods(_variableService, this, _haClientService, _mqttClientService);

    public class MockingVariableInfo
    {
        public string SystemName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public object? Value { get; set; }
        public JsValue[]? ValueSelection { get; set; }
    }

    public List<MockingVariableInfo> GetMockingVariables()
    {
        return _systemMethods.GetMockingVariables();
    }

    public bool GetMockingVariablesActive()
    {
        return _systemMethods.GetMockingVariablesActive();
    }
    public void SetMockingVariablesActive(bool value)
    {
        _systemMethods.SetMockingVariablesActive(value);
    }

    public void UpdateMockingVariableValue(string systemName, object? value)
    {
        _systemMethods.UpdateMockingVariableValue(systemName, value);
        Task.Factory.StartNew(() =>
        {
            TriggerProcess();
        });
    }

    public class SystemMethods
    {
        private readonly VariableService _variableService;
        private readonly StateMachineHandler _stateMachineHandler;
        private readonly HAClientService _haClientService;
        private readonly MqttClientService _mqttClientService;

        private volatile bool _mockingVariablesActive = false;
        private readonly ConcurrentDictionary<string, MockingVariableInfo> _mockingVariables = [];

        public SystemMethods(VariableService variableService, StateMachineHandler stateMachineHandler, HAClientService haClientService, MqttClientService mqttClientService)
        {
            _variableService = variableService;
            _stateMachineHandler = stateMachineHandler;
            _haClientService = haClientService;
            _mqttClientService = mqttClientService;
        }

        //=======================================================================================================
        // mocking functions
        //=======================================================================================================
        public List<MockingVariableInfo> GetMockingVariables()
        {
            return _mockingVariables.Values.ToList();
        }

        public void UpdateMockingVariableValue(string systemName, object? value)
        {
            if (_mockingVariables.TryGetValue(systemName, out var mv))
            {
                mv.Value = value;
            }
        }

        public bool GetMockingVariablesActive()
        {
            return _mockingVariablesActive;
        }

        public void SetMockingVariablesActive(bool value)
        {
            if (!_mockingVariablesActive && value)
            {
                foreach (var variable in _mockingVariables)
                {
                    variable.Value.Value = _variableService.GetVariableValue(variable.Key);
                }
            }
            _mockingVariablesActive = value;
        }

        public void ClearMockingVariables()
        {
            _mockingVariables.Clear();
        }

        public bool isMockingVariablesActive()
        {
            return _mockingVariablesActive;
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

        //=======================================================================================================
        // MQTT functions
        //=======================================================================================================
        public bool createMqttVariable(string? clientName, string name, string? data)
        {
            return createMqttVariableWithMockingValues(clientName, name, data, null);
        }

        public bool createMqttVariableWithMockingValues(string? clientName, string name, string? data, JsValue[]? mockingOptions)
        {
            var result = false;
            var mqttClientHandler = _mqttClientService.GetClientHandler(clientName);
            if (mqttClientHandler != null)
            {
                var internalName = $"__MQ_{mqttClientHandler.MqttClient.Id}__{name}";
                if (!_mockingVariables.TryGetValue(internalName, out var mv))
                {
                    _mockingVariables.TryAdd(internalName, new MockingVariableInfo { SystemName = internalName, Name = name, ValueSelection = mockingOptions });
                }
                return mqttClientHandler.CreateVariableAsync(internalName, data).Result != null;
            }
            return result;
        }

        public object? getMqttVariable(string? clientName, string name)
        {
            var mqttClientHandler = _mqttClientService.GetClientHandler(clientName);
            if (mqttClientHandler != null)
            {
                name = $"__MQ_{mqttClientHandler.MqttClient.Id}__{name}";
                if (_mockingVariablesActive)
                {
                    _mockingVariables.TryGetValue(name, out var mv);
                    return mv?.Value;
                }
                return _variableService.GetVariableValue(name);
            }
            return null;
        }

        public bool mqttClientPublish(string? clientName, string topic, string? data)
        {
            return _mqttClientService.PublishAsync(clientName, topic, data).Result;
        }

        public bool setMqttStateChanged(string? clientName, string name, Func<JsValue, JsValue[], JsValue> callback)
        {
            var mqttClientHandler = _mqttClientService.GetClientHandler(clientName);
            if (mqttClientHandler != null)
            {
                name = $"__MQ_{mqttClientHandler.MqttClient.Id}__{name}";
                var variable = _variableService.GetVariable(name);
                if (variable != null)
                {
                    return _stateMachineHandler.SetStateChanged(null, mqttClientHandler, variable, callback);
                }
            }
            return false;
        }


        //=======================================================================================================
        // Home Asststant (HA) functions
        //=======================================================================================================
        public bool createHAVariable(string? clientName, string name, string? data)
        {
            return createHAVariableWithMockingValues(clientName, name, data, null);
        }

        public bool createHAVariableWithMockingValues(string? clientName, string name, string? data, JsValue[]? mockingOptions)
        {
            var result = false;
            var haClientHandler = _haClientService.GetClientHandler(clientName);
            if (haClientHandler != null)
            {
                var internalName = $"__HA_{haClientHandler.HAClient.Id}__{name}";
                if (!_mockingVariables.TryGetValue(internalName, out var mv))
                {
                    _mockingVariables.TryAdd(internalName, new MockingVariableInfo { SystemName = internalName, Name = name, ValueSelection = mockingOptions });
                }
                return haClientHandler.CreateVariableAsync(internalName, data).Result != null;
            }
            return result;
        }

        public object? getHAVariable(string? clientName, string name)
        {
            var haClientHandler = _haClientService.GetClientHandler(clientName);
            if (haClientHandler != null)
            {
                name = $"__HA_{haClientHandler.HAClient.Id}__{name}";
                if (_mockingVariablesActive)
                {
                    _mockingVariables.TryGetValue(name, out var mv);
                    return mv?.Value;
                }
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
                    return _stateMachineHandler.SetStateChanged(haClientHandler, null, variable, callback);
                }
            }
            return false;
        }

        public bool haClientCallService(string? clientName, string name, string service, object? data)
        {
            return _haClientService.CallServiceAsync(clientName, name, service, data).Result;
        }

        public bool haClientCallServiceForEntities(string? clientName, string name, string service, params string[] entityIds)
        {
            return _haClientService.CallServiceForEntitiesAsync(clientName, name, service, entityIds).Result;
        }

        //=======================================================================================================
        // generic variable functions
        //=======================================================================================================
        public bool createVariable(string name)
        {
            return createVariableWithMockingValues(name, null);
        }

        public bool createVariableWithMockingValues(string name, JsValue[]? mockingOptions)
        {
            var internalName = $"__SM_{_stateMachineHandler.StateMachine.Id}__{name}";
            if (!_mockingVariables.TryGetValue(internalName, out var mv))
            {
                _mockingVariables.TryAdd(internalName, new MockingVariableInfo { SystemName = internalName, Name = name, ValueSelection = mockingOptions });
            }
            return _variableService.CreateStateMachineVariableAsync(internalName, null, _stateMachineHandler.StateMachine.Id).Result != null;
        }

        public bool setVariable(string name, string? v)
        {
            name = $"__SM_{_stateMachineHandler.StateMachine.Id}__{name}";
            if (_mockingVariablesActive)
            {
                if (_mockingVariables.TryGetValue(name, out var mv))
                {
                    mv.Value = v;
                }
            }
            return _variableService.UpdateVariableValueAsync(name, v).Result;
        }

        public object? getVariable(string name)
        {
            name = $"__SM_{_stateMachineHandler.StateMachine.Id}__{name}";
            if (_mockingVariablesActive)
            {
                _mockingVariables.TryGetValue(name, out var mv);
                return mv?.Value;
            }
            return _variableService.GetVariableValue(name);
        }

        public bool createGlobalVariable(string name)
        {
            return createGlobalVariableWithMockingValues(name, null);
        }

        public bool createGlobalVariableWithMockingValues(string name, JsValue[]? mockingOptions)
        {
            if (!_mockingVariables.TryGetValue(name, out var mv))
            {
                _mockingVariables.TryAdd(name, new MockingVariableInfo { SystemName = name, Name = name, ValueSelection = mockingOptions });
            }
            return _variableService.CreateVariableAsync(name, null).Result != null;
        }

        public bool setGlobalVariable(string name, string? v)
        {
            if (_mockingVariablesActive)
            {
                if (_mockingVariables.TryGetValue(name, out var mv))
                {
                    mv.Value = v;
                }
            }
            return _variableService.UpdateVariableValueAsync(name, v).Result;
        }

        public object? getGlobalVariable(string name)
        {
            if (_mockingVariablesActive)
            {
                _mockingVariables.TryGetValue(name, out var mv);
                return mv?.Value;
            }
            return _variableService.GetVariableValue(name);
        }

        //=======================================================================================================
        // counter/timer functions
        //=======================================================================================================

        public bool createCountdownTimer(string name, int seconds)
        {
            return _variableService.CreateCountdownTimer(_stateMachineHandler.StateMachine.Id, name, seconds);
        }

        public bool countdownTimerExpired(string name)
        {
            return _variableService.GetCountdownTimerExpired(_stateMachineHandler.StateMachine.Id, name);
        }

     }

    public const string SystemScript = """"
        log = function(data) {
            return system.log(data);
        }

        isMockingVariablesActive = function() {
            return system.isMockingVariablesActive();
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

        createGlobalVariableWithMockingValues = function(name, valueOptions) {
            //e.g. createGlobalVariableWithMockingValues('test', [true, false]); this will create a persistant variable accessible from any state machine
            return system.createGlobalVariableWithMockingValues(name, valueOptions);
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

        createVariableWithMockingValues = function(name, valueOptions) {
            //e.g. createVariableWithMockingValues('test', [true, false]); this will create a persistant variable accessible from current state machine
            return system.createVariableWithMockingValues(name, valueOptions);
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
        
        createHAVariableWithMockingValues = function(clientname, name, entityId, valueOptions) {
            //e.g. createHAVariableWithMockingValues(null, 'kitchenLight', 'light.my_light', ['on', 'off']);
            return system.createHAVariableWithMockingValues(clientname, name, entityId, valueOptions);
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
            //e.g. haClientCallService(null, 'light', 'turn_on', { "entity_id": "light.my_light", "brightness_pct": 20});
            return system.haClientCallService(clientname, name, service, data);
        }
        
        haClientCallServiceForEntities = function(clientname, name, service, entityIds) {
            //e.g. haClientCallServiceForEntities(null, 'light', 'toggle', ["light.my_light1", "light.my_light2"]);
            return system.haClientCallServiceForEntities(clientname, name, service, entityIds);
        }


        createMqttVariableWithMockingValues = function(clientname, name, topic, valueOptions) {
            //e.g. createMqttVariableWithMockingValues(null, 'kitchenLight', 'mqttnet/samples/topic/2', ['on', 'off']);
            return system.createMqttVariableWithMockingValues(clientname, name, topic, valueOptions);
        }
        
        createMqttVariable = function(clientname, name, topic) {
            //e.g. createMqttVariable(null, 'kitchenLight', 'mqttnet/samples/topic/2');
            return system.createMqttVariable(clientname, name, topic);
        }
        
        getMqttVariable = function(clientname, name) {
            //e.g. getMqttVariable(clientname, 'test'); get the value of the given (MQTT topic) variable
            return system.getMqttVariable(clientname, name);
        }
        
        setMqttStateChanged = function(clientname, name, functionRef) {
            //e.g. setMqttStateChanged(null, 'kitchenLight', function(data) { log(data) });
            return system.setMqttStateChanged(clientname, name, functionRef);
        }
                
        mqttClientPublish = function(clientname, topic, payload) {
            //e.g. mqttClientPublish(null, 'mqttnet/samples/topic/2', 'on');
            return system.mqttClientPublish(clientname, topic, payload);
        }
        

        """";
}

