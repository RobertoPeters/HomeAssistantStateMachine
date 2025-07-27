namespace Hasm.Services.Interfaces;

public interface IAutomationHandler : IDisposable
{
    Models.Automation Automation { get; }
    void TriggerProcess();
    void Start();
    Task UpdateAsync(Models.Automation automation);
    Task Handle(List<VariableService.VariableValueInfo> variableValueInfos);
    Task Handle(List<VariableService.VariableInfo> variableInfos);
}
