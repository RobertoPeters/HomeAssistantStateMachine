using Hasm.Models;
using Hasm.Services.Interfaces;
using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;

namespace Hasm.Services.Clients;

public class SqliteDatabaseClientHandler(Client _client, VariableService _variableService) : IClientHandler
{
    public class ClientProperties
    {
        public string? Filename { get; set; }
        public string? OnStartQueries { get; set; }
    }

    private ClientProperties _clientProperties = new();
    private string ConnectionString => $"Data Source=./Settings/{_clientProperties.Filename}";
    private bool _databaseActionEnabled = true;

    public Client Client => _client;


    public Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        return Task.CompletedTask;
    }

    public Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async Task StartAsync()
    {
        if (!string.IsNullOrWhiteSpace(_client.Data))
        {
            _clientProperties = System.Text.Json.JsonSerializer.Deserialize<ClientProperties>(_client.Data) ?? new();
        }
 
        _databaseActionEnabled = false;
        _clientProperties.Filename = _clientProperties.Filename?.Replace("/", "").Replace("\\", "");
        if (Client.Enabled && !string.IsNullOrWhiteSpace(_clientProperties.Filename))
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                if (!string.IsNullOrWhiteSpace(_clientProperties.OnStartQueries))
                {
                    using var sqlcommand = connection.CreateCommand();
                    sqlcommand.CommandText = _clientProperties.OnStartQueries;
                    await sqlcommand.ExecuteNonQueryAsync();
                }
                _databaseActionEnabled = true;
            }
            catch
            {
                //nothing
            }
        }
    }

    public async Task UpdateAsync(Client client)
    {
        _client = client;
        await StartAsync();
    }

    public async Task<bool> ExecuteAsync(int? variableId, string command, object? parameter1, object? parameter2, object? parameter3)
    {
        if (!_databaseActionEnabled)
        {
            return false;
        }

        var result = false;
        switch (command.ToLower())
        {
            case "nonquery":
                if (parameter1 != null)
                {
                    using var connection = new SqliteConnection(ConnectionString);
                    await connection.OpenAsync();
                    using var sqlcommand = connection.CreateCommand();
                    sqlcommand.CommandText = parameter1.ToString();
                    await sqlcommand.ExecuteNonQueryAsync();
                    result = true;
                }
                break;
            case "scalar":
                if (parameter1 != null && variableId != null)
                {
                    using var connection = new SqliteConnection(ConnectionString);
                    await connection.OpenAsync();
                    using var sqlcommand = connection.CreateCommand();
                    sqlcommand.CommandText = parameter1.ToString();
                    var v = await sqlcommand.ExecuteScalarAsync();
                    result = await _variableService.SetVariableValuesAsync([(variableId.Value, v?.ToString())]);
                }
                break;
            case "reader":
                if (parameter1 != null && variableId != null)
                {
                    using var connection = new SqliteConnection(ConnectionString);
                    await connection.OpenAsync();
                    using var sqlcommand = connection.CreateCommand();
                    sqlcommand.CommandText = parameter1.ToString();
                    var reader = await sqlcommand.ExecuteReaderAsync();
                    var sr = Serialize(reader);
                    var v = System.Text.Json.JsonSerializer.Serialize(sr);
                    result = await _variableService.SetVariableValuesAsync([(variableId.Value, v)]);
                }
                break;
        }
        return result;
    }

    private static IEnumerable<Dictionary<string, object>> Serialize(SqliteDataReader reader)
    {
        var results = new List<Dictionary<string, object>>();
        var cols = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            cols.Add(reader.GetName(i));
        }

        while (reader.Read())
        {
            results.Add(SerializeRow(cols, reader));
        }

        return results;
    }

    private static Dictionary<string, object> SerializeRow(IEnumerable<string> cols, SqliteDataReader reader)
    {
        var result = new Dictionary<string, object>();
        foreach (var col in cols)
        {
            result.Add(col, reader[col]);
        }
        return result;
    }
}
