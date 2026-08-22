using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Items;
using Server.Logging;

namespace Server.Engines.LivingBritain;

public interface ILivingBritainStatic
{
    string OverrideId { get; }
    void Apply(BritainStaticOverride definition);
}

public static class LivingBritainStatics
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(LivingBritainStatics));
    private static readonly Rectangle2D BritainBounds = new(1100, 1300, 800, 800);

    public static void Synchronize(BritainDataSet data)
    {
        var known = new Dictionary<string, ILivingBritainStatic>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<Item>();
        foreach (var item in World.Items.Values)
        {
            if (item is not ILivingBritainStatic managed || item.Deleted || item.Map != Map.Felucca ||
                !BritainBounds.Contains(item.Location) || string.IsNullOrWhiteSpace(managed.OverrideId))
            {
                continue;
            }

            if (!known.TryAdd(managed.OverrideId, managed))
            {
                duplicates.Add(item);
            }
        }

        foreach (var duplicate in duplicates)
        {
            duplicate.Delete();
        }

        var desired = data.StaticOverrides
            .Where(definition => definition.Operation is "add" or "replace")
            .ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var stale in known.Where(pair => !desired.ContainsKey(pair.Key)).Select(pair => pair.Value).OfType<Item>().ToArray())
        {
            stale.Delete();
            known.Remove(((ILivingBritainStatic)stale).OverrideId);
        }

        var created = 0;
        var updated = 0;
        var managedDoors = new List<LivingBritainStaticDoor>();
        foreach (var definition in desired.Values.OrderBy(definition => definition.Id))
        {
            known.TryGetValue(definition.Id, out var managed);
            if (managed != null && !LivingBritainStaticSemantics.IsCompatible(managed, definition))
            {
                ((Item)managed).Delete();
                managed = null;
            }

            if (managed == null)
            {
                managed = LivingBritainStaticSemantics.Create(definition);
                created++;
            }
            else
            {
                updated++;
            }

            managed.Apply(definition);
            var item = (Item)managed;
            var location = definition.ToPoint3D();
            if (item.Map != Map.Felucca || item.Location != location)
            {
                item.MoveToWorld(location, Map.Felucca);
            }

            if (managed is LivingBritainStaticDoor door)
            {
                managedDoors.Add(door);
            }
        }

        foreach (var door in managedDoors)
        {
            door.Link = null;
        }

        var linkedDoorGroups = 0;
        foreach (var group in managedDoors
                     .Where(door => !string.IsNullOrWhiteSpace(door.DoorGroupId))
                     .GroupBy(door => door.DoorGroupId, StringComparer.OrdinalIgnoreCase))
        {
            var doors = group.ToArray();
            if (doors.Length != 2)
            {
                Logger.Warning("Living Britain door group {DoorGroupId} has {DoorCount} leaves; exactly two are required.", group.Key, doors.Length);
                continue;
            }

            doors[0].Link = doors[1];
            doors[1].Link = doors[0];
            linkedDoorGroups++;
        }

        var classicOperations = data.StaticOverrides.Count(definition => definition.Operation is "replace" or "remove");
        if (classicOperations > 0)
        {
            Logger.Warning(
                "Living Britain has {ClassicOperationCount} Classic static replace/remove operations that also require client map overlays.",
                classicOperations
            );
        }

        Logger.Information(
            "Living Britain statics ready: {DefinitionCount} definitions, {ManagedCount} managed items, {CreatedCount} created, {UpdatedCount} updated, {LinkedDoorGroupCount} linked door groups.",
            data.StaticOverrides.Count,
            desired.Count,
            created,
            updated,
            linkedDoorGroups
        );
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainStaticItem : Item, ILivingBritainStatic
{
    [SerializableField(0)]
    private string _overrideId;

    [Constructible]
    public LivingBritainStaticItem() : base(0x1)
    {
        Movable = false;
    }

    public LivingBritainStaticItem(BritainStaticOverride definition) : this() => Apply(definition);

    public void Apply(BritainStaticOverride definition)
    {
        _overrideId = definition.Id;
        ItemID = definition.ItemId;
        Hue = definition.Hue;
        Name = string.IsNullOrWhiteSpace(definition.Name) ? null : definition.Name;
        Movable = false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!LivingBritainStaticSemantics.IsSeat(ItemID))
        {
            return;
        }

        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        from.Animate(32, 5, 1, true, false, 0);
        from.SendMessage(0x3B2, "Ihr setzt Euch hin.");
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainStaticContainer : LockableContainer, ILivingBritainStatic
{
    [SerializableField(0)]
    private string _overrideId;

    [Constructible]
    public LivingBritainStaticContainer() : base(0xE40)
    {
        Movable = false;
    }

    public LivingBritainStaticContainer(BritainStaticOverride definition) : this() => Apply(definition);

    public void Apply(BritainStaticOverride definition)
    {
        _overrideId = definition.Id;
        ItemID = definition.ItemId;
        Hue = definition.Hue;
        Name = string.IsNullOrWhiteSpace(definition.Name) ? "Behälter" : definition.Name;
        Locked = definition.Locked;
        Movable = false;
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainFunctionalItem : Item, ILivingBritainStatic
{
    [SerializableField(0)]
    private string _overrideId;

    [SerializableField(1)]
    private string _functionType;

    [SerializableField(2)]
    private int _baseItemId;

    [SerializableField(3)]
    private bool _active;

    [Constructible]
    public LivingBritainFunctionalItem() : base(0x1)
    {
        Movable = false;
    }

    public LivingBritainFunctionalItem(BritainStaticOverride definition) : this() => Apply(definition);

    public void Apply(BritainStaticOverride definition)
    {
        var functionType = LivingBritainStaticSemantics.NormalizeFunction(definition.FunctionType);
        if (_baseItemId != definition.ItemId || !string.Equals(_functionType, functionType, StringComparison.Ordinal))
        {
            _active = false;
        }

        _overrideId = definition.Id;
        _functionType = functionType;
        _baseItemId = definition.ItemId;
        Hue = definition.Hue;
        Name = string.IsNullOrWhiteSpace(definition.Name) ? null : definition.Name;
        Movable = false;
        ApplyState();
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        switch (_functionType)
        {
            case "seat":
                from.Animate(32, 5, 1, true, false, 0);
                from.SendMessage(0x3B2, "Ihr setzt Euch hin.");
                return;
            case "light":
                _active = !_active;
                Effects.PlaySound(GetWorldLocation(), Map, _active ? 0x47 : 0x3BE);
                from.SendMessage(0x3B2, _active ? "Ihr entzündet die Lichtquelle." : "Ihr löscht die Lichtquelle.");
                break;
            case "lever":
                _active = !_active;
                Effects.PlaySound(GetWorldLocation(), Map, 0x3E8);
                from.SendMessage(0x3B2, _active ? "Der Hebel rastet ein." : "Der Hebel kehrt zurück.");
                break;
            case "trapdoor":
                _active = !_active;
                Effects.PlaySound(GetWorldLocation(), Map, _active ? 0xEA : 0xF1);
                from.SendMessage(0x3B2, _active ? "Die Falltür öffnet sich." : "Die Falltür schließt sich.");
                break;
            default:
                return;
        }

        ApplyState();
        this.MarkDirty();
    }

    private void ApplyState()
    {
        ItemID = _active && _functionType is "lever" or "trapdoor" ? _baseItemId + 1 : _baseItemId;
        Light = _functionType == "light" && _active ? LightType.Circle225 : LightType.Empty;
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainSpinningWheel : Item, ILivingBritainStatic, ISpinningWheel
{
    [SerializableField(0)]
    private string _overrideId;

    [SerializableField(1)]
    private int _baseItemId;

    private Timer _spinTimer;

    [Constructible]
    public LivingBritainSpinningWheel() : base(0x1015)
    {
        _baseItemId = 0x1015;
        Movable = false;
    }

    public LivingBritainSpinningWheel(BritainStaticOverride definition) : this() => Apply(definition);

    public bool Spinning => _spinTimer != null;

    public void Apply(BritainStaticOverride definition)
    {
        _overrideId = definition.Id;
        _baseItemId = definition.ItemId;
        ItemID = _baseItemId;
        Hue = definition.Hue;
        Name = string.IsNullOrWhiteSpace(definition.Name) ? "Spinnrad" : definition.Name;
        Movable = false;
    }

    public void BeginSpin(SpinCallback callback, Mobile from, int hue)
    {
        if (_spinTimer != null)
        {
            return;
        }

        if (_baseItemId is 0x1015 or 0x1019)
        {
            ItemID = _baseItemId + 1;
        }

        Effects.PlaySound(GetWorldLocation(), Map, 0x21);
        _spinTimer = Timer.DelayCall(TimeSpan.FromSeconds(3), () =>
        {
            ItemID = _baseItemId;
            _spinTimer = null;
            callback?.Invoke(this, from, hue);
        });
    }

    public override void OnAfterDelete()
    {
        _spinTimer?.Stop();
        _spinTimer = null;
        base.OnAfterDelete();
    }
}

public readonly record struct LivingBritainDoorSpec(
    int ClosedId,
    int OpenedId,
    int OpenedSound,
    int ClosedSound,
    DoorFacing Facing
);

public enum LivingBritainStaticKind
{
    Item,
    WorldSimulation,
    Door,
    Container,
    Functional,
    SpinningWheel
}

public static class LivingBritainStaticSemantics
{
    public static ILivingBritainStatic Create(BritainStaticOverride definition)
    {
        var kind = Classify(definition);
        if (kind == LivingBritainStaticKind.Door && TryGetDoorSpec(definition.ItemId, out var spec))
        {
            return new LivingBritainStaticDoor(definition, spec);
        }

        return kind switch
        {
            LivingBritainStaticKind.WorldSimulation => new LivingBritainWorldSimulationItem(definition),
            LivingBritainStaticKind.Container     => new LivingBritainStaticContainer(definition),
            LivingBritainStaticKind.Functional    => new LivingBritainFunctionalItem(definition),
            LivingBritainStaticKind.SpinningWheel => new LivingBritainSpinningWheel(definition),
            _                                     => new LivingBritainStaticItem(definition)
        };
    }

    public static bool IsCompatible(ILivingBritainStatic item, BritainStaticOverride definition)
    {
        var kind = Classify(definition);
        return kind switch
        {
            LivingBritainStaticKind.WorldSimulation => item is LivingBritainWorldSimulationItem,
            LivingBritainStaticKind.Door =>
                TryGetDoorSpec(definition.ItemId, out var spec) && item is LivingBritainStaticDoor door && door.DoorClosedId == spec.ClosedId,
            LivingBritainStaticKind.Container     => item is LivingBritainStaticContainer,
            LivingBritainStaticKind.Functional    => item is LivingBritainFunctionalItem functional && functional.FunctionType == NormalizeFunction(definition.FunctionType),
            LivingBritainStaticKind.SpinningWheel => item is LivingBritainSpinningWheel,
            _                                     => item is LivingBritainStaticItem
        };
    }

    public static LivingBritainStaticKind Classify(BritainStaticOverride definition)
    {
        if (definition.Simulation?.Enabled == true)
        {
            return LivingBritainStaticKind.WorldSimulation;
        }

        var functionType = NormalizeFunction(definition.FunctionType);
        return functionType switch
        {
            "container"      => LivingBritainStaticKind.Container,
            "spinning-wheel" => LivingBritainStaticKind.SpinningWheel,
            "seat" or "light" or "lever" or "trapdoor" => LivingBritainStaticKind.Functional,
            _ when TryGetDoorSpec(definition.ItemId, out _) => LivingBritainStaticKind.Door,
            _ => LivingBritainStaticKind.Item
        };
    }

    public static string NormalizeFunction(string value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    public static bool IsSeat(int itemId) =>
        itemId is 0xA2A or 0xB5E ||
        itemId is >= 0xB4E and <= 0xB5D ||
        itemId is >= 0x1218 and <= 0x121B ||
        itemId is >= 0x2DE3 and <= 0x2DE6 ||
        itemId is >= 0x2DEB and <= 0x2DEE ||
        itemId is >= 0x2DF5 and <= 0x2DF6;

    public static bool TryGetDoorSpec(int itemId, out LivingBritainDoorSpec spec)
    {
        if (itemId is >= 0x675 and < 0x6F5)
        {
            var type = (itemId - 0x675) / 16;
            var family = 0x675 + type * 16;
            var sounds = type switch
            {
                0 or 1 or 5 => (Opened: 0xEC, Closed: 0xF3),
                2           => (Opened: 0xEB, Closed: 0xF2),
                _           => (Opened: 0xEA, Closed: 0xF1)
            };
            return CreatePairedSpec(itemId, family, sounds.Opened, sounds.Closed, out spec);
        }

        if (itemId is >= 0x314 and < 0x364)
        {
            var family = 0x314 + (itemId - 0x314) / 16 * 16;
            return CreatePairedSpec(itemId, family, 0xED, 0xF4, out spec);
        }

        if (CreatePairedSpec(itemId, 0x824, 0xEC, 0xF3, out spec, 0x834) ||
            CreatePairedSpec(itemId, 0x839, 0xEB, 0xF2, out spec, 0x849) ||
            CreatePairedSpec(itemId, 0x84C, 0xEC, 0xF3, out spec, 0x85C) ||
            CreatePairedSpec(itemId, 0x866, 0xEB, 0xF2, out spec, 0x876) ||
            CreatePairedSpec(itemId, 0x0E8, 0xED, 0xF4, out spec, 0x0F8) ||
            CreatePairedSpec(itemId, 0x1FED, 0xEC, 0xF3, out spec, 0x1FFD) ||
            itemId is >= 0x241F and < 0x2425 &&
            CreatePairedSpec(itemId, 0x2415, 0xEA, 0xF1, out spec, 0x2425))
        {
            return true;
        }

        spec = default;
        return false;
    }

    private static bool CreatePairedSpec(
        int itemId,
        int family,
        int openedSound,
        int closedSound,
        out LivingBritainDoorSpec spec,
        int exclusiveEnd = int.MaxValue
    )
    {
        if (itemId < family || itemId >= exclusiveEnd)
        {
            spec = default;
            return false;
        }

        var pair = (itemId - family) / 2;
        var closedId = family + pair * 2;
        var facing = (DoorFacing)(pair % 8);
        spec = new LivingBritainDoorSpec(closedId, closedId + 1, openedSound, closedSound, facing);
        return true;
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainStaticDoor : BaseDoor, ILivingBritainStatic
{
    [SerializableField(0)]
    private string _overrideId;

    [SerializableField(1)]
    private int _doorClosedId;

    public string DoorGroupId { get; private set; }

    [Constructible]
    public LivingBritainStaticDoor() : base(0x675, 0x676, 0xEC, 0xF3, GetOffset(DoorFacing.WestCW))
    {
        _doorClosedId = 0x675;
        Movable = false;
    }

    public LivingBritainStaticDoor(BritainStaticOverride definition, LivingBritainDoorSpec spec)
        : base(spec.ClosedId, spec.OpenedId, spec.OpenedSound, spec.ClosedSound, GetOffset(spec.Facing))
    {
        _doorClosedId = spec.ClosedId;
        Apply(definition);
    }

    public override bool UseChainedFunctionality => true;

    public void Apply(BritainStaticOverride definition)
    {
        if (Open)
        {
            Open = false;
        }

        _overrideId = definition.Id;
        DoorGroupId = definition.DoorGroupId;
        Hue = definition.Hue;
        Name = string.IsNullOrWhiteSpace(definition.Name) ? "Tür" : definition.Name;
        Movable = false;
        Locked = false;
    }
}
