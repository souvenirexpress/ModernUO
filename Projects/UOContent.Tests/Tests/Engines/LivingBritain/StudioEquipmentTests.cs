using System;
using System.Buffers;
using System.Text.Json;
using Server;
using Server.Engines.LivingBritain;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Tests.Engines.LivingBritain;

[Collection("Sequential UOContent Tests")]
public class StudioEquipmentTests
{
    private static BritainStaticOverride Definition(bool simulated = true) => new()
    {
        Id = "studio-equipment-test", Operation = "add", Name = "Battle Axe",
        ItemId = 0xF48, Movable = true, Equippable = true, EquipLayer = 1, AnimationId = 615,
        Simulation = simulated ? new BritainWorldSimulationDefinition { Enabled = true, Material = "Steel" } : null
    };

    private static BritainDataSet Data(BritainStaticOverride definition) => new([], [], [], [], [], [], [definition], []);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportedEquipmentCanBeEquippedOnTheDeclaredLayer(bool simulated)
    {
        var definition = JsonSerializer.Deserialize<BritainStaticOverride>(JsonSerializer.Serialize(Definition(simulated)))!;
        var item = (Item)LivingBritainStaticSemantics.Create(definition);
        var actor = new PlayerMobile { Body = 400 };
        try
        {
            Assert.True(item.Movable);
            Assert.Equal(Layer.OneHanded, item.Layer);
            Assert.True(actor.EquipItem(item));
            Assert.Same(actor, item.Parent);
            Assert.Same(item, actor.FindItemOnLayer(Layer.OneHanded));
        }
        finally { item.Delete(); actor.Delete(); }
    }

    [Fact]
    public void EquipAttemptRepairsAnOldCarriedAxeFromTheCurrentExport()
    {
        var currentProperty = typeof(BritainData).GetProperty(nameof(BritainData.Current))!;
        var previous = BritainData.Current;
        var definition = Definition();
        var actor = new PlayerMobile { Body = 400 };
        var bag = new Backpack();
        var axe = new LivingBritainWorldSimulationItem(definition) { Layer = Layer.Invalid, Condition = 0.7 };
        try
        {
            currentProperty.SetValue(null, Data(definition));
            actor.AddItem(bag);
            bag.DropItem(axe);
            Assert.True(actor.EquipItem(axe));
            Assert.Same(actor, axe.Parent);
            Assert.Equal(Layer.OneHanded, axe.Layer);
            Assert.Equal(0.7, axe.Condition);
        }
        finally
        {
            currentProperty.SetValue(null, previous);
            axe.Delete(); bag.Delete(); actor.Delete();
        }
    }

    [Fact]
    public void MissingLayerRepairPreservesCarriedObjectIdentityAndSimulationState()
    {
        var definition = Definition();
        var item = new LivingBritainWorldSimulationItem(definition) { Layer = Layer.Invalid, Condition = 0.63, Sharpness = 0.42, Hue = 10 };
        var actor = new PlayerMobile { Body = 400 };
        var bag = new Backpack();
        try
        {
            actor.AddItem(bag);
            bag.DropItem(item);
            var serial = item.Serial;
            var location = item.Location;
            LivingBritainEquipment.RefreshMissingLayer(item, item.OverrideId, Data(definition));
            Assert.Equal(serial, item.Serial);
            Assert.Equal(location, item.Location);
            Assert.Same(bag, item.Parent);
            Assert.Equal(0.63, item.Condition);
            Assert.Equal(0.42, item.Sharpness);
            Assert.Equal(10, item.Hue);
            Assert.True(actor.EquipItem(item));
        }
        finally { item.Delete(); bag.Delete(); actor.Delete(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(255)]
    public void InvalidSlotsAreNotEquippable(int layer)
    {
        var item = new LivingBritainWorldSimulationItem(Definition() with { EquipLayer = layer });
        var actor = new PlayerMobile { Body = 400 };
        try { Assert.False(actor.EquipItem(item)); }
        finally { item.Delete(); actor.Delete(); }
    }

    [Fact]
    public void DecorationOrRemovedDefinitionCannotRepairAnInvalidLayer()
    {
        var item = new LivingBritainWorldSimulationItem(Definition()) { Layer = Layer.Invalid };
        try
        {
            LivingBritainEquipment.RefreshMissingLayer(item, item.OverrideId, Data(Definition() with { Equippable = false }));
            Assert.Equal(Layer.Invalid, item.Layer);
            LivingBritainEquipment.RefreshMissingLayer(item, item.OverrideId, Data(Definition() with { Operation = "remove" }));
            Assert.Equal(Layer.Invalid, item.Layer);
            LivingBritainEquipment.RefreshMissingLayer(item, "missing", Data(Definition()));
            Assert.Equal(Layer.Invalid, item.Layer);
        }
        finally { item.Delete(); }
    }

    [Fact]
    public void OccupiedHandCannotBeOverwritten()
    {
        var actor = new PlayerMobile { Body = 400 };
        var dagger = new Dagger();
        var axe = new LivingBritainWorldSimulationItem(Definition());
        try
        {
            Assert.True(actor.EquipItem(dagger));
            Assert.False(actor.EquipItem(axe));
            Assert.Same(dagger, actor.FindItemOnLayer(Layer.OneHanded));
        }
        finally { dagger.Delete(); axe.Delete(); actor.Delete(); }
    }

    [SkippableFact]
    public void StandardLiftAndEquipPacketsEquipTheStudioAxe()
    {
        Skip.If(!TestServerInitializer.TileDataLoaded, "Requires locally supplied UO map data.");
        var actor = new PlayerMobile { Body = 400, Str = 100 };
        var axe = new LivingBritainWorldSimulationItem(Definition());
        var bag = new Backpack();
        using var state = PacketTestUtilities.CreateTestNetState();
        try
        {
            var map = Map.Maps[1];
            actor.MoveToWorld(new Point3D(1499, 1598, map.GetAverageZ(1499, 1598)), map);
            actor.AddItem(bag);
            bag.DropItem(axe);
            state.Mobile = actor;
            var lift = new SpanWriter(stackalloc byte[6]);
            lift.Write(axe.Serial.Value); lift.Write((ushort)1);
            IncomingItemPackets.LiftReq(state, new SpanReader(lift.Span));
            Assert.Same(axe, actor.Holding);
            var equip = new SpanWriter(stackalloc byte[9]);
            equip.Write(axe.Serial.Value); equip.Write((byte)1); equip.Write(actor.Serial.Value);
            IncomingItemPackets.EquipReq(state, new SpanReader(equip.Span));
            Assert.Null(actor.Holding);
            Assert.Same(actor, axe.Parent);
            Assert.Same(axe, actor.FindItemOnLayer(Layer.OneHanded));
        }
        finally { state.Mobile = null; actor.NetState = null; axe.Delete(); bag.Delete(); actor.Delete(); }
    }
}
