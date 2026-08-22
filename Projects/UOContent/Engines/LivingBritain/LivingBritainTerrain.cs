using System;
using System.Linq;
using Server.Logging;

namespace Server.Engines.LivingBritain;

public static class LivingBritainTerrain
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(LivingBritainTerrain));

    public static void Synchronize(BritainDataSet data)
    {
        var definitions = data?.TerrainOverrides ?? [];
        foreach (var block in definitions.GroupBy(definition => (X: definition.X >> 3, Y: definition.Y >> 3)))
        {
            var tiles = (LandTile[])Map.Felucca.Tiles.GetLandBlock(block.Key.X, block.Key.Y).Clone();
            foreach (var definition in block)
            {
                var index = ((definition.Y & 0x7) << 3) + (definition.X & 0x7);
                tiles[index] = new LandTile((short)definition.LandId, (sbyte)definition.Z);
            }
            Map.Felucca.Tiles.SetLandBlock(block.Key.X, block.Key.Y, tiles);
        }

        Logger.Information("Living Britain terrain ready: {TerrainOverrideCount} overrides applied to {BlockCount} map blocks.", definitions.Count, definitions.Select(definition => (definition.X >> 3, definition.Y >> 3)).Distinct().Count());
    }
}
