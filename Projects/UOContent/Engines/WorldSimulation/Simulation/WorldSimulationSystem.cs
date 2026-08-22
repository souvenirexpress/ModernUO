using System;
using System.Collections.Generic;

namespace Server.Engines.WorldSimulation;

public static class WorldSimulationSystem
{
    private static readonly List<WorldSimulationItem> _activeItems = [];
    private static readonly HashSet<WorldSimulationItem> _activeSet = [];
    private static TimerExecutionToken _timerToken;

    public static int ActiveCount => _activeItems.Count;

    public static void Initialize()
    {
        _timerToken.Cancel();
        Timer.StartTimer(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0), Tick, out _timerToken);
    }

    public static void RegisterIfActive(WorldSimulationItem item)
    {
        if (item?.Deleted != false || !NeedsUpdates(item, new EnvironmentContext()))
        {
            Unregister(item);
            return;
        }

        if (_activeSet.Add(item))
        {
            _activeItems.Add(item);
        }
    }

    public static void Unregister(WorldSimulationItem item)
    {
        if (item != null)
        {
            _activeSet.Remove(item);
            _activeItems.Remove(item);
        }
    }

    public static bool NeedsUpdates(IWorldSimulatedObject target, EnvironmentContext environment)
    {
        var state = target.State;
        return state.IsBurning || Math.Abs(state.Temperature - environment.AmbientTemperature) >= 0.5 ||
               state.Moisture > WorldSimulationThresholds.DryMoisture;
    }

    public static void Advance(IWorldSimulatedObject target, TimeSpan elapsed, EnvironmentContext environment)
    {
        if (target?.Item?.Deleted != false || elapsed <= TimeSpan.Zero)
        {
            return;
        }

        var seconds = Math.Min(elapsed.TotalSeconds, 10.0);
        var state = target.State;
        var material = MaterialRegistry.Get(target.PrimaryMaterial);

        if (environment.IsUnderWater)
        {
            state.Moisture = 1.0;
            state.CombustionIntensity = 0.0;
        }
        else if (environment.IsRaining)
        {
            state.Moisture += 0.01 * seconds;
            state.CombustionIntensity -= 0.08 * seconds;
        }

        if (state.IsBurning)
        {
            var intensity = state.CombustionIntensity;
            var burnRate = material.BurnRate ?? 0.01;

            state.FuelRemaining -= burnRate * intensity * seconds;
            state.CharLevel += burnRate * 0.65 * intensity * seconds;
            state.Moisture -= 0.04 * intensity * seconds;
            state.Soot += burnRate * 0.20 * intensity * seconds;
            state.Temperature = Math.Max(
                state.Temperature,
                environment.AmbientTemperature + (material.HeatOutput ?? 100.0) * intensity
            );

            if (state.FuelRemaining <= 0.0 || state.Moisture >= WorldSimulationThresholds.MaximumIgnitableMoisture)
            {
                state.CombustionIntensity = 0.0;
            }
        }
        else
        {
            var conductivity = material.ThermalConductivity ?? 0.1;
            var coolingFactor = Math.Min(1.0, conductivity * 0.02 * seconds);
            state.Temperature += (environment.AmbientTemperature - state.Temperature) * coolingFactor;

            if (!environment.IsRaining && state.Moisture > 0.0)
            {
                state.Moisture -= (0.001 + environment.WindStrength * 0.002) * seconds;
            }
        }

        target.OnSimulationStateChanged();
    }

    internal static void ResetForTesting()
    {
        _activeItems.Clear();
        _activeSet.Clear();
    }

    private static void Tick()
    {
        var environment = new EnvironmentContext();

        for (var i = _activeItems.Count - 1; i >= 0; i--)
        {
            var item = _activeItems[i];

            if (item?.Deleted != false)
            {
                _activeSet.Remove(item);
                _activeItems.RemoveAt(i);
                continue;
            }

            Advance(item, TimeSpan.FromSeconds(1.0), environment);

            if (!NeedsUpdates(item, environment))
            {
                _activeSet.Remove(item);
                _activeItems.RemoveAt(i);
            }
        }
    }
}
