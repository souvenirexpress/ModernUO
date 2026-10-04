using System;
using Server;
using Server.Engines.Seating;
using Server.Engines.LivingBritain;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Tests.Engines.Seating;

[Collection("Sequential UOContent Tests")]
public class ChairSeatingTests
{
    [Theory]
    [InlineData(0x1218, 4)]
    [InlineData(0x1219, 2)]
    [InlineData(0x121A, 0)]
    [InlineData(0x121B, 6)]
    [InlineData(0xB57, 4)]
    [InlineData(0xE75, -1)]
    public void ArtworkDeterminesFacing(int itemId, int expected) =>
        Assert.Equal(expected, ChairSeating.Facing(itemId, Direction.South));

    [Fact]
    public void ChairsUseTheNormalItemDoubleClickPipeline()
    {
        foreach (var type in new[] { typeof(StoneChair), typeof(WoodenChair), typeof(BambooChair),
                     typeof(FancyWoodenChairCushion), typeof(WoodenChairCushion), typeof(Throne),
                     typeof(WoodenThrone), typeof(Stool), typeof(FootStool), typeof(WoodenBench) })
        {
            Assert.Equal(type, type.GetMethod(nameof(Item.OnDoubleClick))?.DeclaringType);
        }
    }

    [Fact]
    public void CarriedAndInternalChairsDoNotMoveThePlayer()
    {
        var player = new PlayerMobile { Body = 400 };
        var chair = new StoneChair();
        try
        {
            Assert.False(ChairSeating.TrySit(player, chair));
            player.AddItem(chair);
            Assert.False(ChairSeating.TrySit(player, chair));
        }
        finally
        {
            chair.Delete();
            player.Delete();
        }
    }

    [SkippableFact]
    public void UseSitsAtChairAndRejectsOccupiedDistantAndBlockedSeats()
    {
        Skip.If(!TestServerInitializer.TileDataLoaded, "Requires locally supplied UO map data.");
        var map = Map.Maps[1];
        var player = new PlayerMobile { Body = 400 };
        var other = new PlayerMobile { Body = 401 };
        var chair = new StoneChair { Movable = false };
        try
        {
            var z = map.GetAverageZ(1498, 1598);
            var seat = new Point3D(1498, 1598, z);
            var start = new Point3D(1499, 1598, z);
            chair.MoveToWorld(seat, map);
            player.MoveToWorld(start, map);
            player.Use(chair);
            Assert.Equal(seat, player.Location);
            Assert.Equal(Direction.South, player.Direction);
            player.Location = start;
            other.MoveToWorld(seat, map);
            Assert.False(ChairSeating.TrySit(player, chair));
            Assert.Equal(start, player.Location);
            other.Internalize();
            player.Location = new Point3D(1495, 1598, z);
            Assert.False(ChairSeating.TrySit(player, chair));
            player.Location = start;
            player.Frozen = true;
            Assert.False(ChairSeating.TrySit(player, chair));
            player.Frozen = false;
            player.Warmode = true;
            Assert.False(ChairSeating.TrySit(player, chair));
            player.Warmode = false;
            var wall = new Item(0x80) { Movable = false };
            try
            {
                wall.MoveToWorld(seat, map);
                Assert.False(ChairSeating.TrySit(player, chair));
            }
            finally
            {
                wall.Delete();
            }
            chair.Z = z + 10;
            Assert.False(ChairSeating.TrySit(player, chair));
        }
        finally
        {
            chair.Delete();
            player.Delete();
            other.Delete();
        }
    }

    [SkippableFact]
    public void StudioChairsSitAndUnsafeSimulationChairsRemainUnusable()
    {
        Skip.If(!TestServerInitializer.TileDataLoaded, "Requires locally supplied UO map data.");
        var map = Map.Maps[1];
        var player = new PlayerMobile { Body = 401 };
        var chair = new LivingBritainWorldSimulationItem { ItemID = 0x1219 };
        var plain = new LivingBritainStaticItem { ItemID = 0x121A };
        var functional = new LivingBritainFunctionalItem(new BritainStaticOverride { Id = "seat-test", ItemId = 0x121B, FunctionType = "seat" });
        try
        {
            var z = map.GetAverageZ(1498, 1598);
            var seat = new Point3D(1498, 1598, z);
            var start = new Point3D(1499, 1598, z);
            player.MoveToWorld(start, map);
            foreach (var item in new Item[] { chair, plain, functional })
            {
                item.MoveToWorld(seat, map);
                player.Use(item);
                Assert.Equal(seat, player.Location);
                Assert.Equal((Direction)ChairSeating.Facing(item.ItemID, Direction.South), player.Direction);
                item.Internalize();
                player.Location = start;
            }
            chair.MoveToWorld(seat, map);
            chair.State.Temperature = 100;
            player.Use(chair);
            Assert.Equal(start, player.Location);
            chair.State.Temperature = 20;
            chair.State.CombustionIntensity = 1;
            player.Use(chair);
            Assert.Equal(start, player.Location);
            chair.State.CombustionIntensity = 0;
            chair.State.Condition = 0;
            player.Use(chair);
            Assert.Equal(start, player.Location);
        }
        finally
        {
            chair.Delete();
            plain.Delete();
            functional.Delete();
            player.Delete();
        }
    }
}
