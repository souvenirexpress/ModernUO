using System;
using System.Collections.Generic;
using System.Linq;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Server.Engines.LivingBritain;

public static class LivingBritainSimulation
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(LivingBritainSimulation));
    private static readonly List<LivingBritainResident> Residents = [];
    private static readonly Dictionary<LivingBritainResident, Point3D> ReservedDestinations = [];
    private static Timer _timer;
    private static int _cursor;
    private static bool _destinationsReported;

    public static void Configure()
    {
        EventSink.WorldLoad += OnWorldLoad;
        EventSink.WorldSave += OnWorldSave;
    }

    public static IReadOnlyList<LivingBritainResident> ActiveResidents => Residents;

    private static void OnWorldLoad()
    {
        var data = BritainData.Load();
        LivingBritainTerrain.Synchronize(data);
        LivingBritainInteriors.Synchronize(data);
        LivingBritainStatics.Synchronize(data);
        LivingBritainStudioSpawners.Synchronize();
        var known = new Dictionary<string, LivingBritainResident>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<LivingBritainResident>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not LivingBritainResident resident || resident.Deleted || string.IsNullOrWhiteSpace(resident.ProfileId))
            {
                continue;
            }

            if (!known.TryAdd(resident.ProfileId, resident))
            {
                duplicates.Add(resident);
            }
        }

        foreach (var duplicate in duplicates)
        {
            duplicate.Delete();
        }

        Residents.Clear();
        ReservedDestinations.Clear();
        foreach (var profile in data.Residents.OrderBy(x => x.Id))
        {
            if (!known.TryGetValue(profile.Id, out var resident))
            {
                resident = new LivingBritainResident(profile.Id);
                resident.MoveToWorld(profile.SpawnFallback.ToPoint3D(), Map.Felucca);
            }

            resident.BindProfile(profile);
            Residents.Add(resident);
        }

        _timer?.Stop();
        _cursor = 0;
        _destinationsReported = false;
        _timer = Timer.DelayCall(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), Tick);
        Logger.Information("Living Britain vertical slice active with {ResidentCount} residents.", Residents.Count);
    }

    internal static Point3D ReserveActivityDestination(LivingBritainResident resident, Point3D requested)
    {
        ReservedDestinations.Remove(resident);
        var map = resident.Map == null || resident.Map == Map.Internal ? Map.Felucca : resident.Map;

        foreach (var candidate in DestinationCandidates(requested, 6))
        {
            var reserved = ReservedDestinations.Values.Any(point =>
                point.X == candidate.X && point.Y == candidate.Y && Math.Abs(point.Z - candidate.Z) < 16
            );
            // Existing residents may still occupy their shared legacy destination while
            // slots are assigned. They must not make an otherwise walkable slot invalid.
            if (reserved || !map.CanFit(candidate, 16, false, false))
            {
                continue;
            }

            ReservedDestinations[resident] = candidate;
            return candidate;
        }

        ReservedDestinations[resident] = requested;
        return requested;
    }

    private static IEnumerable<Point3D> DestinationCandidates(Point3D center, int radius)
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

    private static void OnWorldSave()
    {
        Logger.Debug("Living Britain state included in world save for {ResidentCount} residents.", Residents.Count);
    }

    private static void Tick()
    {
        if (Residents.Count == 0)
        {
            return;
        }

        // Small staggered batches keep paths fluid without updating every resident in one slice.
        var count = Math.Min(4, Residents.Count);
        for (var i = 0; i < count; i++)
        {
            if (_cursor >= Residents.Count)
            {
                _cursor = 0;
            }

            var resident = Residents[_cursor++];
            if (!resident.Deleted)
            {
                resident.SimulationTick(Clock.WorldTime);
            }
        }

        if (!_destinationsReported && ReservedDestinations.Count == Residents.Count)
        {
            _destinationsReported = true;
            Logger.Information(
                "Living Britain activity slots ready: {UniqueDestinationCount}/{ResidentCount} unique, {MobileResidentCount} mobile, {StationaryResidentCount} stationary.",
                ReservedDestinations.Values.Distinct().Count(),
                Residents.Count,
                Residents.Count(resident => !resident.CantWalk),
                Residents.Count(resident => resident.CantWalk)
            );
        }
    }
}
