using Hasm.Models;
using Hasm.Services;
using Microsoft.Data.Sqlite;

namespace Hasm.Repository;

public static class DataRepositoryUpgrades
{
    public class StateMachineV2 : ModelBase
    {
        public string Name { get; set; } = null!;
        public bool Enabled { get; set; }
        public bool IsSubStateMachine { get; set; }
        public string? PreStartAction { get; set; }
        public string? PreScheduleAction { get; set; }
        public List<StateMachineHandler.State> States { get; set; } = [];
        public List<StateMachineHandler.Transition> Transitions { get; set; } = [];
        public List<StateMachineHandler.SubStateMachineParameter> SubStateMachineParameters { get; set; } = [];
        public List<StateMachineHandler.Information> Informations { get; set; } = [];
    }

    public static async Task CheckUpgradesAsync(SqliteConnection connection)
    {
        int targetVersion = 3;

        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        var userVersion = await command.ExecuteScalarAsync();
        await command.DisposeAsync();

        if (userVersion is long version)
        {
            if (version == 0)
            {
                await CheckUpgradeTo2Async(connection);
                await CheckUpgradeTo3Async(connection);
            }
            else if (version == 2)
            {
                await CheckUpgradeTo3Async(connection);
            }
            else if (version == targetVersion)
            {
                return;
            }
        }
        else if (userVersion == null)
        {
            await CheckUpgradeTo2Async(connection);
        }

        command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {targetVersion}";
        await command.ExecuteNonQueryAsync();
        await command.DisposeAsync();
    }

