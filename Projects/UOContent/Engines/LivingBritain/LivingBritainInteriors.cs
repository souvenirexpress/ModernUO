using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Items;
using Server.Logging;

namespace Server.Engines.LivingBritain;

public interface ILivingBritainInterior
{
    string PlanId { get; }
    string ObjectId { get; }
    void Apply(BritainInteriorPlan plan, BritainInteriorObject definition);
}

public static class LivingBritainInteriors
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(LivingBritainInteriors));

    public static void Synchronize(BritainDataSet data)
    {
        var known = new Dictionary<string, ILivingBritainInterior>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<Item>();
        foreach (var item in World.Items.Values)
        {
            if (item is not ILivingBritainInterior interior || item.Deleted || string.IsNullOrWhiteSpace(interior.ObjectId))
            {
                continue;
            }

            if (!known.TryAdd(interior.ObjectId, interior))
            {
                duplicates.Add(item);
            }
        }

        foreach (var duplicate in duplicates)
        {
            duplicate.Delete();
        }

        var desired = data.Interiors
            .Where(plan => plan.ActiveVariant != null)
            .SelectMany(plan => plan.ActiveVariant.Rooms.SelectMany(room => room.Objects.Select(definition => (Plan: plan, Definition: definition))))
            .ToDictionary(x => x.Definition.StableId, StringComparer.OrdinalIgnoreCase);
        var interactionTiles = data.Interiors
            .Where(plan => plan.ActiveVariant != null)
            .SelectMany(plan => plan.ActiveVariant.Rooms)
            .SelectMany(room => room.InteractionPoints)
            .Select(point => new Point2D(point.X, point.Y))
            .ToHashSet();

        foreach (var stale in known.Where(x => !desired.ContainsKey(x.Key)).Select(x => x.Value).OfType<Item>().ToArray())
        {
            stale.Delete();
            known.Remove(((ILivingBritainInterior)stale).ObjectId);
        }

        var occupied = new HashSet<Point3D>();
        var unresolved = 0;
        var heightCorrections = 0;
        foreach (var pair in desired.Values.OrderBy(x => x.Definition.StableId))
        {
            var definition = pair.Definition;
            known.TryGetValue(definition.StableId, out var managed);
            var needsContainer = definition.Container;
            if (managed != null && needsContainer != managed.GetType().IsAssignableTo(typeof(LivingBritainInteriorContainer)))
            {
                ((Item)managed).Delete();
                managed = null;
            }

            managed ??= needsContainer
                ? new LivingBritainInteriorContainer(pair.Plan.Id, definition)
                : new LivingBritainInteriorItem(pair.Plan.Id, definition);
            managed.Apply(pair.Plan, definition);
            var item = (Item)managed;
            var requested = definition.ToPoint3D();
            var placement = FindPlacement(item, requested, interactionTiles, occupied);
            if (placement == null)
            {
                unresolved++;
                Logger.Warning(
                    "Living Britain interior placement unresolved: {ObjectId} ({ItemId}) requested at {Location}.",
                    definition.StableId,
                    definition.ItemId,
                    requested
                );
                if (item.Map != Map.Internal)
                {
                    item.MoveToWorld(Point3D.Zero, Map.Internal);
                }

                continue;
            }

            if (placement.Value.Z != requested.Z)
            {
                heightCorrections++;
            }

            occupied.Add(placement.Value);
            if (item.Map != Map.Felucca || item.Location != placement.Value)
            {
                item.MoveToWorld(placement.Value, Map.Felucca);
            }
        }

        var points = data.Interiors.Where(x => x.ActiveVariant != null).SelectMany(x => x.ActiveVariant.Rooms).Sum(x => x.InteractionPoints.Count);
        Logger.Information(
            "Living Britain interiors ready: {PlanCount} plans, {ObjectCount} persistent objects, {InteractionCount} interaction points, {CorrectionCount} height-corrected placements, {UnresolvedCount} unresolved placements.",
            data.Interiors.Count,
            desired.Count,
            points,
            heightCorrections,
            unresolved
        );
    }

    private static Point3D? FindPlacement(Item item, Point3D requested, HashSet<Point2D> interactions, HashSet<Point3D> occupied)
    {
        foreach (var horizontalCandidate in Candidates(requested, 6))
        {
            if (interactions.Contains(new Point2D(horizontalCandidate.X, horizontalCandidate.Y)))
            {
                continue;
            }

            foreach (var candidate in SurfaceCandidates(horizontalCandidate))
            {
                if (occupied.Contains(candidate))
                {
                    continue;
                }

                var height = Math.Max(1, item.ItemData.Height);
                if (Map.Felucca.CanFit(candidate, height, false, false))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<Point3D> SurfaceCandidates(Point3D candidate)
    {
        yield return candidate;
        var averageZ = Map.Felucca.GetAverageZ(candidate.X, candidate.Y);
        if (averageZ != candidate.Z)
        {
            yield return new Point3D(candidate.X, candidate.Y, averageZ);
        }
    }

    private static IEnumerable<Point3D> Candidates(Point3D center, int radius)
    {
        yield return center;
        for (var distance = 1; distance <= radius; distance++)
        {
            for (var dx = -distance; dx <= distance; dx++)
            {
                for (var dy = -distance; dy <= distance; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == distance)
                    {
                        yield return new Point3D(center.X + dx, center.Y + dy, center.Z);
                    }
                }
            }
        }
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainInteriorItem : Item, ILivingBritainInterior
{
    [SerializableField(0)]
    private string _planId;

    [SerializableField(1)]
    private string _objectId;

    [SerializableField(2)]
    private string _ownerNpcId;

    [SerializableField(3)]
    private string _access;

    [SerializableField(4)]
    private string _purpose;

    [SerializableField(5)]
    private string _reason;

    [Constructible]
    public LivingBritainInteriorItem() : base(0x1)
    {
        Movable = false;
    }

    public LivingBritainInteriorItem(string planId, BritainInteriorObject definition) : this() =>
        ApplyDefinition(planId, definition);

    void ILivingBritainInterior.Apply(BritainInteriorPlan plan, BritainInteriorObject definition) =>
        ApplyDefinition(plan.Id, definition);

    private void ApplyDefinition(string planId, BritainInteriorObject definition)
    {
        _planId = planId;
        _objectId = definition.StableId;
        _ownerNpcId = definition.OwnerNpcId;
        _access = definition.Access;
        _purpose = definition.Purpose;
        _reason = definition.Reason;
        ItemID = definition.ItemId;
        Hue = definition.Hue;
        Name = definition.Name;
        Movable = definition.Movable;
        LootType = definition.TheftStatus == "protected" ? LootType.Blessed : LootType.Regular;
        Light = definition.Light ? LightType.Circle150 : LightType.Empty;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(this, 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (_purpose is "sit" or "guest_seat")
        {
            from.Animate(32, 5, 1, true, false, 0);
        }

        from.SendMessage(0x3B2, string.IsNullOrWhiteSpace(_reason) ? $"Ihr benutzt {Name}." : _reason);
    }
}

[SerializationGenerator(0)]
public partial class LivingBritainInteriorContainer : LockableContainer, ILivingBritainInterior
{
    [SerializableField(0)]
    private string _planId;

    [SerializableField(1)]
    private string _objectId;

    [SerializableField(2)]
    private string _ownerNpcId;

    [SerializableField(3)]
    private string _access;

    [SerializableField(4)]
    private string _purpose;

    [SerializableField(5)]
    private string _reason;

    [Constructible]
    public LivingBritainInteriorContainer() : base(0x0E43)
    {
        Movable = false;
    }

    public LivingBritainInteriorContainer(string planId, BritainInteriorObject definition) : this()
    {
        ApplyDefinition(planId, definition);
        SeedContents(definition);
    }

    void ILivingBritainInterior.Apply(BritainInteriorPlan plan, BritainInteriorObject definition) =>
        ApplyDefinition(plan.Id, definition);

    private void ApplyDefinition(string planId, BritainInteriorObject definition)
    {
        _planId = planId;
        _objectId = definition.StableId;
        _ownerNpcId = definition.OwnerNpcId;
        _access = definition.Access;
        _purpose = definition.Purpose;
        _reason = definition.Reason;
        ItemID = definition.ItemId;
        Hue = definition.Hue;
        Name = definition.Name;
        Movable = definition.Movable;
        Locked = definition.Locked;
        LockLevel = definition.Locked ? 95 : 0;
        RequiredSkill = definition.Locked ? 95 : 0;
        LootType = definition.TheftStatus == "protected" ? LootType.Blessed : LootType.Regular;
        Light = definition.Light ? LightType.Circle150 : LightType.Empty;
    }

    private void SeedContents(BritainInteriorObject definition)
    {
        foreach (var content in definition.ContainerContents.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            switch (content)
            {
                case "bread": DropItem(new BreadLoaf()); break;
                case "cheese": DropItem(new CheeseWheel()); break;
                case "household_savings": DropItem(new Gold(Utility.RandomMinMax(8, 32))); break;
            }
        }
    }

    public override bool CheckLocked(Mobile from)
    {
        if (_access == "public" || from.AccessLevel >= AccessLevel.GameMaster)
        {
            return false;
        }

        return base.CheckLocked(from);
    }
}
