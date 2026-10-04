using System;
using Server.Items;

namespace Server.Engines.WorldSimulation;

public sealed class MechanicalInteractionRule : IInteractionRule
{
    public bool CanHandle(InteractionContext context) =>
        context.Action is WorldInteractionAction.Cut or WorldInteractionAction.Chop && context.Target is IWorldSimulatedObject;

    public InteractionExplanation Explain(InteractionContext context)
    {
        var target = (IWorldSimulatedObject)context.Target;
        var resistance = MaterialRegistry.Get(target.PrimaryMaterial).CutResistance;
        if (target.State.Condition <= 0)
        {
            return new(false, "Das Objekt ist bereits zerstoert.");
        }
        if (target.Item.Stackable && target.Item.Amount > 1)
        {
            return new(false, "Bitte trennt zuerst ein einzelnes Objekt vom Stapel.");
        }
        if (context.Source is Item { Stackable: true, Amount: > 1 })
        {
            return new(false, "Bitte trennt zuerst ein einzelnes Werkzeug vom Stapel.");
        }
        if (resistance is null || !double.IsFinite(resistance.Value) || resistance < 0 || resistance >= 1)
        {
            return new(false, "Dieses Material kann so nicht bearbeitet werden.");
        }
        var force = EffectivePower(context);
        if (force <= Math.Max(0.05, resistance.Value))
        {
            return new(false, "Werkzeugstaerke, Schaerfe oder Zustand reichen fuer diesen Materialwiderstand nicht aus.");
        }
        return new(true, "Das Werkzeug kann das Material bearbeiten.");
    }

    internal static double EffectivePower(InteractionContext context)
    {
        var capabilities = SourceCapabilityResolver.GetCapabilities(context.Source);
        var power = context.Action == WorldInteractionAction.Cut ? capabilities.CutPower : capabilities.ChopPower;
        if (!double.IsFinite(power))
        {
            return 0;
        }
        if (context.Source is IWorldSimulatedObject source)
        {
            power *= source.State.Condition * source.State.Sharpness;
        }
        return double.IsFinite(power) ? Math.Clamp(power, 0, 1) : 0;
    }

    public InteractionResult Resolve(InteractionContext context)
    {
        var target = (IWorldSimulatedObject)context.Target;
        var resistance = MaterialRegistry.Get(target.PrimaryMaterial).CutResistance!.Value;
        var before = target.State.Condition;
        var damage = Math.Clamp((EffectivePower(context) - resistance) *
            (context.Action == WorldInteractionAction.Chop ? 0.45 : 0.30), 0.01, 0.45);
        target.State.Condition = Math.Max(0, before - damage);
        target.State.Cracking = Math.Min(1, target.State.Cracking + damage);
        target.OnSimulationStateChanged();

        if (context.Source is IWorldSimulatedObject source)
        {
            source.State.Condition = Math.Max(0, source.State.Condition - 0.002);
            source.State.Sharpness = Math.Max(0, source.State.Sharpness - 0.003);
            source.OnSimulationStateChanged();
        }
        else if (context.Source is BaseWeapon { MaxHitPoints: > 0 } weapon)
        {
            weapon.HitPoints = Math.Max(0, weapon.HitPoints - 1);
        }

        var result = new InteractionResult { Success = true };
        result.StateChanges.Add($"Condition {before:F2} -> {target.State.Condition:F2}");
        result.Messages.Add(target.State.Condition <= 0
            ? "Das Objekt ist zerstoert. Seine Ueberreste bleiben erhalten."
            : context.Action == WorldInteractionAction.Chop ? "Ihr hackt in das Material." : "Ihr schneidet in das Material.");
        context.Actor?.PlaySound(context.Action == WorldInteractionAction.Chop ? 0x13E : 0x248);
        return result;
    }
}
