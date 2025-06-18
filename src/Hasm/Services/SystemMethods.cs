using Jint.Native;
using System.Collections.Concurrent;

namespace Hasm.Services;

public class SystemMethods
{
    private readonly VariableService _variableService;
    private readonly DataService _dataService;
    private readonly StateMachineHandler _stateMachineHandler;
    private readonly ConcurrentDictionary<int, Models.Variable> _variables;
    private readonly ConcurrentDictionary<string, Models.Client> _clients;

    public SystemMethods(VariableService variableService, DataService dataService, StateMachineHandler stateMachineHandler)
    {

        _variableService = variableService;
        _dataService = dataService;
        _stateMachineHandler = stateMachineHandler;
        var variables = dataService.GetVariables().Where(x => x.StateMachineId == null || x.StateMachineId == stateMachineHandler.StateMachine.Id);
        _variables = new ConcurrentDictionary<int, Models.Variable>(variables.ToDictionary(v => v.Id));
        _clients = new ConcurrentDictionary<string, Models.Client>(dataService.GetClients().ToDictionary(c => c.Name));
    }

    public int createVariable(string name, bool isStateMachineVariable, bool persistant, string clientName, string? data, JsValue[]? mockingOptions)
    {
        if (!_clients.TryGetValue(clientName, out var client))
        {
            return -1;
        }
        var variable = _variables.Values.FirstOrDefault(x => x.Name == name
        && (!isStateMachineVariable || x.StateMachineId == _stateMachineHandler.StateMachine.Id)
        && (isStateMachineVariable || x.StateMachineId == null)
        && client.Id == x.ClientId
        );

        if (variable != null && string.Compare(data, variable.Data) == 0)
        {
            return variable.Id;
        }

        if (variable == null)
        {
            variable = new()
            {
                 ClientId = client.Id,
                 Name = name,
                 StateMachineId = isStateMachineVariable ? _stateMachineHandler.StateMachine.Id : null,
                 Persistant = persistant
            };
        }

        variable.Data = data;
        variable.MockingValues = null; //todo
        _dataService.AddOrUpdateVariableAsync(variable).Wait();

        return variable.Id;
    }

    public const string SystemScript = """"

    createVariable = function(name) {
        return system.createVariable(name);
    }
    
    """";
}
