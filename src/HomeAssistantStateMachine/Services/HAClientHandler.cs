﻿using HassClient.Models;
using HassClient.WS;
using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Mono.TextTemplating;
using System.Collections.Concurrent;

namespace HomeAssistantStateMachine.Services;

public class HAClientHandler : IAsyncDisposable
{
    private sealed class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue? VariableValue { get; set; }
    }

    public HAClientService HAClientService { get; private set; }
    public HAClient HAClient { get; private set; }
    public VariableService VariableService { get; private set; }

    private HassWSApi? _wsApi;
    private bool _started = false;
    private readonly ConcurrentDictionary<string, List<VariableInfo>> _variables = [];

    public HAClientHandler(HAClientService haClientService, HAClient haClient, VariableService variableService)
    {
        HAClientService = haClientService;
        HAClient = haClient;
        VariableService = variableService;
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            var variables = VariableService.GetScopedVariables(HAClient, null, null);
            foreach (var variable in variables)
            {
                if (!string.IsNullOrWhiteSpace(variable.variable.Data))
                {
                    if (!_variables.TryGetValue(variable.variable.Data, out var variableInfos))
                    {
                        variableInfos = [];
                        _variables.TryAdd(variable.variable.Data, variableInfos);
                    }
                    var v = new VariableInfo()
                    {
                        Variable = variable.variable,
                        VariableValue = variable.variableValue
                    };
                    variableInfos.Add(v);
                }
            }
            _wsApi = new HassWSApi();
            _wsApi.ConnectionStateChanged += _wsApi_ConnectionStateChanged;
            if (HAClient.Enabled && !string.IsNullOrWhiteSpace(HAClient.Token) && !string.IsNullOrWhiteSpace(HAClient.Host))
            {
                await ConnectAsync();
                foreach (var variable in _variables)
                {
                    _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(variable.Key, EventHandlerEventStateChanged);
                }
                _started = true;
            }
        }
    }

    public async Task<Variable?> CreateVariableAsync(string name, string? data, HasmDbContext? ctx = null)
    {
        Variable? result = null;
        var haEntityId = data ?? name;

        var existingVariable = VariableService.GetVariable(name);
        if (existingVariable != null && existingVariable.Data == data && existingVariable.HAClientId == HAClient.Id)
        {
            return existingVariable;
        }

        //todo: check data has changed!

        result = await VariableService.CreateVariableAsync(name, data, HAClient, null, null, ctx);
        if (result != null && !string.IsNullOrWhiteSpace(result.Data))
        {
            VariableValue? vv = null;
            if (!_variables.TryGetValue(haEntityId, out var variableInfos))
            {
                variableInfos = [];
                _variables.TryAdd(haEntityId, variableInfos);
            }
            else
            {
                vv = variableInfos.Find(v => v.VariableValue != null)?.VariableValue;
            }
            var v = new VariableInfo()
            {
                Variable = result,
                VariableValue = null
            };
            variableInfos.Add(v);

            if (vv != null && vv.Value != null)
            {
                await UpdateVariableValue(haEntityId, vv.Value);
            }
            else if (vv == null && ConnectionState == ConnectionStates.Connected)
            {
                _wsApi!.StateChagedEventListener.SubscribeEntityStatusChanged(haEntityId, EventHandlerEventStateChanged);
                var states = await _wsApi!.GetStatesAsync();
                var state = states.FirstOrDefault(s => s.EntityId == result.Data);
                if (state != null)
                {
                    await UpdateVariableValue(state.EntityId, state.State);
                }
            }
        }
        return result;
    }

    public async Task<bool> CallServiceAsync(string name, string service, object? data = null)
    {
        var result = false;
        if (ConnectionState == ConnectionStates.Connected)
        {
            try
            {
                var callResult = await _wsApi!.CallServiceAsync(name, service, data);
                result = callResult != null;
            }
            catch
            {//ignore
            }
        }
        return result;
    }

    public async Task<bool> CallServiceForEntitiesAsync(string name, string service, params string[] entityIds)
    {
        var result = false;
        if (ConnectionState == ConnectionStates.Connected)
        {
            try
            {
                result = await _wsApi!.CallServiceForEntitiesAsync(name, service, entityIds);
            }
            catch
            {//ignore
            }
        }
        return result;
    }

    public ConnectionStates ConnectionState => _wsApi?.ConnectionState ?? ConnectionStates.Disconnected;

    private async void _wsApi_ConnectionStateChanged(object? sender, ConnectionStates e)
    {
        if (e == ConnectionStates.Connected && _variables.Any())
        {
            var states = await _wsApi!.GetStatesAsync();
            foreach (var state in states)
            {
                await UpdateVariableValue(state.EntityId, state.State);
            }
        }
        HAClientService.ClientHandlerConnectionStateChanged(this, e);
    }

    private async Task ConnectAsync()
    {
        if (_wsApi == null) return;
        try
        {
            var connectionParameters = ConnectionParameters.CreateFromInstanceBaseUrl(HAClient.Host, HAClient.Token);
            await _wsApi.ConnectAsync(connectionParameters);
        }
        catch
        {
            //todo
        }
    }

    private async void EventHandlerEventStateChanged(object? sender, StateChangedEvent stateChangedArgs)
    {
        await UpdateVariableValue(stateChangedArgs.EntityId, stateChangedArgs.NewState.State);
    }

    private async Task UpdateVariableValue(string eventId, string state)
    {
        if (_variables.TryGetValue(eventId, out var variableInfos))
        {
            foreach (var variableInfo in variableInfos)
            {
                if (variableInfo.VariableValue == null)
                {
                    variableInfo.VariableValue = new VariableValue()
                    {
                        Variable = variableInfo.Variable,
                        Value = state
                    };
                    await VariableService.CreateVariableValueAsync(variableInfo.VariableValue);
                }
                else
                {
                    await VariableService.UpdateVariableValueAsync(variableInfo.VariableValue, state);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started && _wsApi != null)
        {
            _wsApi.ConnectionStateChanged -= _wsApi_ConnectionStateChanged;
            try
            {
                await _wsApi.CloseAsync();
            }
            catch
            {
                //nothing
            }
        }
    }
}