    public static async Task CheckUpgradeTo2Async(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF; DROP TABLE IF EXISTS VariableValues; DROP TABLE IF EXISTS Variables; PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();
        await command.DisposeAsync();

        command = connection.CreateCommand();
        command.CommandText = "SELECT count(1) FROM sqlite_master WHERE type = 'table' AND name = 'HAClients'";
        var result = await command.ExecuteScalarAsync();
        await command.DisposeAsync();

        if (result is long count && count > 0)
        {
            List<Client> clients = [];
            command = connection.CreateCommand();
            command.CommandText = $"select Id, Name, Enabled, Host, Token from HAClients";
            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var record = new Client()
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Enabled = reader.GetBoolean(2),
                    ClientType = ClientType.HomeAssistant,
                    Data = System.Text.Json.JsonSerializer.Serialize(new HAClientHandler.ClientProperties
                    {
                        Host = reader.GetString(3),
                        Token = reader.GetString(4)
                    })
                };
                clients.Add(record);
            }
            await command.DisposeAsync();
            foreach(var record in clients)
            {
                command = connection.CreateCommand();
                command.CommandText = $"insert into Client(Data) values(@data)";
                command.Parameters.AddWithValue("@data", record.ToData());
                await command.ExecuteNonQueryAsync();
                await command.DisposeAsync();
            }
        }

        command = connection.CreateCommand();
        command.CommandText = "SELECT count(1) FROM sqlite_master WHERE type = 'table' AND name = 'StateMachines'";
        result = await command.ExecuteScalarAsync();
        await command.DisposeAsync();

        if (result is long count2 && count2 > 0)
        {
            Dictionary<int, StateMachineV2> stateMachines = [];
            command = connection.CreateCommand();
            command.CommandText = $"select Id, Name, Enabled, PreStartAction, PreScheduleAction from StateMachines";
            var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var record = new StateMachineV2()
                {
                    Name = reader.GetString(1),
                    Enabled = reader.GetBoolean(2),
                    PreStartAction = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PreScheduleAction = reader.IsDBNull(4) ? null : reader.GetString(4),
                };
                stateMachines.Add(id, record);
            }
            await command.DisposeAsync();
            await reader.DisposeAsync();

            Dictionary<(int id, int stateMachineId), StateMachineHandler.State> states = [];
            command = connection.CreateCommand();
            command.CommandText = $"select Id, StateMachineId, Name, IsErrorState, IsStartState, Description, EntryAction, UIData from States";
            reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var stateMachineId = reader.GetInt32(1);
                var record = new StateMachineHandler.State()
                {
                    Id = Guid.NewGuid(),
                    Name = reader.GetString(2),
                    IsErrorState = reader.GetBoolean(3),
                    IsStartState = reader.GetBoolean(4),
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    EntryAction = reader.IsDBNull(6) ? null : reader.GetString(6),
                    UIData = reader.IsDBNull(7) ? null : reader.GetString(7),
                };
                states.Add((id, stateMachineId), record);
            }
            await command.DisposeAsync();
            await reader.DisposeAsync();

            Dictionary<(int stateMachineId, int fromStateId, int toStateId), StateMachineHandler.Transition> transitions = [];
            command = connection.CreateCommand();
            command.CommandText = $"select Id, Description, Condition, UIData, StateMachineId, FromStateId, ToStateId from Transitions";
            reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var stateMachineId = reader.GetInt32(4);
                var fromStateId = reader.GetInt32(5);
                var toStateId = reader.GetInt32(6);
                var record = new StateMachineHandler.Transition()
                {
                    Id = Guid.NewGuid(),
                    Description = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Condition = reader.IsDBNull(2) ? null : reader.GetString(2),
                    UIData = reader.IsDBNull(3) ? null : reader.GetString(3),
                };
                transitions.Add((stateMachineId, fromStateId, toStateId), record);
            }
            await command.DisposeAsync();
            await reader.DisposeAsync();

            foreach (var stateMachine in stateMachines)
            {
                foreach (var state in states.Where(s => s.Key.stateMachineId == stateMachine.Key))
                {
                    stateMachine.Value.States.Add(state.Value);
                }
                foreach (var transition in transitions.Where(s => s.Key.stateMachineId == stateMachine.Key))
                {
                    var fromState = states.Where(x => x.Key.id == transition.Key.fromStateId).Select(x => x.Value).FirstOrDefault();
                    var toState = states.Where(x => x.Key.id == transition.Key.toStateId).Select(x => x.Value).FirstOrDefault();
                    if (fromState != null && toState != null)
                    {
                        transition.Value.FromStateId = fromState.Id;
                        transition.Value.ToStateId = toState.Id;
                        stateMachine.Value.Transitions.Add(transition.Value);
                    }
                }

                command = connection.CreateCommand();
                command.CommandText = $"insert into StateMachine(Data) values(@data)";
                command.Parameters.AddWithValue("@data", stateMachine.Value.ToData());
                await command.ExecuteNonQueryAsync();
                await command.DisposeAsync();

            }
        }

        command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF; DROP TABLE IF EXISTS HAClients; DROP TABLE IF EXISTS MqttClients; DROP TABLE IF EXISTS States; DROP TABLE IF EXISTS Transitions; DROP TABLE IF EXISTS StateMachines; PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();
        await command.DisposeAsync();
    }

    public static async Task CheckUpgradeTo3Async(SqliteConnection connection)
    {
        var dataBuffer = new byte[10_000_000];

        var command = connection.CreateCommand();
        command.CommandText = "SELECT count(1) FROM sqlite_master WHERE type = 'table' AND name = 'StateMachine'";
        var result = await command.ExecuteScalarAsync();
        await command.DisposeAsync();

        if (result is long count && count > 0)
        {
            command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF; DROP TABLE IF EXISTS VariableValues; DROP TABLE IF EXISTS Variables; PRAGMA foreign_keys = ON;";
            await command.ExecuteNonQueryAsync();
            await command.DisposeAsync();

            List<Automation> automations = [];
            command = connection.CreateCommand();
            command.CommandText = $"select Id, Data from StateMachine";
            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                StateMachineV2 stateMachineV2 = null!;
                var id = reader.GetInt32(0);
                var dataLength = reader.GetBytes(1, 0, dataBuffer, 0, dataBuffer.Length);
                if (dataLength > 0)
                {
                    stateMachineV2 = StateMachineV2.FromData<StateMachineV2>(id, dataBuffer, (int)dataLength);
                }
                else
                {
                    stateMachineV2 = new ();
                }
                var record = new Automation()
                {
                    Id = id,
                    Name = stateMachineV2.Name,
                    Enabled = stateMachineV2.Enabled,
                    IsSubAutomation = stateMachineV2.IsSubStateMachine,
                    AutomationType = AutomationType.StateMachine,
                    Data = System.Text.Json.JsonSerializer.Serialize(new StateMachineHandler.AutomationProperties
                    {
                        States = stateMachineV2.States,
                        Transitions = stateMachineV2.Transitions,
                        Informations = stateMachineV2.Informations,
                        SubStateMachineParameters = stateMachineV2.SubStateMachineParameters,
                        PreScheduleAction = stateMachineV2.PreScheduleAction,
                        PreStartAction = stateMachineV2.PreStartAction
                    })
                };
                automations.Add(record);
            }
            await command.DisposeAsync();
            foreach (var record in automations)
            {
                command = connection.CreateCommand();
                command.CommandText = $"insert into Automation(Data) values(@data)";
                command.Parameters.AddWithValue("@data", record.ToData());
                await command.ExecuteNonQueryAsync();
                await command.DisposeAsync();
            }

        }

        command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF; DROP TABLE IF EXISTS StateMachine; PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();
        await command.DisposeAsync();
    }
}

