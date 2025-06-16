namespace Hasm.Services;

public interface IClientHandler : IAsyncDisposable
{
    public Models.Client Client { get; }
    public Task StartAsync();
    public Task UpdateAsync(Models.Client client);
    public Task DeleteVariableAsync(Models.Variable variable);
    public Task AddOrUpdateVariableAsync(Models.Variable variable);
}
