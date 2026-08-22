using System;

namespace Server.Engines.WorldSimulation;

public sealed class WorldObjectState : IWorldObjectState
{
    private double _condition = 1.0;
    private double _temperature = 20.0;
    private double _moisture;
    private double _sharpness;
    private double _deformation;
    private double _cracking;
    private double _combustionIntensity;
    private double _fuelRemaining = 1.0;
    private double _charLevel;
    private double _dirt;
    private double _blood;
    private double _oil;
    private double _poison;
    private double _soot;
    private double _freshness = 1.0;
    private double _decay;
    private double _fermentation;

    public double Condition { get => _condition; set => _condition = Normalize(value); }
    public double Temperature { get => _temperature; set => _temperature = value; }
    public double Moisture { get => _moisture; set => _moisture = Normalize(value); }
    public double Sharpness { get => _sharpness; set => _sharpness = Normalize(value); }
    public double Deformation { get => _deformation; set => _deformation = Normalize(value); }
    public double Cracking { get => _cracking; set => _cracking = Normalize(value); }
    public double CombustionIntensity { get => _combustionIntensity; set => _combustionIntensity = Normalize(value); }
    public double FuelRemaining { get => _fuelRemaining; set => _fuelRemaining = Normalize(value); }
    public double CharLevel { get => _charLevel; set => _charLevel = Normalize(value); }
    public double Dirt { get => _dirt; set => _dirt = Normalize(value); }
    public double Blood { get => _blood; set => _blood = Normalize(value); }
    public double Oil { get => _oil; set => _oil = Normalize(value); }
    public double Poison { get => _poison; set => _poison = Normalize(value); }
    public double Soot { get => _soot; set => _soot = Normalize(value); }
    public double Freshness { get => _freshness; set => _freshness = Normalize(value); }
    public double Decay { get => _decay; set => _decay = Normalize(value); }
    public double Fermentation { get => _fermentation; set => _fermentation = Normalize(value); }

    public WorldObjectState Clone() => new()
    {
        Condition = Condition,
        Temperature = Temperature,
        Moisture = Moisture,
        Sharpness = Sharpness,
        Deformation = Deformation,
        Cracking = Cracking,
        CombustionIntensity = CombustionIntensity,
        FuelRemaining = FuelRemaining,
        CharLevel = CharLevel,
        Dirt = Dirt,
        Blood = Blood,
        Oil = Oil,
        Poison = Poison,
        Soot = Soot,
        Freshness = Freshness,
        Decay = Decay,
        Fermentation = Fermentation
    };

    public static double Normalize(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed class ContainerObjectState
{
    private double _fillLevel;

    public bool IsOpen { get; set; }
    public bool IsLocked { get; set; }
    public bool IsSealed { get; set; }
    public double FillLevel { get => _fillLevel; set => _fillLevel = WorldObjectState.Normalize(value); }
}

public interface IWorldContainerStateProvider
{
    ContainerObjectState ContainerState { get; }
}
