namespace Hasm.Services;

public class VariableService(DataService _dataService)
{
    public Task StartAsync()
    {
        var variables = _dataService.GetVariables();
        return Task.CompletedTask;

    }
}
