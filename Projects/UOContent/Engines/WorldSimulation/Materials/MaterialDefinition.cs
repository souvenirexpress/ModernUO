namespace Server.Engines.WorldSimulation;

public sealed class MaterialDefinition
{
    public MaterialId Id { get; init; }
    public string Name { get; init; }

    public double? Density { get; init; }
    public double? Hardness { get; init; }
    public double? Toughness { get; init; }
    public double? Brittleness { get; init; }
    public double? Flexibility { get; init; }
    public double? Porosity { get; init; }
    public double? Friction { get; init; }

    public double? StructuralStrength { get; init; }
    public double? ImpactResistance { get; init; }
    public double? CutResistance { get; init; }
    public double? PierceResistance { get; init; }
    public double? CrushResistance { get; init; }

    public double? HeatCapacity { get; init; }
    public double? ThermalConductivity { get; init; }
    public double? Flammability { get; init; }
    public double? IgnitionTemperature { get; init; }
    public double? BurnRate { get; init; }
    public double? HeatOutput { get; init; }
    public double? MeltingPoint { get; init; }
    public double? FreezingPoint { get; init; }
    public double? BoilingPoint { get; init; }

    public double? WaterAbsorption { get; init; }
    public double? WaterResistance { get; init; }
    public double? Solubility { get; init; }
    public double? CorrosionPotential { get; init; }
    public double? OxidationRate { get; init; }
    public double? Toxicity { get; init; }
    public double? DecayPotential { get; init; }
    public double? FermentationPotential { get; init; }
}
