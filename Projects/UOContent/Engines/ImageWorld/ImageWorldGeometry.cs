using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Server.Engines.ImageWorld;

public sealed class ImageWorldGeometry
{
    public const int MapId = 32;
    public const string DefinitionHash = "6fa142d988d495a34a47f95c1b14eacfa9426962e4fb8d6bf0ab1ab73980bb25";
    public const string ImageHash = "e45bc058b0012013db7a7c0243811fd032e5703b448363e3665583dbce7871c0";
    private readonly Dictionary<int, int> _cells = new();
    public Point3D Start { get; private init; }
    public int Count => _cells.Count;

    public static ImageWorldGeometry Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("id").GetString() != "sternenfallbucht-1" ||
            root.GetProperty("mapId").GetInt32() != MapId || root.GetProperty("width").GetInt32() != 256 ||
            root.GetProperty("height").GetInt32() != 256 || root.GetProperty("imageSha256").GetString() != ImageHash)
        {
            throw new ArgumentException("Unexpected image world contract.");
        }
        var start = root.GetProperty("start");
        var geometry = new ImageWorldGeometry { Start = new Point3D(start[0].GetInt32(), start[1].GetInt32(), start[2].GetInt32()) };
        foreach (var cell in root.GetProperty("cells").EnumerateArray())
        {
            var x = cell[0].GetInt32(); var y = cell[1].GetInt32(); var z = cell[2].GetInt32();
            if (cell.GetArrayLength() != 3 || x is < 0 or > 255 || y is < 0 or > 255 || z is < -128 or > 127 || !geometry._cells.TryAdd(y * 256 + x, z))
            {
                throw new ArgumentException("Invalid image world cell.");
            }
        }
        if (!geometry.Contains(geometry.Start))
        {
            throw new ArgumentException("Image world start is blocked.");
        }
        return geometry;
    }

    public bool TryCell(int x, int y, out int z)
    {
        z = 0;
        return x is >= 0 and < 256 && y is >= 0 and < 256 && _cells.TryGetValue(y * 256 + x, out z);
    }
    public bool Contains(Point3D p) => TryCell(p.X, p.Y, out var z) && z == p.Z;
    public bool TryStep(Point3D from, int dx, int dy, out int z)
    {
        z = from.Z;
        if (!Contains(from) || dx is < -1 or > 1 || dy is < -1 or > 1 || dx == 0 && dy == 0 ||
            !TryCell(from.X + dx, from.Y + dy, out var next) || Math.Abs(next - from.Z) > 2)
        {
            return false;
        }
        if (dx != 0 && dy != 0 && (!TryCell(from.X + dx, from.Y, out var a) || !TryCell(from.X, from.Y + dy, out var b) ||
            Math.Abs(a - from.Z) > 2 || Math.Abs(b - from.Z) > 2 || Math.Abs(a - next) > 2 || Math.Abs(b - next) > 2))
        {
            return false;
        }
        z = next;
        return true;
    }
}
