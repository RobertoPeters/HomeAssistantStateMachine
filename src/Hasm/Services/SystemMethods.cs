using Jint.Native;
using System.Collections.Concurrent;

namespace Hasm.Services;

public class SystemMethods
{
    private readonly ClientService _clientService;
    private readonly DataService _dataService;
    private readonly StateMachineHandler _stateMachineHandler;
    private readonly ConcurrentDictionary<int, Models.Variable> _variables;
    private readonly ConcurrentDictionary<int, Models.Client> _clients;

    public SystemMethods(ClientService clientService, DataService dataService, StateMachineHandler stateMachineHandler)
    {

        _clientService = clientService;
        _dataService = dataService;
        _stateMachineHandler = stateMachineHandler;
        var variables = dataService.GetVariables().Where(x => x.StateMachineId == null || x.StateMachineId == stateMachineHandler.StateMachine.Id);
        _variables = new ConcurrentDictionary<int, Models.Variable>(variables.ToDictionary(v => v.Id));
        _clients = new ConcurrentDictionary<int, Models.Client>(dataService.GetClients().ToDictionary(c => c.Id));
    }

    public int createVariable(string name, int clientId, bool isStateMachineVariable, bool persistant, string? data, JsValue[]? mockingOptions)
    {
        if (!_clients.TryGetValue(clientId, out var client) || !client.Enabled)
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

    public bool setVariableValue(int variableId, string? value)
    {
        if (!_variables.TryGetValue(variableId, out var variable))
        {
            return false;
        }
        return _clientService.SetVariableValueAsync(variableId, variable.ClientId, value).Result;
    }

    public int GetClientId(string name)
    {
        var client = _clients.Values.FirstOrDefault(c => c.Name == name);
        return client?.Id ?? -1;
    }

    public const string SystemScript = """"

    // returns the client id or -1 if not found
    getClientId = function(name) {
        return system.getClientId(name)
    }

    // creates a variable and returns the variable id (-1 if it fails)
    createVariable = function(name, clientId, isStateMachineVariable, persistant, data, mockingOptions) {
        return system.createVariable(name, clientId, isStateMachineVariable, persistant, data, mockingOptions)
    }

    var genericClientId = getClientId('Generic')
    
    """";
}
