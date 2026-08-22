using System;
using Server.Engines.WorldSimulation;
using Server.Targeting;

namespace Server.Commands;

public static class WorldSimulationCommands
{
    public static void Configure()
    {
        CommandSystem.Register("SimInspect", AccessLevel.GameMaster, Inspect_OnCommand);
        CommandSystem.Register("SimExplain", AccessLevel.GameMaster, Explain_OnCommand);
    }

    [Usage("SimInspect")]
    [Description("Displays material, dynamic state, derived state, and affordances for a targeted item.")]
    private static void Inspect_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, Inspect_OnTarget);
        e.Mobile.SendMessage("Target an item to inspect its world simulation state.");
    }

    private static void Inspect_OnTarget(Mobile from, object target)
    {
        if (target is not IWorldSimulatedObject simulated)
        {
            from.SendMessage("This legacy object has no world simulation component.");
            return;
        }

        var material = MaterialRegistry.Get(simulated.PrimaryMaterial);
        var state = simulated.State;
        var actions = AffordanceResolver.GetAvailableActions(from, null, target, new EnvironmentContext());

        from.SendMessage($"--- Simulation: {simulated.Item} ---");
        from.SendMessage($"Material: {material.Name} ({simulated.PrimaryMaterial})");
        var mass = simulated.PhysicalProperties.Mass;

        if (mass is null)
        {
            from.SendMessage("Mass: n/a");
        }
        else
        {
            from.SendMessage($"Mass: {mass:F2}");
        }

        from.SendMessage($"Hardness: {Format(material.Hardness)} | Flammability: {Format(material.Flammability)} | Ignition: {Format(material.IgnitionTemperature)} C");
        from.SendMessage(
            $"Temperature: {state.Temperature:F1} C | Moisture: {state.Moisture:F2} | Condition: {state.Condition:F2}"
        );
        from.SendMessage(
            $"Combustion: {state.CombustionIntensity:F2} | Fuel: {state.FuelRemaining:F2} | Char: {state.CharLevel:F2}"
        );
        from.SendMessage(
            $"Derived: Dry={state.IsDry} Wet={state.IsWet} Hot={state.IsHot} Burning={state.IsBurning} Charred={state.IsCharred}"
        );
        from.SendMessage("Available without a tool:");
        from.SendMessage(string.Join(", ", actions));
    }

    [Usage("SimExplain <action>")]
    [Description("Targets a source and target, then explains why a simulation interaction can or cannot happen.")]
    private static void Explain_OnCommand(CommandEventArgs e)
    {
        if (e.Arguments.Length != 1 ||
            !Enum.TryParse<WorldInteractionAction>(e.GetString(0), true, out var action))
        {
            e.Mobile.SendMessage("Usage: SimExplain <Heat|Ignite|Cool|Extinguish>");
            return;
        }

        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, source) => ExplainSourceTarget(from, source, action));
        e.Mobile.SendMessage("Target the source or tool.");
    }

    private static void ExplainSourceTarget(Mobile from, object source, WorldInteractionAction action)
    {
        from.BeginTarget(
            -1,
            false,
            TargetFlags.None,
            (mobile, target) =>
            {
                var explanation = InteractionResolver.Explain(
                    new InteractionContext
                    {
                        Actor = mobile,
                        Source = source,
                        Target = target,
                        Action = action,
                        Environment = new EnvironmentContext()
                    }
                );

                mobile.SendMessage(explanation.Allowed ? "ALLOWED" : "FAILED");
                mobile.SendMessage(explanation.Reason);
            }
        );
        from.SendMessage("Target the object to interact with.");
    }

    private static string Format(double? value) => value?.ToString("F2") ?? "n/a";
}
