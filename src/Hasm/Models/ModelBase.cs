using System.Text.Json.Serialization;

namespace Hasm.Models;

public class ModelBase
{
    [JsonIgnore]
    public int Id { get; set; }

    public byte[] ToData()
    {
        var jsonText = System.Text.Json.JsonSerializer.Serialize(this, this.GetType());
        return System.Text.UTF8Encoding.UTF8.GetBytes(jsonText);
    }

    public static T FromData<T>(int id, byte[] data, int dataLength) where T : ModelBase, new()
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.UTF8Encoding.UTF8.GetString(data, 0, dataLength))!;
        result.Id = id;
        return result;
    }
}
