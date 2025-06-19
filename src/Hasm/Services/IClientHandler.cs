namespace Hasm.Services;

public interface IClientHandler : IAsyncDisposable
{
    Models.Client Client { get; }
    Task StartAsync();
    Task UpdateAsync(Models.Client client);
    Task DeleteVariableAsync(Models.Variable variable);
    Task AddOrUpdateVariableAsync(Models.Variable variable);
    Task<bool> SetVariableValueAsync(int variableId, string value);
}
