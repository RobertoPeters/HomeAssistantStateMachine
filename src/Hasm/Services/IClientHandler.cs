namespace Hasm.Services;

public interface IClientHandler : IAsyncDisposable
{
    Models.Client Client { get; }
    Task StartAsync();
    Task UpdateAsync(Models.Client client);
    Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables);
    Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables);
    Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3);
}
