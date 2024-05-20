using HomeAssistantStateMachine.Models;

namespace HomeAssistantStateMachine.Services;

public class ClipboardService
{
    public class ClipboardContent
    {
        public List<State> States { get; set; } = [];
        public List<Transition> Transitions { get; set; } = [];
        public StateMachine? StateMachine { get; set; }
    }

    private ClipboardContent? _clipboardContent;

    public ClipboardService()
    {
    }

    public void Copy(ClipboardContent content)
    {
        if (!content.States.Any() && content.StateMachine == null)
        {
            _clipboardContent = null;
        }
        else
        {
            content?.States.ForEach(x => x.StateMachine = null);
            content?.Transitions.ForEach(x => { x.StateMachine = null; x.FromState = null; x.ToState = null; });
            content?.StateMachine?.States.ToList().ForEach(x => x.StateMachine = null);
            content?.StateMachine?.Transitions.ToList().ForEach(x => { x.StateMachine = null; x.FromState = null; x.ToState = null; });
            _clipboardContent = content.CopyObject();
        }
    }

    public bool CanPaste()
    {
        return _clipboardContent != null;
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
