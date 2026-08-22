using System;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server.Engines.WorldSimulation;
using Server.Items;
using Server.Targeting;

namespace Server.Engines.LivingBritain;

[SerializationGenerator(0)]
public partial class LivingBritainWorldSimulationItem : WorldSimulationItem, ILivingBritainStatic, IWorldInteractionSource, IWorldVisualProfile
{
    [SerializableField(0)]
    private string _overrideId;

    [SerializableField(1)]
    private string _definitionFingerprint;

    [SerializableField(2)]
    private double _cutPower;

    [SerializableField(3)]
    private double _chopPower;

    [SerializableField(4)]
    private double _piercePower;

    [SerializableField(5)]
    private double _strikePower;

    [SerializableField(6)]
    private double _crushPower;

    [SerializableField(7)]
    private double _pryPower;

    [SerializableField(8)]
    private double _heatPower;

    [SerializableField(9)]
    private double _ignitePower;

    [SerializableField(10)]
    private double _coolPower;

    [SerializableField(11)]
    private double _extinguishPower;

    [SerializableField(12)]
    private double _lightPower;

    [SerializableField(13)]
    private int _defaultItemId;

    [SerializableField(14)]
    private int _burningItemId;

    [SerializableField(15)]
    private int _charredItemId;

    [SerializableField(16)]
    private int _ashItemId;

    [Constructible]
    public LivingBritainWorldSimulationItem() : base(0x1, MaterialId.Unknown)
    {
        Movable = false;
    }

    public LivingBritainWorldSimulationItem(BritainStaticOverride definition) : this() => Apply(definition);

    public ToolCapabilities Capabilities => new(
        _cutPower,
        _chopPower,
        _piercePower,
        _strikePower,
        _crushPower,
        _pryPower,
        _heatPower,
        _ignitePower,
        _coolPower,
        _extinguishPower,
        _lightPower
    );

    public int DefaultItemID => _defaultItemId;
    public int BurningItemID => _burningItemId;
    public int CharredItemID => _charredItemId;
    public int AshItemID => _ashItemId;

    public void Apply(BritainStaticOverride definition)
    {
        var simulation = definition.Simulation ?? throw new InvalidOperationException($"{definition.Id} has no simulation definition.");
        if (!Enum.TryParse<MaterialId>(simulation.Material, true, out var material) || material == MaterialId.Unknown)
        {
            throw new InvalidOperationException($"{definition.Id} uses unknown material '{simulation.Material}'.");
        }

        var fingerprint = JsonSerializer.Serialize(simulation);
        var resetState = !string.Equals(_definitionFingerprint, fingerprint, StringComparison.Ordinal);
        _overrideId = definition.Id;
        _definitionFingerprint = fingerprint;
        SetPrimaryMaterial(material);
        ApplyCapabilities(simulation.Capabilities);
        ApplyVisuals(definition.ItemId, simulation.Visuals);
        Hue = definition.Hue;
        Name = string.IsNullOrWhiteSpace(definition.Name) ? null : definition.Name;
        Movable = definition.Movable;

        if (resetState)
        {
            RestoreState(ToState(simulation.State));
        }
        else
        {
            OnSimulationStateChanged();
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446);
            return;
        }

        if (_heatPower <= 0 && _ignitePower <= 0 && _coolPower <= 0 && _extinguishPower <= 0)
        {
            from.SendMessage(0x3B2, $"Material: {PrimaryMaterial}, Zustand: {State.Condition:P0}, Feuchte: {State.Moisture:P0}.");
            return;
        }

        from.BeginTarget(2, true, TargetFlags.None, ApplyToTarget);
        from.SendMessage(0x3B2, "Worauf möchtet Ihr dieses Objekt anwenden?");
    }

    private void ApplyToTarget(Mobile from, object target)
    {
        var actions = AffordanceResolver.GetAvailableActions(from, this, target, new EnvironmentContext());
        var action = actions.Contains(WorldInteractionAction.Extinguish) ? WorldInteractionAction.Extinguish
            : actions.Contains(WorldInteractionAction.Ignite) ? WorldInteractionAction.Ignite
            : actions.Contains(WorldInteractionAction.Heat) ? WorldInteractionAction.Heat
            : actions.Contains(WorldInteractionAction.Cool) ? WorldInteractionAction.Cool
            : (WorldInteractionAction?)null;
        if (action == null)
        {
            from.SendMessage(0x22, "Diese Kombination besitzt keine anwendbare Interaktion.");
            return;
        }

        var result = InteractionResolver.Resolve(new InteractionContext
        {
            Actor = from,
            Source = this,
            Target = target,
            Action = action.Value,
            Environment = new EnvironmentContext()
        });
        WorldSimulationMessaging.Send(from, result);
    }

    private void ApplyCapabilities(BritainWorldSimulationCapabilities capabilities)
    {
        capabilities ??= new BritainWorldSimulationCapabilities();
        _cutPower = capabilities.CutPower;
        _chopPower = capabilities.ChopPower;
        _piercePower = capabilities.PiercePower;
        _strikePower = capabilities.StrikePower;
        _crushPower = capabilities.CrushPower;
        _pryPower = capabilities.PryPower;
        _heatPower = capabilities.HeatPower;
        _ignitePower = capabilities.IgnitePower;
        _coolPower = capabilities.CoolPower;
        _extinguishPower = capabilities.ExtinguishPower;
        _lightPower = capabilities.LightPower;
    }

    private void ApplyVisuals(int fallbackItemId, BritainWorldSimulationVisuals visuals)
    {
        visuals ??= new BritainWorldSimulationVisuals();
        _defaultItemId = visuals.DefaultItemId > 0 ? visuals.DefaultItemId : fallbackItemId;
        _burningItemId = visuals.BurningItemId > 0 ? visuals.BurningItemId : _defaultItemId;
        _charredItemId = visuals.CharredItemId > 0 ? visuals.CharredItemId : _defaultItemId;
        _ashItemId = visuals.AshItemId > 0 ? visuals.AshItemId : _charredItemId;
    }

    private static WorldObjectState ToState(BritainWorldSimulationState state)
    {
        state ??= new BritainWorldSimulationState();
        return new WorldObjectState
        {
            Condition = state.Condition,
            Temperature = state.Temperature,
            Moisture = state.Moisture,
            Sharpness = state.Sharpness,
            Deformation = state.Deformation,
            Cracking = state.Cracking,
            CombustionIntensity = state.CombustionIntensity,
            FuelRemaining = state.FuelRemaining,
            CharLevel = state.CharLevel,
            Dirt = state.Dirt,
            Blood = state.Blood,
            Oil = state.Oil,
            Poison = state.Poison,
            Soot = state.Soot,
            Freshness = state.Freshness,
            Decay = state.Decay,
            Fermentation = state.Fermentation
        };
    }
}
