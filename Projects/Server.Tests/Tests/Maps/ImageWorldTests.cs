using System;
using System.Text.Json;
using Server.Engines.ImageWorld;
using Xunit;

namespace Server.Tests.Maps;

public class ImageWorldTests
{
    private static ImageWorldGeometry Geometry(params int[][] cells) => ImageWorldGeometry.Parse(JsonSerializer.Serialize(new
    {
        version = 1, id = "sternenfallbucht-1", mapId = 32, width = 256, height = 256,
        imageSha256 = ImageWorldGeometry.ImageHash, start = cells[0], cells
    }));

    [Fact]
    public void MovementRequiresTheRealStartHeightAndARealAdjacentCell()
    {
        var g = Geometry([2, 2, 4], [3, 2, 6], [4, 2, 9]);
        Assert.True(g.TryStep(new Point3D(2, 2, 4), 1, 0, out var z));
        Assert.Equal(6, z);
        Assert.False(g.TryStep(new Point3D(2, 2, 0), 1, 0, out _));
        Assert.False(g.TryStep(new Point3D(2, 2, 4), 2, 0, out _));
        Assert.False(g.TryStep(new Point3D(3, 2, 6), 1, 0, out _));
        Assert.False(g.TryStep(new Point3D(2, 2, 4), 0, 1, out _));
        Assert.False(g.TryCell(-1, 2, out _));
        Assert.False(g.TryCell(256, 2, out _));
    }

    [Fact]
    public void DiagonalCannotCutEitherMissingOrHighCorner()
    {
        Assert.False(Geometry([2, 2, 4], [3, 3, 4]).TryStep(new Point3D(2, 2, 4), 1, 1, out _));
        Assert.False(Geometry([2, 2, 4], [3, 3, 4], [3, 2, 4], [2, 3, 8]).TryStep(new Point3D(2, 2, 4), 1, 1, out _));
        Assert.True(Geometry([2, 2, 4], [3, 3, 6], [3, 2, 5], [2, 3, 5]).TryStep(new Point3D(2, 2, 4), 1, 1, out var z));
        Assert.Equal(6, z);
    }

    [Fact]
    public void InvalidOrDuplicateCoordinatesCannotChangeAuthoritativeGeometry()
    {
        Assert.Throws<ArgumentException>(() => Geometry([2, 2, 4], [2, 2, 6]));
        Assert.Throws<ArgumentException>(() => Geometry([2, 2, 128]));
        Assert.Throws<ArgumentException>(() => Geometry([256, 2, 4]));
    }
}
