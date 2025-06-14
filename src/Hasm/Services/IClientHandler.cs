namespace Hasm.Services;

public interface IClientHandler : IAsyncDisposable
{
    public Models.Client Client { get; }
    public Task StartAsync();
    public Task UpdateAsync(Models.Client client);
    public Task DeleteVariableAsync(string name);
    public Task AddVariableAsync(Models.Variable variable);
    public Task UpdateVariableAsync(Models.Variable variable);
}
