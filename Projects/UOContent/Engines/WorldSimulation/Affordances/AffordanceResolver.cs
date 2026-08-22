using System.Collections.Generic;

namespace Server.Engines.WorldSimulation;

public static class AffordanceResolver
{
    public static IReadOnlyList<WorldInteractionAction> GetAvailableActions(
        Mobile actor,
        object source,
        object target,
        EnvironmentContext environment
    )
    {
        var actions = new List<WorldInteractionAction> { WorldInteractionAction.Inspect };

        if (target is not IWorldSimulatedObject simulatedTarget)
        {
            return actions;
        }

        var capabilities = SourceCapabilityResolver.GetCapabilities(source);
        var material = MaterialRegistry.Get(simulatedTarget.PrimaryMaterial);
        var state = simulatedTarget.State;

        if (capabilities.HeatPower > 0.0)
        {
            actions.Add(WorldInteractionAction.Heat);
        }

        if (capabilities.IgnitePower >= WorldSimulationThresholds.MinimumIgnitePower &&
            material.Flammability >= WorldSimulationThresholds.MinimumFlammability &&
            state.Moisture < WorldSimulationThresholds.MaximumIgnitableMoisture &&
            state.FuelRemaining > 0.0)
        {
            actions.Add(WorldInteractionAction.Ignite);
        }

        if (capabilities.CoolPower > 0.0)
        {
            actions.Add(WorldInteractionAction.Cool);
        }

        if (capabilities.ExtinguishPower > 0.0 && state.IsBurning)
        {
            actions.Add(WorldInteractionAction.Extinguish);
        }

        if (capabilities.ChopPower > 0.0 && material.CutResistance is not null)
        {
            actions.Add(WorldInteractionAction.Chop);
        }

        if (capabilities.CutPower > 0.0 && material.CutResistance is not null)
        {
            actions.Add(WorldInteractionAction.Cut);
        }

        if (simulatedTarget.Item.Movable)
        {
            actions.Add(WorldInteractionAction.Carry);
        }

        return actions;
    }
}
