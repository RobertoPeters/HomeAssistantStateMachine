using HomeAssistantStateMachine.Data;
using HomeAssistantStateMachine.Models;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HomeAssistantStateMachine.Services;

public partial class VariableService
{
    public event EventHandler? CountdownTimerChanged;

    public class CountdownTimer
    {
        public int Id { get; private set; }
        public int StateMachineId { get; private set; }
        public string Name { get; private set; }
        public DateTime Start { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        
        public CountdownTimer(int id, int machineStateId, string name, int seconds)
        {
            Duration = TimeSpan.FromSeconds(seconds);
            Name = name;
            StateMachineId = machineStateId;
            Id = id;
        }

        public bool IsExpired => (DateTime.UtcNow - Start) > Duration;
        public int Value => (int)((Start.Add(Duration)) - DateTime.UtcNow).TotalSeconds;
    }

    private Dictionary<int, Dictionary<string, CountdownTimer>> CountdownTimers = [];
    private Timer? _timer = null;
    private readonly object _lockTimer = new object();
    private int _timerId = 0;

    public bool CreateCountdownTimer(int statemachineId, string name, int seconds)
    {
        var result = true;
        lock (_lockTimer)
        {
            if (!CountdownTimers.TryGetValue(statemachineId, out var timers))
            {
                timers = [];
                CountdownTimers.Add(statemachineId, timers);
            }
            if (!timers.TryGetValue(name, out var timer))
            {
                _timerId--;
                if (_timerId == int.MinValue)
                {
                    _timerId = -1;
                }
                timer = new CountdownTimer(_timerId, statemachineId, name, seconds);
                timers.Add(name, timer);
            }
            timer.Start = DateTime.UtcNow;
            timer.Duration = TimeSpan.FromSeconds(seconds);
            if (_timer == null)
            {
                _timer = new Timer(CheckCountdownTimers, null, 1000, Timeout.Infinite);
            }
        }
        return result;
    }

    public List<CountdownTimer> AllCountDownTimers()
    {
        List<CountdownTimer> result = [];
        lock (_lockTimer)
        {
            foreach (var timers in CountdownTimers.Values)
            {
                result.AddRange(timers.Values);
            }
        }
        return result;
    }

    private CountdownTimer? GetCountdownTimer(int statemachineId, string name)
    {
        if (CountdownTimers.TryGetValue(statemachineId, out var timers))
        {
            if (timers.TryGetValue(name, out var timer))
            {
                return timer;
            }
        }
        return null;
    }

    public int GetCountdownTimerValue(int statemachineId, string name)
    {
        var result = 0;
        lock (_lockTimer)
        {
            var timer = GetCountdownTimer(statemachineId, name);
            result = timer?.Value ?? 0;
        }
        return result;
    }

    public bool GetCountdownTimerExpired(int statemachineId, string name)
    {
        var result = true;
        lock (_lockTimer)
        {
            var timer = GetCountdownTimer(statemachineId, name);
            result = timer?.IsExpired ?? true;
        }
        return result;
    }

    private void CheckCountdownTimers(object? state)
    {
        lock (_lockTimer)
        {
            foreach (var timers in CountdownTimers.Values.ToList())
            {
                foreach(var timer in timers.Values.ToList())
                {
                    if (timer.IsExpired)
                    {
                        timers.Remove(timer.Name);
                        if (timers.Count == 0)
                        {
                            CountdownTimers.Remove(timer.StateMachineId);

                            if (CountdownTimers.Count == 0)
                            {
                                _timer!.Dispose();
                                _timer = null;
                            }
                        }
                    }
                }
            }
            _timer?.Change(1000, Timeout.Infinite);
        }

        CountdownTimerChanged?.Invoke(this, EventArgs.Empty);
    }
}
