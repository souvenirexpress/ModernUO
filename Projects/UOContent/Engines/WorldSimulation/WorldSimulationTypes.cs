using System;
using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.WorldSimulation;

public enum MaterialId
{
    Unknown,
    OakWood,
    PineWood,
    GenericWood,
    Stone,
    Granite,
    Iron,
    Steel,
    Copper,
    Gold,
    Silver,
    Leather,
    Cloth,
    Wool,
    Glass,
    Clay,
    Bone,
    Water,
    Oil,
    Wax,
    Flesh,
    PlantMatter,
    FoodOrganic
}

public enum WorldInteractionAction
{
    Cut,
    Chop,
    Pierce,
    Strike,
    Crush,
    Pry,
    Heat,
    Ignite,
    Cool,
    Extinguish,
    Contain,
    Pour,
    Absorb,
    Release,
    Wash,
    Mix,
    Attach,
    Detach,
    Tie,
    Support,
    Open,
    Close,
    Lock,
    Unlock,
    Fill,
    Empty,
    Cook,
    Bake,
    Boil,
    Grind,
    Ferment,
    Eat,
    Drink,
    Wield,
    Wear,
    Carry,
    Sit,
    Read,
    Write,
    Inspect,
    EmitLight,
    ProduceSound,
    ProduceSmell,
    ChannelMagic,
    StoreMagic,
    ReleaseMagic
}

public readonly record struct MaterialFraction(MaterialId Material, double Fraction);

public readonly record struct PhysicalProperties(double? Mass, double? Volume);

public readonly record struct ToolCapabilities(
    double CutPower = 0,
    double ChopPower = 0,
    double PiercePower = 0,
    double StrikePower = 0,
    double CrushPower = 0,
    double PryPower = 0,
    double HeatPower = 0,
    double IgnitePower = 0,
    double CoolPower = 0,
    double ExtinguishPower = 0,
    double LightPower = 0
);

public readonly record struct EnvironmentContext(
    double AmbientTemperature = 20.0,
    bool IsRaining = false,
    bool IsUnderWater = false,
    double WindStrength = 0.0
)
{
    public EnvironmentContext() : this(20.0, false, false, 0.0)
    {
    }
}

public interface IWorldObjectState
{
    double Condition { get; set; }
    double Temperature { get; set; }
    double Moisture { get; set; }
    double Sharpness { get; set; }
    double Deformation { get; set; }
    double Cracking { get; set; }
    double CombustionIntensity { get; set; }
    double FuelRemaining { get; set; }
    double CharLevel { get; set; }
    double Dirt { get; set; }
    double Blood { get; set; }
    double Oil { get; set; }
    double Poison { get; set; }
    double Soot { get; set; }
    double Freshness { get; set; }
    double Decay { get; set; }
    double Fermentation { get; set; }

    bool IsDry => Moisture <= WorldSimulationThresholds.DryMoisture;
    bool IsWet => Moisture >= WorldSimulationThresholds.WetMoisture;
    bool IsHot => Temperature >= WorldSimulationThresholds.HotTemperature;
    bool IsCold => Temperature <= WorldSimulationThresholds.ColdTemperature;
    bool IsBurning => CombustionIntensity > 0.001 && FuelRemaining > 0.0;
    bool IsCharred => CharLevel >= WorldSimulationThresholds.CharredLevel;
}

public interface IWorldSimulatedObject
{
    Item Item { get; }
    MaterialId PrimaryMaterial { get; }
    IReadOnlyList<MaterialFraction> Composition { get; }
    PhysicalProperties PhysicalProperties { get; }
    IWorldObjectState State { get; }
    void OnSimulationStateChanged();
}

public interface IWorldInteractionSource
{
    ToolCapabilities Capabilities { get; }
}

public interface IWorldConsumableSource
{
    void Consume(WorldInteractionAction action, double amount);
}

public interface IWorldVisualProfile
{
    int DefaultItemID { get; }
    int BurningItemID { get; }
    int CharredItemID { get; }
    int AshItemID { get; }
}

public sealed class InteractionContext
{
    public Mobile Actor { get; init; }
    public object Source { get; init; }
    public object Target { get; init; }
    public WorldInteractionAction Action { get; init; }
    public EnvironmentContext Environment { get; init; } = new();
}

public sealed class InteractionResult
{
    public bool Success { get; init; }
    public List<string> StateChanges { get; } = [];
    public List<Item> GeneratedObjects { get; } = [];
    public List<Item> DestroyedObjects { get; } = [];
    public List<string> Effects { get; } = [];
    public List<string> Messages { get; } = [];

    public static InteractionResult Failed(string message)
    {
        var result = new InteractionResult();
        result.Messages.Add(message);
        return result;
    }
}

public readonly record struct InteractionExplanation(bool Allowed, string Reason);

public static class WorldSimulationThresholds
{
    public const double DryMoisture = 0.10;
    public const double WetMoisture = 0.30;
    public const double SoakedMoisture = 0.70;
    public const double MaximumIgnitableMoisture = 0.35;
    public const double HotTemperature = 60.0;
    public const double ColdTemperature = 5.0;
    public const double CharredLevel = 0.50;
    public const double MinimumFlammability = 0.20;
    public const double MinimumIgnitePower = 0.20;
}
