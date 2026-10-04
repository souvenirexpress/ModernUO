using System;
using Server.Items;

namespace Server.Engines.WorldSimulation;

public sealed class HeatInteractionRule : IInteractionRule
{
    public bool CanHandle(InteractionContext context) =>
        context?.Action == WorldInteractionAction.Heat && context.Target is IWorldSimulatedObject;

    public InteractionExplanation Explain(InteractionContext context)
    {
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);
        return double.IsFinite(capabilities.HeatPower) && capabilities.HeatPower > 0.0
            ? new InteractionExplanation(true, "The source can transfer heat to the target.")
            : new InteractionExplanation(false, "The source has no heat power.");
    }

    public InteractionResult Resolve(InteractionContext context)
    {
        var target = (IWorldSimulatedObject)context.Target;
        var state = target.State;
        var material = MaterialRegistry.Get(target.PrimaryMaterial);
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);
        var before = state.Temperature;
        var heatCapacity = Math.Max(0.1, material.HeatCapacity ?? 1.0);
        state.Temperature += capabilities.HeatPower * 100.0 / heatCapacity;
        target.OnSimulationStateChanged();

        var result = new InteractionResult { Success = true };
        result.StateChanges.Add($"Temperature {before:F1} C -> {state.Temperature:F1} C");
        result.Messages.Add("The target grows hotter.");
        return result;
    }
}

public sealed class IgniteInteractionRule : IInteractionRule
{
    public bool CanHandle(InteractionContext context) =>
        context?.Action == WorldInteractionAction.Ignite && context.Target is IWorldSimulatedObject;

    public InteractionExplanation Explain(InteractionContext context)
    {
        var target = (IWorldSimulatedObject)context.Target;
        var state = target.State;
        var material = MaterialRegistry.Get(target.PrimaryMaterial);
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);

        if (!double.IsFinite(capabilities.IgnitePower) || !double.IsFinite(capabilities.HeatPower) ||
            capabilities.IgnitePower < WorldSimulationThresholds.MinimumIgnitePower)
        {
            return new InteractionExplanation(
                false,
                $"Source ignite power {capabilities.IgnitePower:F2}; required >= {WorldSimulationThresholds.MinimumIgnitePower:F2}."
            );
        }

        if (material.Flammability is null or < WorldSimulationThresholds.MinimumFlammability)
        {
            return new InteractionExplanation(false, "Target material is not sufficiently flammable.");
        }

        if (material.IgnitionTemperature is null)
        {
            return new InteractionExplanation(false, "Target material has no ignition temperature.");
        }

        if (state.Moisture >= WorldSimulationThresholds.MaximumIgnitableMoisture)
        {
            return new InteractionExplanation(
                false,
                $"Target moisture {state.Moisture:F2}; required < {WorldSimulationThresholds.MaximumIgnitableMoisture:F2}."
            );
        }

        if (state.FuelRemaining <= 0.0)
        {
            return new InteractionExplanation(false, "Target has no fuel remaining.");
        }

        if (state.IsBurning || context.Environment.IsUnderWater)
        {
            return new InteractionExplanation(false, "The target is already burning or under water.");
        }
        var heatCapacity = Math.Max(0.1, material.HeatCapacity ?? 1.0);
        var reached = state.Temperature + (capabilities.HeatPower * 100.0 + capabilities.IgnitePower * 400.0) /
            heatCapacity * (1.0 - state.Moisture * 0.75);
        if (reached < material.IgnitionTemperature)
        {
            return new InteractionExplanation(false, "Insufficient heat to ignite; heat the target first.");
        }

