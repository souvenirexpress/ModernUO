using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Server.Accounting;
using Server.Mobiles;

namespace Server.Engines.ImageWorld;

public static class ImageWorldSystem
{
    private const string Marker = "Abadoria.ImageWorld.v1.";
    public static ImageWorldGeometry Geometry { get; private set; }
    public static Map SceneMap { get; private set; }
    public static bool StartEnabled { get; private set; }
    public static CityInfo StartCity => new("Sternenfallbucht", "Westzugang", Geometry.Start.X, Geometry.Start.Y, Geometry.Start.Z, SceneMap);

    [CallPriority(3)]
    public static void Configure()
    {
        StartEnabled = string.Equals(Environment.GetEnvironmentVariable("ABADORIA_IMAGE_WORLD_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);
        var root = Path.Combine(Core.BaseDirectory, "Data", "ImageWorld");
        var path = Path.Combine(root, "world.json");
        if (!File.Exists(path) && !StartEnabled)
        {
            return;
        }
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != ImageWorldGeometry.DefinitionHash)
        {
            throw new InvalidDataException("Image world geometry hash mismatch.");
        }
        Geometry = ImageWorldGeometry.Parse(Encoding.UTF8.GetString(bytes));
        if (Geometry.Count != 1479 || Map.Maps[ImageWorldGeometry.MapId] != null)
        {
            throw new InvalidDataException("Image world geometry or map slot is invalid.");
        }
        WriteNativeFiles(root);
        ServerConfiguration.DataDirectories.Add(root);
        SceneMap = new Map(ImageWorldGeometry.MapId, ImageWorldGeometry.MapId, ImageWorldGeometry.MapId, 256, 256, 0, "Sternenfallbucht", MapRules.TrammelRules);
        Map.Maps[ImageWorldGeometry.MapId] = SceneMap;
        Map.AllMaps.Add(SceneMap);
    }

    private static void WriteNativeFiles(string root)
    {
        // Only this new map is generated. Existing native maps and saves stay intact.
        using var map = new BinaryWriter(File.Create(Path.Combine(root, "map32.mul")));
        using var index = new BinaryWriter(File.Create(Path.Combine(root, "staidx32.mul")));
        using var statics = File.Create(Path.Combine(root, "statics32.mul"));
        for (var bx = 0; bx < 32; bx++)
        {
            for (var by = 0; by < 32; by++)
            {
                map.Write(0);
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var walkable = Geometry.TryCell(bx * 8 + x, by * 8 + y, out var z);
                        map.Write((ushort)(walkable ? 3 : 0xA8));
                        map.Write((sbyte)z);
                    }
                }
                index.Write(-1); index.Write(-1); index.Write(-1);
            }
        }
    }

    public static void PrepareLogin(PlayerMobile player)
    {
        if (!StartEnabled || SceneMap == null || player.Account is not Account account)
        {
            return;
        }
        var key = Marker + player.Serial.Value;
        if (account.GetTag(key) == null)
        {
            // Preserve the former position as an audit/rollback reference, once per character.
            var oldMap = player.Map == Map.Internal ? player.LogoutMap : player.Map;
            var old = player.Map == Map.Internal ? player.LogoutLocation : player.Location;
            player.MoveToWorld(Geometry.Start, SceneMap);
            account.SetTag(key, $"{oldMap?.MapIndex ?? 127},{old.X},{old.Y},{old.Z}");
        }
        else if (player.Map == SceneMap && !Geometry.Contains(player.Location))
        {
            player.MoveToWorld(Geometry.Start, SceneMap);
        }
    }

    public static bool CheckMovement(Mobile mobile, Point3D from, Direction direction, out int z)
    {
        var to = from;
        Server.Movement.Movement.Offset(direction, ref to);
        if (!Geometry.TryStep(from, to.X - from.X, to.Y - from.Y, out z))
        {
            return false;
        }
        foreach (var item in SceneMap.GetItemsInRange(to, 0))
        {
            if (item.Parent == null && item.ItemData.Impassable && item.Z < z + 16 && item.Z + Math.Max(1, item.ItemData.Height) > z)
            {
                return false;
            }
        }
        return true;
    }
}
