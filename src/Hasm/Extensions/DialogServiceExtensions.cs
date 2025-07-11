using Hasm.Components.Dialogs;

public static partial class DialogServiceExtensions
{
    public async static Task ShowDialogAsync<TDialog>(
        this Radzen.DialogService service,
        string title,
        params Action<TDialog>?[]? setParameters)
        where TDialog : DialogOptionsBase, new()
        => await service.ShowDynamicDialogAsync(title, setParameters);

    public async static Task<TResult?> ShowDialogAsync<TDialog, TResult>(
        this Radzen.DialogService service,
        string title,
        params Action<TDialog>?[]? setParameters)
        where TDialog : ResultDialogBase<TResult>, new()
    {
        var result = await service.ShowDynamicDialogAsync(title, setParameters);

        if (result is TResult resultValue)
        {
            return resultValue;
        }

        return default;
    }

    private async static Task<dynamic?> ShowDynamicDialogAsync<TDialog>(
        this Radzen.DialogService service,
        string title,
        params Action<TDialog>?[]? setParameters)
        where TDialog : DialogOptionsBase, new()
    {
        var instance = GetPreRenderedInstance(setParameters);

#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
        instance.Title = title;
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

        var options = instance.GetDialogOptions();

        //options.ShowClose = false;
        //options.ShowTitle = false;

        var parameters = instance.GetParameters();

        return await service.OpenAsync<TDialog>(title, parameters, options);
    }

    private static TDialog GetPreRenderedInstance<TDialog>(
        params Action<TDialog>?[]? setParameters)
        where TDialog : DialogOptionsBase, new()
    {
        var instance = new TDialog();

        if (setParameters == null || !setParameters.Any())
        {
            return instance;
        }

        foreach (var setParameter in setParameters)
        {
            setParameter?.Invoke(instance);
        }

        return instance;
    }
}
