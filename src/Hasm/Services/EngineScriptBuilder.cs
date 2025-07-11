﻿using System.Text;
using Hasm.Models;

namespace Hasm.Services;

public static class EngineScriptBuilder
{
    public static string BuildEngineScriptForEditor(StateMachine stateMachine)
    {
        return BuildEngineScript(stateMachine, true, Guid.Empty, null);
    }

    public static string BuildEngineScript(StateMachine stateMachine, bool asMainStateMachine, Guid instanceId, List<(string variableName, string? variableValue)>? machineStateParameters)
    {
        var script = new StringBuilder();
        script.AppendLine("var global = this");
        script.AppendLine($"var isMainStateMachine = {asMainStateMachine.ToString().ToLower()}");
        script.AppendLine($"var instanceId = '{instanceId.ToString()}'");
        script.AppendLine();

        if (machineStateParameters == null)
        {
            foreach (var parameter in stateMachine.SubStateMachineParameters.ToList())
            {
                script.AppendLine($"var {parameter.ScriptVariableName} = {(string.IsNullOrWhiteSpace(parameter.DefaultValue) ? "null" : parameter.DefaultValue)}");
            }
        }
        else
        {
            if (machineStateParameters != null && machineStateParameters.Count > 0)
            {
                foreach (var parameter in machineStateParameters)
                {
                    script.AppendLine($"var {parameter.variableName} = {parameter.variableValue ?? "null"}");
                }
                script.AppendLine();
            }
        }

        script.AppendLine();
        script.AppendLine(SystemMethods.SystemScript);
        script.AppendLine();

        foreach (var state in stateMachine.States)
        {
            script.AppendLine();
            script.AppendLine($"//State Entry [{state.Name}]");
            script.AppendLine($"function {StateEntryMethodName(state)}() {{ ");
            if (state.EntryAction != null)
            {
                var allLines = state.EntryAction.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in allLines)
                {
                    script.AppendLine($"  {line}");
                }
            }
            script.AppendLine($"}}");
        }

        foreach (var transition in stateMachine.Transitions)
        {
            script.AppendLine();
            var fromState = stateMachine.States.First(s => s.Id == transition.FromStateId);
            var toState = stateMachine.States.First(s => s.Id == transition.ToStateId);
            script.AppendLine($"//Transition from [{fromState.Name}] to [{toState.Name}]");
            script.AppendLine($"function {TransitionMethodName(transition)}() {{ ");
            script.AppendLine($"  return {transition.Condition ?? "true"} ;");
            script.AppendLine($"}}");
        }

        script.AppendLine();
        script.AppendLine($"//Pre schedule action");
        script.AppendLine($"function preScheduleAction() {{ ");
        if (stateMachine.PreScheduleAction != null)
        {
            var allLines = stateMachine.PreScheduleAction.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in allLines)
            {
                script.AppendLine($"  {line}");
            }
        }
        script.AppendLine($"}}");

        script.AppendLine();
        script.AppendLine("var stateInfo = []");
        foreach (var state in stateMachine.States)
        {
            script.AppendLine($"stateInfo['{state.Id.ToString("N")}'] = {{");
            script.AppendLine($"'name': '{state.Name.Replace('\'', ' ')}',");
            script.AppendLine($"'externalId': '{state.Id.ToString()}',");
            script.AppendLine($"'isSubStateMachine': {state.IsSubState.ToString().ToLower()}");
            script.AppendLine($"}}");
        }
        script.AppendLine("var currentState = null");
        script.AppendLine($"var startState = '{GetStartState(stateMachine)?.Id.ToString("N")}'");
        var errorState = stateMachine.States.FirstOrDefault(s => s.IsErrorState);
        if (errorState == null)
        {
            script.AppendLine($"var errorState = null");
        }
        else
        {
            script.AppendLine($"var errorState = '{errorState.Id.ToString("N")}'");
        }

        script.AppendLine("var stateTransitionMap = []");
        foreach (var transition in stateMachine.Transitions)
        {
            var fromState = stateMachine.States.First(s => s.Id == transition.FromStateId);
            var toState = stateMachine.States.First(s => s.Id == transition.ToStateId);
            script.AppendLine($"stateTransitionMap.push({{'fromState': '{fromState.Id.ToString("N")}', 'transition': '{transition.Id.ToString("N")}', 'toState': '{toState.Id.ToString("N")}'}})");
        }
        script.AppendLine();

        script.AppendLine(""""
            function schedule() {
                var errorInSchedule = false
                var errorLogInfo = ''

                try
                {
                    errorLogInfo = 'Pre Schedule Action'
            	    preScheduleAction()

            	    if (currentState == null)
            	    {
                        errorLogInfo = 'State Entry (stateInfo[startState].name)'
            		    changeState(startState)
            	    }
            	    else
            	    {
            	        var transitions = stateTransitionMap.filter((transition) => transition.fromState == currentState)
                        if (transitions.length == 0)
                        {
                           log('No transitions found for current state: ' + stateInfo[currentState].name)
                           system.setRunningStateToFinished(instanceId)
                        }

                        errorLogInfo = 'Evaluation transitions of State: ' + stateInfo[currentState].name
               		    var successFulTransition = transitions.find((transition) => eval('transitionResult'+transition.transition+'()'))
            		    if (successFulTransition != null)
            		    {
                           errorLogInfo = 'State Entry (' + stateInfo[successFulTransition.toState].name + ')'
                           changeState(successFulTransition.toState)
            		    }
            	    }
                } catch (error) {
                    errorInSchedule = true
                    errorLogInfo = errorLogInfo + ': ' + error
                    log(errorLogInfo)
                }

                if (errorInSchedule && errorState != null && errorState != currentState) {
                    changeState(errorState)
                }
            }

            function changeState(state) {
                log('changing state to: ' + stateInfo[state].name)
            	eval('stateEntryAction'+state+'()')
            	currentState = state
                system.setCurrentState(stateInfo[state].name)
                if (stateInfo[state].isSubStateMachine) {
                    startSubStateMachine(stateInfo[state].externalId, instanceId)
                }
            }
            
            """");

        script.AppendLine();
        script.AppendLine("// Pre-start statemachine action");
        script.AppendLine($"{stateMachine.PreStartAction ?? ""}");

        return script.ToString();
    }

    private static string TransitionMethodName(Transition transition)
    {
        return $"transitionResult{transition.Id.ToString("N")}";
    }

    private static string StateEntryMethodName(State state)
    {
        return $"stateEntryAction{state.Id.ToString("N")}";
    }

    private static State? GetStartState(StateMachine stateMachine)
    {
        var states = stateMachine.States.Where(x => x.IsStartState).ToList();
        if (states.Count == 1)
        {
            return states[0];
        }
        else if (states.Count > 1)
        {
            return null;
        }

        foreach (var state in stateMachine.States)
        {
            if (!stateMachine.Transitions.Any(x => x.ToStateId == state.Id))
            {
                states.Add(state);
            }
        }

        if (states.Count == 1)
        {
            return states[0];
        }

        return null;
    }

    public static bool ValidateModel(StateMachine stateMachine)
    {
        //do we have one start state?
        if (GetStartState(stateMachine) == null)
        {
            return false;
        }

        //one rror state
        var states = stateMachine.States.Where(x => x.IsErrorState).ToList();
        if (states.Count > 1)
        {
            return false;
        }

        return true;
    }
}
