using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.WorldSimulation;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class DryOakLog : WorldSimulationItem, IWorldVisualProfile
{
    private static readonly MaterialFraction[] _composition = [new(MaterialId.OakWood, 1.0)];

    [Constructible]
    public DryOakLog() : base(0x1BDD, MaterialId.OakWood)
    {
        Weight = 2.0;
        Moisture = 0.05;
        FuelRemaining = 1.0;
    }

    public override string DefaultName => "a dry oak log";
    public override IReadOnlyList<MaterialFraction> Composition => _composition;
    public override PhysicalProperties PhysicalProperties => new(2.0, 0.003);

    public int DefaultItemID => 0x1BDD;
    public int BurningItemID => 0xDE3;
    public int CharredItemID => 0xDE9;
    public int AshItemID => 0xDEA;
}

[SerializationGenerator(0)]
public partial class WetOakLog : DryOakLog
{
    [Constructible]
    public WetOakLog()
    {
        Moisture = 0.85;
        OnSimulationStateChanged();
    }

    public override string DefaultName => "a wet oak log";
}

[SerializationGenerator(0)]
public partial class SimulationTorch : BaseEquipableLight, IWorldInteractionSource
{
    [Constructible]
    public SimulationTorch() : base(0xF6B)
    {
        Stackable = true;
        Light = LightType.Circle300;
    }

    public override string DefaultName => "a simulation torch";
    public override double DefaultWeight => 1.0;
    public override int LitItemID => 0xA12;
    public override int UnlitItemID => 0xF6B;
    public override int LitSound => 0x54;
    public override int UnlitSound => 0x4BB;

    public ToolCapabilities Capabilities => Burning
        ? new ToolCapabilities(HeatPower: 1.0, IgnitePower: 1.0, LightPower: 1.0)
        : default;

    public override void OnDoubleClick(Mobile from)
    {
        if (BurntOut || Protected && from.AccessLevel == AccessLevel.Player || !from.InRange(GetWorldLocation(), 2))
        {
            return;
        }

        if (!Burning)
        {
            base.OnDoubleClick(from);
            from.SendMessage("The torch is now lit. Use it again to target something.");
            return;
        }

        from.BeginTarget(2, true, TargetFlags.None, IgniteTarget);
        from.SendMessage("What do you want to ignite?");
    }

    private void IgniteTarget(Mobile from, object target)
    {
        var result = InteractionResolver.Resolve(
            new InteractionContext
            {
                Actor = from,
                Source = this,
                Target = target,
                Action = WorldInteractionAction.Ignite,
                Environment = new EnvironmentContext()
            }
        );

        WorldSimulationMessaging.Send(from, result);
    }
}

[SerializationGenerator(0)]
public partial class SimulationWater : WorldSimulationItem, IWorldInteractionSource, IWorldConsumableSource
{
    [Constructible]
    public SimulationWater(int amount = 1) : base(0x1F9D, MaterialId.Water)
    {
        Stackable = true;
        Amount = amount;
        Weight = 1.0;
        Moisture = 1.0;
        FuelRemaining = 0.0;
    }

    public override string DefaultName => "simulation water";
    public ToolCapabilities Capabilities => new(CoolPower: 1.0, ExtinguishPower: 1.0);

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack) && !from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        from.BeginTarget(2, true, TargetFlags.None, PourTarget);
        from.SendMessage("What do you want to pour the water on?");
    }

    public void Consume(WorldInteractionAction action, double amount)
    {
        if (action is not WorldInteractionAction.Extinguish and not WorldInteractionAction.Cool)
        {
            return;
        }

        if (Amount > 1)
        {
            Amount--;
        }
        else
        {
            Delete();
        }
    }

    private void PourTarget(Mobile from, object target)
    {
        var result = InteractionResolver.Resolve(
            new InteractionContext
            {
                Actor = from,
                Source = this,
                Target = target,
                Action = WorldInteractionAction.Extinguish,
                Environment = new EnvironmentContext()
            }
        );

        WorldSimulationMessaging.Send(from, result);
    }
}

[SerializationGenerator(0)]
public partial class SimulationIronIngot : WorldSimulationItem
{
    [Constructible]
    public SimulationIronIngot() : base(0x1BF2, MaterialId.Iron)
    {
        Weight = 0.1;
        Stackable = true;
    }

    public override string DefaultName => "a simulation iron ingot";
}

[SerializationGenerator(0)]
public partial class SimulationStone : WorldSimulationItem
{
    [Constructible]
    public SimulationStone() : base(0x1779, MaterialId.Stone) => Weight = 2.0;

    public override string DefaultName => "a simulation stone";
}

[SerializationGenerator(0)]
public partial class SimulationCloth : WorldSimulationItem
{
    [Constructible]
    public SimulationCloth() : base(0x1766, MaterialId.Cloth)
    {
        Weight = 1.0;
        Stackable = true;
    }

    public override string DefaultName => "simulation cloth";
}

public static class WorldSimulationMessaging
{
    public static void Send(Mobile mobile, InteractionResult result)
    {
        foreach (var message in result.Messages)
        {
            mobile.SendMessage(message);
        }
    }
}
