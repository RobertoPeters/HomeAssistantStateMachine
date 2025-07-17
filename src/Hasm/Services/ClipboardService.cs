using Hasm.Models;

namespace Hasm.Services;

public class ClipboardService
{
    public class ClipboardContent
    {
        public List<State> States { get; set; } = [];
        public List<Transition> Transitions { get; set; } = [];
        public List<Information> Informations { get; set; } = [];
        public StateMachine? StateMachine { get; set; }
    }

    private ClipboardContent? _clipboardContent;

    public ClipboardService()
    {
    }

    public void Copy(ClipboardContent content)
    {
        if (!content.States.Any() && !content.Informations.Any() && content.StateMachine == null)
        {
            _clipboardContent = null;
        }
        else
        {
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
