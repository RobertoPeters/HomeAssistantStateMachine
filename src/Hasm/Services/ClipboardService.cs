using Hasm.Models;
using Hasm.Services.Automations;

namespace Hasm.Services;

public class ClipboardService
{
    public class ClipboardContent
    {
        public List<StateMachineHandler.State> States { get; set; } = [];
        public List<StateMachineHandler.Transition> Transitions { get; set; } = [];
        public List<StateMachineHandler.Information> Informations { get; set; } = [];
        public Automation? Automation { get; set; }
    }

    private ClipboardContent? _clipboardContent;

    public ClipboardService()
    {
    }

    public void Copy(ClipboardContent content)
    {
        if (!content.States.Any() && !content.Informations.Any() && content.Automation == null)
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
