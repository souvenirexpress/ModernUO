using System;
using System.IO;
using System.Linq;
using System.Buffers;
using Server.Engines.LivingBritain;
using Server.Engines.WorldSimulation;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;

namespace Server.Tests;

[Collection("Sequential UOContent Tests")]
public class WorldInteractionTests
{
    public WorldInteractionTests() => MaterialRegistry.Load(Path.Combine(Core.BaseDirectory, "Data", "world-simulation", "materials.json"));

    private static LivingBritainWorldSimulationItem Tool(double sharpness = 1, double condition = 1) =>
        new(new BritainStaticOverride
        {
            Id = "interaction-tool", ItemId = 0xF43, Movable = true,
            Simulation = new BritainWorldSimulationDefinition
            {
                Enabled = true, Material = "Steel",
                State = new BritainWorldSimulationState { Sharpness = sharpness, Condition = condition },
                Capabilities = new BritainWorldSimulationCapabilities { CutPower = 0.9, ChopPower = 1 }
            }
        });

    private static InteractionContext Context(object source, object target, WorldInteractionAction action, Mobile actor = null) =>
        new() { Source = source, Target = target, Action = action, Actor = actor };

    [Theory]
    [InlineData(WorldInteractionAction.Cut)]
    [InlineData(WorldInteractionAction.Chop)]
    public void MechanicalActionsDamageMaterialWearToolAndPreserveIdentity(WorldInteractionAction action)
    {
        var tool = Tool();
        var target = new DryOakLog();
        try
        {
            var serial = target.Serial;
            var result = InteractionResolver.Resolve(Context(tool, target, action));
            Assert.True(result.Success);
            Assert.InRange(target.Condition, 0.1, 0.999);
            Assert.True(target.Cracking > 0);
            Assert.True(tool.Condition < 1 && tool.Sharpness < 1);
            Assert.Equal(serial, target.Serial);
            Assert.False(target.Deleted);
            Assert.Empty(result.GeneratedObjects);
        }
        finally { tool.Delete(); target.Delete(); }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(0.1, 0.1)]
    public void BluntBrokenAndWeakToolsAreNeitherOfferedNorExecuted(double sharpness, double condition)
    {
        var tool = Tool(sharpness, condition);
        var target = new DryOakLog();
        try
        {
            Assert.DoesNotContain(WorldInteractionAction.Chop, AffordanceResolver.GetAvailableActions(null, tool, target, new()));
            Assert.False(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Chop)).Success);
            Assert.Equal(1, target.Condition);
        }
        finally { tool.Delete(); target.Delete(); }
    }

    [Fact]
    public void UncuttableMaterialsAndStacksAreRejected()
    {
        var knife = new Dagger();
        var stone = new SimulationStone();
        var cloth = new SimulationCloth { Amount = 3 };
        var water = new SimulationWater();
        try
        {
            foreach (var target in new WorldSimulationItem[] { stone, cloth, water })
            {
                Assert.False(InteractionResolver.Resolve(Context(knife, target, WorldInteractionAction.Cut)).Success);
                Assert.Equal(1, target.Condition);
            }
        }
        finally { knife.Delete(); stone.Delete(); cloth.Delete(); water.Delete(); }
    }

    [Fact]
    public void DestroyedTargetCannotProduceRepeatedDamageOrExtraObjects()
    {
        var tool = Tool();
        var target = new SimulationCloth { Condition = 0.01 };
        try
        {
            Assert.True(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Cut)).Success);
            Assert.Equal(0, target.Condition);
            var wear = tool.Sharpness;
            Assert.False(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Cut)).Success);
            Assert.Equal(wear, tool.Sharpness);
            Assert.False(target.Deleted);
        }
        finally { tool.Delete(); target.Delete(); }
    }

    [Fact]
    public void OffersAndExplanationsAgreeForEveryActionAndSource()
    {
        var target = new DryOakLog();
        var tool = Tool();
        var water = new SimulationWater();
        var torch = new SimulationTorch { Burning = true };
        try
        {
            foreach (var source in new Item[] { tool, water, torch })
            {
                var offered = AffordanceResolver.GetAvailableActions(null, source, target, new());
                foreach (var action in Enum.GetValues<WorldInteractionAction>())
                {
                    Assert.Equal(InteractionResolver.Explain(Context(source, target, action)).Allowed, offered.Contains(action));
                }
            }
            Assert.DoesNotContain(WorldInteractionAction.Carry, AffordanceResolver.GetAvailableActions(null, tool, target, new()));
            Assert.False(InteractionResolver.Resolve(Context(tool, tool, WorldInteractionAction.Cut)).Success);
        }
        finally { target.Delete(); tool.Delete(); water.Delete(); torch.Delete(); }
    }

    [Fact]
    public void StandardKnifeAndAxeAreAdaptedAndBrokenWeaponCannotWork()
    {
        var knife = new Dagger { MaxHitPoints = 10, HitPoints = 10 };
        var axe = new Axe();
        var target = new SimulationCloth();
        try
        {
            Assert.True(SourceCapabilityResolver.GetCapabilities(axe).ChopPower > 0);
            Assert.True(InteractionResolver.Resolve(Context(knife, target, WorldInteractionAction.Cut)).Success);
            Assert.Equal(9, knife.HitPoints);
            knife.HitPoints = 0;
            Assert.False(InteractionResolver.Resolve(Context(knife, target, WorldInteractionAction.Cut)).Success);
        }
        finally { knife.Delete(); axe.Delete(); target.Delete(); }
    }

    [SkippableFact]
    public void NetworkQueryAndExecutionUseTheSameRulesAndRejectStaleTargets()
    {
        Skip.If(!TestServerInitializer.TileDataLoaded, "Requires locally supplied UO map data.");
        var actor = new PlayerMobile { Body = 400 };
        var target = new DryOakLog();
        var tool = Tool();
        using var state = PacketTestUtilities.CreateTestNetState();
        try
        {
            var map = Map.Maps[1];
            var z = map.GetAverageZ(1498, 1598);
            actor.MoveToWorld(new Point3D(1499, 1598, z), map);
            actor.AddItem(tool);
            target.MoveToWorld(new Point3D(1498, 1598, z), map);
            state.Mobile = actor;
            var query = new SpanWriter(stackalloc byte[9]);
            query.Write((byte)0); query.Write(123u); query.Write(target.Serial.Value);
            WorldInteractionPackets.Receive(state, new SpanReader(query.Span));
            var response = state.SendBuffer.GetReadSpan();
            Assert.Equal(0xbf, response[0]);
            Assert.Equal(0x81, response[4]);
            Assert.Equal(1, response[14]);
            Assert.Equal(3, response[15]); // Inspect, Cut, Chop
            Assert.Equal((byte)WorldInteractionAction.Inspect, response[16]);

            var command = new SpanWriter(stackalloc byte[14]);
            command.Write((byte)1); command.Write(123u); command.Write(target.Serial.Value);
            command.Write(tool.Serial.Value); command.Write((byte)WorldInteractionAction.Chop);
            WorldInteractionPackets.Receive(state, new SpanReader(command.Span));
            Assert.True(target.Condition < 1);
            var condition = target.Condition;
            actor.EndAction(typeof(InteractionResolver));
            target.X += 5;
            WorldInteractionPackets.Receive(state, new SpanReader(command.Span));
            Assert.Equal(condition, target.Condition);
            WorldInteractionPackets.Receive(state, new SpanReader(command.Span[..^1]));
            Assert.Equal(condition, target.Condition);
        }
        finally { state.Mobile = null; actor.NetState = null; tool.Delete(); target.Delete(); actor.Delete(); }
    }

    [Fact]
    public void LegacyWaterConsumptionIsOwnedByTheResolverAndCannotBeRepeatedWhenEmpty()
    {
        var water = new Pitcher(BeverageType.Water) { Quantity = 1 };
        var target = new DryOakLog();
        try
        {
            Assert.True(InteractionResolver.Resolve(Context(water, target, WorldInteractionAction.Cool)).Success);
            Assert.Equal(0, water.Quantity);
            var moisture = target.Moisture;
            Assert.False(InteractionResolver.Resolve(Context(water, target, WorldInteractionAction.Cool)).Success);
            Assert.Equal(moisture, target.Moisture);
        }
        finally { water.Delete(); target.Delete(); }
    }

    [Fact]
    public void SchedulerCanUnregisterTheFinalCoolingItemDuringItsTick()
    {
        WorldSimulationSystem.ResetForTesting();
        var target = new DryOakLog { Temperature = 20.5001, Moisture = 0 };
        try
        {
            target.OnSimulationStateChanged();
            Assert.Equal(1, WorldSimulationSystem.ActiveCount);
            typeof(WorldSimulationSystem).GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null);
            Assert.Equal(0, WorldSimulationSystem.ActiveCount);
        }
        finally { target.Delete(); }
    }

    [SkippableFact]
    public void MenuAndExecutionRecheckRangeOwnershipStateAndCooldown()
    {
        Skip.If(!TestServerInitializer.TileDataLoaded, "Requires locally supplied UO map data.");
        var map = Map.Maps[1];
        var actor = new PlayerMobile { Body = 400 };
        var other = new PlayerMobile { Body = 400 };
        var tool = Tool();
        var target = new DryOakLog();
        try
        {
            var z = map.GetAverageZ(1498, 1598);
            actor.MoveToWorld(new Point3D(1499, 1598, z), map);
            other.MoveToWorld(actor.Location, map);
            target.MoveToWorld(new Point3D(1498, 1598, z), map);
            actor.AddItem(tool);
            Assert.Contains(WorldInteractionMenu.GetOffers(actor, target), offer => offer.Action == WorldInteractionAction.Chop);
            Assert.True(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Chop, actor)).Success);
            Assert.False(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Chop, actor)).Success);
            actor.EndAction(typeof(InteractionResolver));
            actor.X += 5;
            Assert.Empty(WorldInteractionMenu.GetOffers(actor, target));
            Assert.False(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Chop, actor)).Success);
            actor.X -= 5;
            other.AddItem(tool);
            Assert.DoesNotContain(WorldInteractionMenu.GetOffers(actor, target), offer => offer.Action == WorldInteractionAction.Chop);
            Assert.False(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Chop, actor)).Success);
            actor.AddItem(tool);
            tool.Sharpness = 0;
            Assert.False(InteractionResolver.Resolve(Context(tool, target, WorldInteractionAction.Chop, actor)).Success);
        }
        finally { tool.Delete(); target.Delete(); actor.Delete(); other.Delete(); }
    }
}