        return new InteractionExplanation(true, "Material, moisture, fuel, and source power permit ignition.");
    }

    public InteractionResult Resolve(InteractionContext context)
    {
        var target = (IWorldSimulatedObject)context.Target;
        var state = target.State;
        var material = MaterialRegistry.Get(target.PrimaryMaterial);
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);
        var heatCapacity = Math.Max(0.1, material.HeatCapacity ?? 1.0);
        var beforeTemperature = state.Temperature;
        var heatTransfer = (capabilities.HeatPower * 100.0 + capabilities.IgnitePower * 400.0) / heatCapacity;
        state.Temperature += heatTransfer * (1.0 - state.Moisture * 0.75);

        var result = new InteractionResult();
        result.StateChanges.Add($"Temperature {beforeTemperature:F1} C -> {state.Temperature:F1} C");

        if (state.Temperature < material.IgnitionTemperature)
        {
            target.OnSimulationStateChanged();
            result.Messages.Add(
                $"Target reached {state.Temperature:F1} C; ignition requires {material.IgnitionTemperature:F1} C."
            );
            return result;
        }

        state.CombustionIntensity = Math.Clamp(
            material.Flammability!.Value * capabilities.IgnitePower * (1.0 - state.Moisture),
            0.05,
            1.0
        );
        target.OnSimulationStateChanged();

        result = new InteractionResult { Success = true };
        result.StateChanges.Add($"Temperature {beforeTemperature:F1} C -> {state.Temperature:F1} C");
        result.StateChanges.Add($"Combustion intensity -> {state.CombustionIntensity:F2}");
        result.Effects.Add("Fire and light");
        result.Messages.Add("The target catches fire.");
        return result;
    }
}

public sealed class ExtinguishInteractionRule : IInteractionRule
{
    public bool CanHandle(InteractionContext context) =>
        context?.Action is WorldInteractionAction.Extinguish or WorldInteractionAction.Cool &&
        context.Target is IWorldSimulatedObject;

    public InteractionExplanation Explain(InteractionContext context)
    {
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);

        var power = context.Action == WorldInteractionAction.Cool ? capabilities.CoolPower : capabilities.ExtinguishPower;
        if (!double.IsFinite(power) || power <= 0.0)
        {
            return new InteractionExplanation(false, "The source cannot cool or extinguish the target.");
        }

        if (context.Action == WorldInteractionAction.Extinguish && !((IWorldSimulatedObject)context.Target).State.IsBurning)
        {
            return new InteractionExplanation(false, "The target is not burning.");
        }

        return new InteractionExplanation(true, "The source can absorb heat and suppress combustion.");
    }

    public InteractionResult Resolve(InteractionContext context)
    {
        var target = (IWorldSimulatedObject)context.Target;
        var state = target.State;
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);
        var power = Math.Clamp(context.Action == WorldInteractionAction.Cool ? capabilities.CoolPower : capabilities.ExtinguishPower, 0.0, 2.0);
        var beforeTemperature = state.Temperature;
        var beforeMoisture = state.Moisture;
        var beforeCombustion = state.CombustionIntensity;

        state.Temperature -= 180.0 * power;
        state.Moisture += 0.55 * power;
        state.CombustionIntensity -= 0.90 * power;

        if (state.Moisture >= WorldSimulationThresholds.MaximumIgnitableMoisture)
        {
            state.CombustionIntensity = 0.0;
        }

        target.OnSimulationStateChanged();

        if (context.Source is IWorldConsumableSource consumable)
        {
            consumable.Consume(context.Action, power);
        }
        else if (context.Source is BaseBeverage beverage)
        {
            beverage.Quantity--;
        }
        else if (context.Source is BaseWaterContainer water)
        {
            water.Quantity--;
        }

        var result = new InteractionResult { Success = true };
        result.StateChanges.Add($"Temperature {beforeTemperature:F1} C -> {state.Temperature:F1} C");
        result.StateChanges.Add($"Moisture {beforeMoisture:F2} -> {state.Moisture:F2}");
        result.StateChanges.Add($"Combustion {beforeCombustion:F2} -> {state.CombustionIntensity:F2}");
        result.Messages.Add(context.Action == WorldInteractionAction.Cool ? "The target cools and becomes wetter."
            : state.IsBurning ? "The flames weaken." : "The fire is extinguished.");
        return result;
    }
}
