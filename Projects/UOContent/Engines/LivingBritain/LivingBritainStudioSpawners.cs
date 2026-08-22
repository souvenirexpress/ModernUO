using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Engines.Spawners;
using Server.Json;
using Server.Logging;

namespace Server.Engines.LivingBritain;

/// <summary>
/// Imports the World Studio spawn export into an existing save during WorldLoad.
/// ModernUO's JSON files are normally only imported by an administrator command,
/// so deployments would otherwise leave the JSON on disk without live spawners.
/// </summary>
public static class LivingBritainStudioSpawners
{
    private const string NamePrefix = "Abadoria Studio:";
    private const string RelativePath = "Data/Spawns/shared/felucca/world-studio.json";
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(LivingBritainStudioSpawners));

    public static void Synchronize()
    {
        var path = Path.Combine(Core.BaseDirectory, RelativePath);
        if (!File.Exists(path))
        {
            RemoveManagedSpawners([]);
            Logger.Information("Living Britain studio spawners ready: no export found.");
            return;
        }

        List<SpawnerDto> definitions;
        try
        {
            definitions = JsonConfig.Deserialize<List<SpawnerDto>>(path, SpawnerJsonSerializer.Options) ?? [];
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Living Britain could not read studio spawners from {Path}.", path);
            return;
        }

        var desired = definitions
            .Where(definition =>
                definition.Guid != Guid.Empty &&
                definition.Map != null &&
                definition.Map != Map.Internal &&
                definition.Name?.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase) == true
            )
            .ToDictionary(definition => definition.Guid);

        var managed = World.Items.Values
            .OfType<BaseSpawner>()
            .Where(spawner =>
                !spawner.Deleted &&
                spawner.Name?.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase) == true
            )
            .ToDictionary(spawner => spawner.Guid);

        var removed = RemoveManagedSpawners(desired.Keys, managed);
        var created = 0;
        var updated = 0;

        foreach (var definition in desired.Values.OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (managed.Remove(definition.Guid, out var existing))
            {
                existing.Delete();
                updated++;
            }

            try
            {
                var spawner = definition.ToSpawner();
                spawner.MoveToWorld(definition.Location, definition.Map);
                spawner.Respawn();
                created++;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Living Britain could not create studio spawner {SpawnerName}.", definition.Name);
            }
        }

        Logger.Information(
            "Living Britain studio spawners ready: {DefinitionCount} definitions, {CreatedCount} created, {UpdatedCount} updated, {RemovedCount} removed.",
            desired.Count,
            created,
            updated,
            removed
        );
    }

    private static int RemoveManagedSpawners(IEnumerable<Guid> desiredIds, Dictionary<Guid, BaseSpawner> managed = null)
    {
        var desired = desiredIds.ToHashSet();
        managed ??= World.Items.Values
            .OfType<BaseSpawner>()
            .Where(spawner =>
                !spawner.Deleted &&
                spawner.Name?.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase) == true
            )
            .ToDictionary(spawner => spawner.Guid);

        var removed = 0;
        foreach (var stale in managed.Where(pair => !desired.Contains(pair.Key)).Select(pair => pair.Value).ToArray())
        {
            stale.Delete();
            removed++;
        }

        return removed;
    }
}
