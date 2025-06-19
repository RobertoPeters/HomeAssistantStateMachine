using Hasm.Models;
using System.Collections.Concurrent;

namespace Hasm.Services;

public class GenericClientHandler(Client _client, DataService _dataService) : IClientHandler
{
    private sealed class VariableInfo
    {
        public Variable Variable { get; set; } = null!;
        public VariableValue VariableValue { get; set; } = null!;
    }

    public Client Client => _client;

    private ConcurrentDictionary<int, VariableInfo> _variables = [];

    public async Task<bool> SetVariableValueAsync(int variableId, string value)
    {
        if (_variables.TryGetValue(variableId, out var variableInfo))
        {
            variableInfo.VariableValue.Value = value;
            variableInfo.VariableValue.Update = DateTime.UtcNow;
            await _dataService.AddOrUpdateVariableValueAsync(variableInfo.VariableValue);
            return true;
        }
        return false;
    }

    public Task AddOrUpdateVariableAsync(Variable variable)
    {
        VariableValue? variableValue = _dataService.GetVariableValues().FirstOrDefault(x => x.VariableId == variable.Id);
        variableValue ??= new()
        {
            Update = DateTime.UtcNow,
            VariableId = variable.Id,
            Value = variable.Data
        };
        if (!variable.Persistant)
        {
            variableValue.Value = variable.Data;
        }
        var varInfo = new VariableInfo()
        {
            Variable = variable,
            VariableValue = variableValue
        };

        _variables.AddOrUpdate(variable.Id, varInfo, (_, _) => varInfo);
        return Task.CompletedTask;
    }

    public Task DeleteVariableAsync(Variable variable)
    {
        _variables.TryRemove(variable.Id, out _);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async Task StartAsync()
    {
        if (_client.Enabled)
        {
            var variableValues = _dataService.GetVariableValues().ToDictionary(x => x.VariableId, x => x);
            _dataService.GetVariables()
               .Where(v => v.ClientId == _client.Id)
               .ToList()
               .ForEach(variable =>
               {
                   VariableValue? variableValue = null;
                   variableValues.TryGetValue(variable.Id, out variableValue);
                   variableValue ??= new()
                   {
                       Update = DateTime.UtcNow,
                       VariableId = variable.Id,
                       Value = variable.Data
                   };
                   _variables.TryAdd(variable.Id, new VariableInfo()
                   {
                       Variable = variable,
                       VariableValue = variableValue
                   });
               });
            foreach (var variable in _variables.Values.Where(x => x.VariableValue.Id == 0).ToList())
            {
                await _dataService.AddOrUpdateVariableValueAsync(variable.VariableValue);
            }
        }
     }

    public async Task UpdateAsync(Client client)
    {
        _variables = [];
        _client = client;
        await StartAsync();
    }
}
