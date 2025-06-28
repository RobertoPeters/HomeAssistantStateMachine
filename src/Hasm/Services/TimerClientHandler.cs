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
        public DateTime? Start { get; set; }
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
                    if (int.TryParse(variable.Variable.Data, out var seconds))
                    {
                        cdtimer.Duration = TimeSpan.FromSeconds(seconds);
                        cdtimer.Start = DateTime.UtcNow;
                        cdtimer.IsRunning = true;
                    }
                    else
                    {
                        cdtimer.Duration = null;
                        cdtimer.IsRunning = false;
                    }
                }
                else
                {
                    cdtimer = new CountdownTimer(variable.Variable);
                    if (cdtimer.Duration != null)
                    {
                        cdtimer.Start = DateTime.UtcNow;
                        cdtimer.IsRunning = true;
                    }
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
        //idea: remove timer variables on start to reduce slack variables
        //for now, we just set them to null
        CountdownTimers = new ConcurrentDictionary<int, CountdownTimer>(variables);
        await _variableService.SetVariableValueAsync(
            CountdownTimers.Values
                .Where(x => x.Start != null && x.Duration != null)
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


    private async void CheckCountdownTimers(object? state)
    {
        List<(int variableId, string? value)> updatedValues = [];
        lock (_lockTimer)
        {
            foreach (var timer in CountdownTimers.Values.ToList())
            {
                if (timer.IsRunning)
                {
                    var value = (int)Math.Round((timer.Start!.Value.Add(timer.Duration!.Value) - DateTime.UtcNow).TotalSeconds);
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
            await _variableService.SetVariableValueAsync(updatedValues);
        }
    }

}
