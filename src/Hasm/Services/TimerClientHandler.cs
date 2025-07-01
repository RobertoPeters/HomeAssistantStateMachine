using System.Collections.Concurrent;
using System.Collections.Generic;
using Hasm.Models;

namespace Hasm.Services;

public class TimerClientHandler(Client _client, VariableService _variableService) : IClientHandler
{
    public Client Client => _client;

    public class CountdownTimer
    {
        public Variable Variable { get; set; }
        public DateTime? StartTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool IsRunning { get; set; }

        public CountdownTimer(Variable variable)
        {
            Variable = variable;
            if (int.TryParse(variable.Data, out var seconds))
            {
                Duration = TimeSpan.FromSeconds(seconds);
            }
        }

        public bool Start()
        {
            if (Duration == null)
            {
                return false;
            }
            StartTime = DateTime.UtcNow;
            IsRunning = true;
            return true;
        }

        public void Stop()
        {
            IsRunning = false;
            StartTime = null;
        }
    }

    private ConcurrentDictionary<int, CountdownTimer> CountdownTimers = [];
    private Timer? _timer = null;
    private readonly object _lockTimer = new object();

    public Task AddOrUpdateVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        lock (_lockTimer)
        {
            foreach (var variable in variables)
            {
                if (CountdownTimers.TryGetValue(variable.Variable.Id, out var cdtimer))
                {
                    cdtimer.Variable = variable.Variable;
                    cdtimer.Stop();
                    if (int.TryParse(variable.Variable.Data, out var seconds))
                    {
                        cdtimer.Duration = TimeSpan.FromSeconds(seconds);
                    }
                    else
                    {
                        cdtimer.Duration = null;
                    }
                }
                else
                {
                    cdtimer = new CountdownTimer(variable.Variable);
                    CountdownTimers.TryAdd(variable.Variable.Id, cdtimer);
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteVariableInfoAsync(List<VariableService.VariableInfo> variables)
    {
        foreach (var variable in variables)
        {
            CountdownTimers.TryRemove(variable.Variable.Id, out _);
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    private void Stop()
    {
        lock (_lockTimer)
        {
            _timer?.Dispose();
            _timer = null;
            CountdownTimers.Clear();
        }
    }

    public async Task StartAsync()
    {
        Stop();
        var variables = _variableService.GetVariables()
                .Where(x => x.Variable.ClientId == _client.Id)
                .Select(x => new CountdownTimer(x.Variable))
                .ToDictionary(x => x.Variable.Id, x => x);
        CountdownTimers = new ConcurrentDictionary<int, CountdownTimer>(variables);
        await _variableService.SetVariableValuesAsync(
            CountdownTimers.Values
                .Where(x => x.StartTime != null && x.Duration != null)
                .Select(x => (variableId: x.Variable.Id, value: (string?)null))
                .ToList()
        );
        _timer = new Timer(CheckCountdownTimers, null, 1000, Timeout.Infinite);
    }

    public async Task UpdateAsync(Client client)
    {
        _client = client;
        await StartAsync();
    }

    public async Task<bool> ExecuteAsync(int? variableId, string command, string? parameter)
    {
        if (variableId == null || !CountdownTimers.TryGetValue(variableId.Value, out var timer))
        {
            return false;
        }
        var result = true;
        switch (command.ToLower())
        {
            case "start":
                result = timer.Start();
                if (timer.IsRunning)
                {
                    var value = (int)Math.Round((timer.StartTime!.Value.Add(timer.Duration!.Value) - DateTime.UtcNow).TotalSeconds);
                    if (value < 0)
                    {
                        value = 0;
                    }
                    await _variableService.SetVariableValuesAsync([(variableId: timer.Variable.Id, value: value.ToString())]);
                }
                break;
            case "stop":
                timer.Stop();
                break;
            default:
                result = false;
                break;
        }
        return result;
    }

    private async void CheckCountdownTimers(object? state)
    {
        List<(int variableId, string? value)> updatedValues = [];
        lock (_lockTimer)
        {
            foreach (var timer in CountdownTimers.Values.ToList())
            {
                if (timer.IsRunning)
                {
                    var value = (int)Math.Round((timer.StartTime!.Value.Add(timer.Duration!.Value) - DateTime.UtcNow).TotalSeconds);
                    if (value < 0)
                    {
                        value = 0;
                    }
                    updatedValues.Add((timer.Variable.Id, value.ToString()));
                    if (value == 0)
                    {
                        timer.IsRunning = false;
                    }
                }
            }
            _timer?.Change(1000, Timeout.Infinite);
        }
        if (updatedValues.Any())
        {
            await _variableService.SetVariableValuesAsync(updatedValues);
        }
    }

}
