using Hasm.Models;
using Hasm.Services.Automations.Flow;
using Hasm.Services.Automations.StateMachine;

namespace Hasm.Services;

public class ClipboardService
{
    public class ClipboardContent
    {
        public List<StateMachineHandler.State> StateMachineStates { get; set; } = [];
        public List<StateMachineHandler.Transition> StateMachineTransitions { get; set; } = [];
        public List<StateMachineHandler.Information> StateMachineInformations { get; set; } = [];
        public List<Step> FlowSteps { get; set; } = [];
        public List<Information> FlowInformations { get; set; } = [];
        public Automation? StateMachineAutomation { get; set; }
        public Automation? FlowAutomation { get; set; }
    }

    private ClipboardContent? _clipboardContent;

    public ClipboardService()
    {
    }

    public void Copy(ClipboardContent content)
    {
        if (!content.FlowSteps.Any() && !content.StateMachineStates.Any() && !content.StateMachineInformations.Any() && !content.FlowInformations.Any() && content.StateMachineAutomation == null && content.FlowAutomation == null)
        {
            _clipboardContent = null;
        }
        else
        {
            _clipboardContent = content.CopyObject();
        }
    }

    public bool CanPaste(AutomationType automationType)
    {
        if (_clipboardContent == null)
        {
            return false;
        }
        if (automationType == AutomationType.Flow)
        {
            return _clipboardContent.FlowAutomation != null || _clipboardContent.FlowInformations.Any() || _clipboardContent.FlowSteps.Any();
        }
        else if (automationType == AutomationType.StateMachine)
        {
            return _clipboardContent.StateMachineAutomation != null || _clipboardContent.StateMachineInformations.Any() || _clipboardContent.StateMachineStates.Any() || _clipboardContent.StateMachineTransitions.Any();
        }
        return false;
    }

    public ClipboardContent? Paste()
    {
        if (_clipboardContent == null)
        {
            return null;
        }
        return _clipboardContent.CopyObject();
    }
}
