using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Engines.WorldSimulation;

[SerializationGenerator(0)]
public abstract partial class WorldSimulationItem : Item, IWorldSimulatedObject, IWorldObjectState
{
    private static readonly MaterialFraction[] _emptyComposition = [];

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private MaterialId _primaryMaterial;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _condition = 1.0;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _temperature = 20.0;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _moisture;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _sharpness;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _deformation;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _cracking;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _combustionIntensity;

    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _fuelRemaining = 1.0;

    [SerializableField(9)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _charLevel;

    [SerializableField(10)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _dirt;

    [SerializableField(11)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _blood;

    [SerializableField(12)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _oil;

    [SerializableField(13)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _poison;

    [SerializableField(14)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _soot;

    [SerializableField(15)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _freshness = 1.0;

    [SerializableField(16)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _decay;

    [SerializableField(17)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _fermentation;

    protected WorldSimulationItem(int itemID, MaterialId primaryMaterial) : base(itemID)
    {
        _primaryMaterial = primaryMaterial;
    }

    public Item Item => this;
    public IWorldObjectState State => this;
    public virtual IReadOnlyList<MaterialFraction> Composition => _emptyComposition;
    public virtual PhysicalProperties PhysicalProperties => new(Weight, null);

    public bool IsDry => _moisture <= WorldSimulationThresholds.DryMoisture;
    public bool IsWet => _moisture >= WorldSimulationThresholds.WetMoisture;
    public bool IsHot => _temperature >= WorldSimulationThresholds.HotTemperature;
    public bool IsCold => _temperature <= WorldSimulationThresholds.ColdTemperature;
    public bool IsBurning => _combustionIntensity > 0.001 && _fuelRemaining > 0.0;
    public bool IsCharred => _charLevel >= WorldSimulationThresholds.CharredLevel;

    public WorldObjectState CaptureState() => new()
    {
        Condition = _condition,
        Temperature = _temperature,
        Moisture = _moisture,
        Sharpness = _sharpness,
        Deformation = _deformation,
        Cracking = _cracking,
        CombustionIntensity = _combustionIntensity,
        FuelRemaining = _fuelRemaining,
        CharLevel = _charLevel,
        Dirt = _dirt,
        Blood = _blood,
        Oil = _oil,
        Poison = _poison,
        Soot = _soot,
        Freshness = _freshness,
        Decay = _decay,
        Fermentation = _fermentation
    };

    public void RestoreState(IWorldObjectState state)
    {
        _condition = state.Condition;
        _temperature = state.Temperature;
        _moisture = state.Moisture;
        _sharpness = state.Sharpness;
        _deformation = state.Deformation;
        _cracking = state.Cracking;
        _combustionIntensity = state.CombustionIntensity;
        _fuelRemaining = state.FuelRemaining;
        _charLevel = state.CharLevel;
        _dirt = state.Dirt;
        _blood = state.Blood;
        _oil = state.Oil;
        _poison = state.Poison;
        _soot = state.Soot;
        _freshness = state.Freshness;
        _decay = state.Decay;
        _fermentation = state.Fermentation;
        OnSimulationStateChanged();
    }

    public virtual void OnSimulationStateChanged()
    {
        _condition = WorldObjectState.Normalize(_condition);
        _moisture = WorldObjectState.Normalize(_moisture);
        _sharpness = WorldObjectState.Normalize(_sharpness);
        _deformation = WorldObjectState.Normalize(_deformation);
        _cracking = WorldObjectState.Normalize(_cracking);
        _combustionIntensity = WorldObjectState.Normalize(_combustionIntensity);
        _fuelRemaining = WorldObjectState.Normalize(_fuelRemaining);
        _charLevel = WorldObjectState.Normalize(_charLevel);
        _dirt = WorldObjectState.Normalize(_dirt);
        _blood = WorldObjectState.Normalize(_blood);
        _oil = WorldObjectState.Normalize(_oil);
        _poison = WorldObjectState.Normalize(_poison);
        _soot = WorldObjectState.Normalize(_soot);
        _freshness = WorldObjectState.Normalize(_freshness);
        _decay = WorldObjectState.Normalize(_decay);
        _fermentation = WorldObjectState.Normalize(_fermentation);

        VisualStateResolver.Apply(this);
        WorldSimulationSystem.RegisterIfActive(this);
        InvalidateProperties();
        this.MarkDirty();
    }

    [AfterDeserialization]
    private void AfterWorldSimulationDeserialization()
    {
        VisualStateResolver.Apply(this);
        WorldSimulationSystem.RegisterIfActive(this);
    }

    public override void OnAfterDelete()
    {
        WorldSimulationSystem.Unregister(this);
        base.OnAfterDelete();
    }
}
