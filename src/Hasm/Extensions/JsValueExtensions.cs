using Jint.Native;

public static class JsValueExtensions
{
    private static System.Text.Json.JsonSerializerOptions JsValueJsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true
    };

    public static string JsValueToString(this JsValue? jsValue, bool autoStringQuotes = false)
    {
        string result;
        if (jsValue == null)
        {
            result = "null";
        }
        else
        {
            var obj = jsValue.ToObject();
            if (obj == null)
            {
                result = "null";
            }
            else if (obj is string s)
            {
                if (autoStringQuotes)
                {
                    result = $"'{s}'";
                }
                else
                {
                    result = s;
                }
            }
            else if (obj.GetType().IsValueType)
            {
                result = obj.ToString()!;
            }
            else
            {
                result = System.Text.Json.JsonSerializer.Serialize(obj, JsValueJsonOptions);
            }
        }
        return result!;
    }
}

