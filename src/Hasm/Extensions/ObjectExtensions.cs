public static class ObjectExtensions
{
    public static T? CopyObject<T>(this T target) where T : new()
    {
        var result = default(T);
        if (target != null)
        {
            var s = System.Text.Json.JsonSerializer.Serialize(target);
            if (s != null)
            {
                result = (T?)System.Text.Json.JsonSerializer.Deserialize(s, typeof(T));
                if (result is Hasm.Services.Automations.Flow.FlowHandler.AutomationProperties flowProperties)
                {
                    flowProperties.CreateStepsFromStepDatas();
                }
            }
        }
        return result;
    }

    public static TResult? CopyObjectToOtherType<T, TResult>(this T target) where TResult : new()
    {
        var result = default(TResult);
        if (target != null)
        {
            var s = System.Text.Json.JsonSerializer.Serialize(target);
            if (s != null)
            {
                result = (TResult?)System.Text.Json.JsonSerializer.Deserialize(s, typeof(TResult));
            }
        }
        return result;
    }

    public static string? Truncate<T>(this T target, int maxLength)
    {
        switch (target)
        {
            case string s:
                {
                    if (s.Length > maxLength)
                    {
                        s = s.Substring(0, maxLength);
                    }
                    return s;
                }
            default:
                {
                    if (target == null)
                    {
                        return null;
                    }
                    var s = target.ToString();
                    if (s != null && s.Length > maxLength)
                    {
                        s = s.Substring(0, maxLength);
                    }
                    return s;

                }
        }
    }

}

