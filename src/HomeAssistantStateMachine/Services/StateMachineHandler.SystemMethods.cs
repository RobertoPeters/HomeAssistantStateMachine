using HomeAssistantStateMachine.Models;
using Jint.Native;
using System.Collections.Generic;
using System.Text;

namespace HomeAssistantStateMachine.Services;

public partial class StateMachineHandler
{
    private SystemMethods NewSystemMethods => new SystemMethods(_variableService, this);

    public class SystemMethods
    {
        private readonly VariableService _variableService;
        private readonly StateMachineHandler _stateMachineHandler;

        public SystemMethods(VariableService variableService, StateMachineHandler stateMachineHandler)
        {
            _variableService = variableService;
            _stateMachineHandler = stateMachineHandler;
        }

        public object? getVariableValue(string variable)
        {
            return _variableService.GetVariableValue(variable);
        }

        public bool createCountdownTimer(string name, int seconds)
        {
            return true;
            //return _variableService.CreateCountdownTimer(_stateMachineHandler.StateMachine.Id, name, seconds);
        }

        public bool countdownTimerExpired(string name)
        {
            return _variableService.GetCountdownTimerExpired(_stateMachineHandler.StateMachine.Id, name);
        }
    }

    public const string SystemScript = """"
        getValue = function(variable) {
            return system.getVariableValue(variable);
        }

        createTimer = function(name, seconds) {
            return system.createCountdownTimer(name, seconds);
        }

        timerExpired = function(name) {
            return system.countdownTimerExpired(name);
        }
        """";
}

