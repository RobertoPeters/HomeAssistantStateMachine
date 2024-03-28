using Microsoft.AspNetCore.Components;
using System.Reflection;

namespace HomeAssistantStateMachine.Components;

public abstract class ResultDialogBase<TResult> : DialogOptionsBase
{
    protected void Close(TResult? result) => DialogService.Close(result);
}

public abstract class DialogBase : DialogOptionsBase
{
    protected void Close() => DialogService.Close();

    public enum DialogResult
    {
        None = 0,
        Failure = 1,
        Success = 2,
    }
}

public class DialogOptionsBase : ComponentBase
{
     [Inject] private Radzen.DialogService? Service { get; set; }

    protected Radzen.DialogService DialogService => Service!;

    /// <summary>
    /// Sets the dialog's heading.
    /// </summary>
    [Parameter] public virtual string? Title { get; set; }

    /// <summary>
    /// Overrides the values for <see cref="CloseDialogOnOverlayClick"/>, <see cref="CloseDialogOnEsc"/>
    /// and <see cref="ShowClose"/> to 'false'.
    /// </summary>
    protected virtual bool IsPersistent { get; set; } = true;

    /// <summary>
    /// Overrides the values for <see cref="Resizable"/> and <see cref="Draggable"/> to 'false'.
    /// </summary>
    protected virtual bool IsFixed { get; set; } = false;

    // Dialog close options
    /// <summary>Default value: true</summary>
    protected virtual bool CloseDialogOnOverlayClick { get; set; } = true;
    /// <summary>Default value: true</summary>
    protected virtual bool CloseDialogOnEsc { get; set; } = true;
    /// <summary>Default value: true</summary>
    protected virtual bool ShowClose { get; set; } = true;

    // Size and position
    /// <summary>Default value: false</summary>
    protected virtual bool Resizable { get; set; } = false;
    /// <summary>Default value: false</summary>
    protected virtual bool Draggable { get; set; } = false;
    protected virtual int? HeightInPixels { get; set; }
    protected virtual int? WidthInPixels { get; set; }
    protected virtual string? Height { get; set; }
    protected virtual string? Width { get; set; }
    protected virtual string? Bottom { get; set; }
    protected virtual string? Left { get; set; }
    protected virtual string? Top { get; set; }

    // Styles
    protected virtual string? CssClass { get; set; }
    protected virtual string? Style { get; set; }

    // Other options
    protected virtual RenderFragment<Radzen.DialogService>? ChildContent { get; set; }
    /// <summary>Default value: true</summary>
    protected virtual bool AutoFocusFirstElement { get; set; } = false;
    /// <summary>Default value: true</summary>
    protected virtual bool ShowTitle { get; set; } = true;
    protected virtual bool HasSidebar { get; set; } = false;
    protected virtual bool HasProgressSteps { get; set; } = false;

    public void SetIsPersistent(bool isPersistent) => IsPersistent = isPersistent;

    public bool GetIsPersistent() => IsPersistent;
    public bool GetShowTitle() => ShowTitle;
    public bool GetShowClose() => ShowClose;

    /// <summary>
    /// Retrieves the dialog options for the current dialog instance.
    /// </summary>
    /// <returns>The <see cref="Radzen.DialogOptions"/></returns>
    public Radzen.DialogOptions GetDialogOptions()
        => new()
        {
            CloseDialogOnOverlayClick = !IsPersistent && CloseDialogOnOverlayClick,
            CloseDialogOnEsc = !IsPersistent && CloseDialogOnEsc,

            Resizable = !IsFixed && Resizable,
            Draggable = !IsFixed && Draggable,
            Height = Height ?? HeightInPixelsAsString,
            Width = Width ?? WidthInPixelsAsString,
            Bottom = Bottom,
            Left = Left,
            Top = Top,

            CssClass = $"{CssClass} {(HasSidebar ? "echo-dialog-with-sidebar" : string.Empty)} {(HasProgressSteps ? "echo-steps-dialog" : string.Empty)}",
            Style = Style,

            ChildContent = ChildContent,
            AutoFocusFirstElement = AutoFocusFirstElement,

            ShowClose = false,
            ShowTitle = false,
        };

    /// <summary>
    /// Retrieves the set parameters names and their current values as a dictionary.
    /// </summary>
    /// <returns>The dictionary containing all parameters names and current values.</returns>
    public Dictionary<string, object?> GetParameters()
        => this.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CustomAttributes.Any(attribute => attribute.AttributeType == typeof(ParameterAttribute)))
            .Select(property => new KeyValuePair<string, object?>(property.Name, property.GetValue(this)))
            .ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);

    public virtual Task<bool> CanCloseAsync() => Task.FromResult(true);

    private string? WidthInPixelsAsString
        => WidthInPixels.HasValue
            ? $"{WidthInPixels}px"
            : null;

    private string? HeightInPixelsAsString
        => HeightInPixels.HasValue
            ? $"{HeightInPixels}px"
            : null;
}
