namespace Hasm.Models;

public class Client: ModelBase
{
    public string Name { get; set; } = null!;
    public bool Enabled { get; set; }
    public ClientType ClientType { get; set; }
    public string Data { get; set; } = null!;
}
