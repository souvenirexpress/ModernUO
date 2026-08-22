using System;
using System.Collections.Generic;
using System.IO;
using Server.Engines.WorldSimulation;
using Server.Items;
using Xunit;

namespace Server.Tests;

[Collection("Sequential UOContent Tests")]
public class WorldSimulationTests
{
    private sealed class TestSource : IWorldInteractionSource
    {
        public TestSource(ToolCapabilities capabilities) => Capabilities = capabilities;

        public ToolCapabilities Capabilities { get; }
    }

    private sealed class TestTarget : IWorldSimulatedObject
    {
        private static readonly MaterialFraction[] _composition = [new(MaterialId.OakWood, 1.0)];

        public TestTarget(double moisture = 0.05)
        {
            Item = new Item(0x1BDD);
            State = new WorldObjectState { Moisture = moisture };
        }

        public Item Item { get; }
        public MaterialId PrimaryMaterial => MaterialId.OakWood;
        public IReadOnlyList<MaterialFraction> Composition => _composition;
        public PhysicalProperties PhysicalProperties => new(2.0, 0.003);
        public IWorldObjectState State { get; }
        public void OnSimulationStateChanged() { }
    }

    public WorldSimulationTests()
    {
        MaterialRegistry.ResetForTesting(
            new MaterialDefinition
            {
                Id = MaterialId.OakWood,
                Name = "Oak Wood",
                HeatCapacity = 1.7,
                ThermalConductivity = 0.12,
                Flammability = 0.82,
                IgnitionTemperature = 280.0,
                BurnRate = 0.012,
                HeatOutput = 480.0,
                CutResistance = 0.42,
                WaterAbsorption = 0.62
            },
            new MaterialDefinition
            {
                Id = MaterialId.Water,
                Name = "Water",
                HeatCapacity = 4.18,
                ThermalConductivity = 0.58,
                FreezingPoint = 0.0,
                BoilingPoint = 100.0
            }
        );
        WorldSimulationSystem.ResetForTesting();
    }

    [Fact]
    public void DryWoodWithSufficientIgnitionStartsBurning()
    {
        var target = new TestTarget();
        var result = Ignite(target, new ToolCapabilities(HeatPower: 1.0, IgnitePower: 1.0));

        Assert.True(result.Success);
        Assert.True(target.State.IsBurning);
    }

    [Fact]
    public void SoakedWoodWithWeakFlameDoesNotIgnite()
    {
        var target = new TestTarget(0.85);
        var result = Ignite(target, new ToolCapabilities(HeatPower: 0.2, IgnitePower: 0.25));

        Assert.False(result.Success);
        Assert.False(target.State.IsBurning);
        Assert.Contains("moisture", result.Messages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BurningWoodAndWaterStopsCombustion()
    {
        var target = BurningTarget();
        var result = Extinguish(target);

        Assert.True(result.Success);
        Assert.False(target.State.IsBurning);
    }

    [Fact]
    public void BurningWoodAndWaterIncreasesMoisture()
    {
        var target = BurningTarget();
        var before = target.State.Moisture;

        Extinguish(target);

        Assert.True(target.State.Moisture > before);
    }

    [Fact]
    public void BurningWoodConsumesFuelOverTime()
    {
        var target = BurningTarget();
        var before = target.State.FuelRemaining;

        WorldSimulationSystem.Advance(target, TimeSpan.FromSeconds(10), new EnvironmentContext());

        Assert.True(target.State.FuelRemaining < before);
    }

    [Fact]
    public void BurningWoodIncreasesCharOverTime()
    {
        var target = BurningTarget();
        var before = target.State.CharLevel;

        WorldSimulationSystem.Advance(target, TimeSpan.FromSeconds(10), new EnvironmentContext());

        Assert.True(target.State.CharLevel > before);
    }

    [Fact]
    public void WoodWithNoFuelCannotContinueBurning()
    {
        var target = BurningTarget();
        target.State.FuelRemaining = 0.0;

        WorldSimulationSystem.Advance(target, TimeSpan.FromSeconds(1), new EnvironmentContext());

        Assert.False(target.State.IsBurning);
    }

    [Fact]
    public void MaterialDefinitionsAreLoadedAndShared()
    {
        var path = Path.Combine(Core.BaseDirectory, "Data", "world-simulation", "materials.json");
        MaterialRegistry.Load(path);

        foreach (var id in Enum.GetValues<MaterialId>())
        {
            Assert.Equal(id, MaterialRegistry.Get(id).Id);
        }

        Assert.Same(MaterialRegistry.Get(MaterialId.OakWood), MaterialRegistry.Get(MaterialId.OakWood));
    }

    [Fact]
    public void SaveAndLoadPreservesDynamicState()
    {
        var original = new DryOakLog
        {
            Temperature = 321.5,
            Moisture = 0.42,
            Condition = 0.81,
            CombustionIntensity = 0.65,
            FuelRemaining = 0.44,
            CharLevel = 0.37,
            Dirt = 0.12,
            Soot = 0.25
        };
        var loaded = new DryOakLog();
        var path = Path.Combine(Path.GetTempPath(), $"world-simulation-{Guid.NewGuid():N}.bin");

        try
        {
            var writer = new BufferWriter(true);
            original.Serialize(writer);
            File.WriteAllBytes(path, writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());

            using var reader = new BinaryFileReader(path);
            loaded.Deserialize(reader);

            Assert.Equal(original.Temperature, loaded.Temperature);
            Assert.Equal(original.Moisture, loaded.Moisture);
            Assert.Equal(original.Condition, loaded.Condition);
            Assert.Equal(original.CombustionIntensity, loaded.CombustionIntensity);
            Assert.Equal(original.FuelRemaining, loaded.FuelRemaining);
            Assert.Equal(original.CharLevel, loaded.CharLevel);
            Assert.Equal(original.Dirt, loaded.Dirt);
            Assert.Equal(original.Soot, loaded.Soot);
        }
        finally
        {
            original.Delete();
            loaded.Delete();
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyItemWithoutSimulationContinuesWithoutException()
    {
        var legacy = new Item(0x1);

        try
        {
            var actions = AffordanceResolver.GetAvailableActions(null, null, legacy, new EnvironmentContext());
            var result = InteractionResolver.Resolve(
                new InteractionContext
                {
                    Source = null,
                    Target = legacy,
                    Action = WorldInteractionAction.Ignite
                }
            );

            Assert.Contains(WorldInteractionAction.Inspect, actions);
            Assert.False(result.Success);
        }
        finally
        {
            legacy.Delete();
        }
    }

    private static TestTarget BurningTarget()
    {
        var target = new TestTarget();
        var result = Ignite(target, new ToolCapabilities(HeatPower: 1.0, IgnitePower: 1.0));
        Assert.True(result.Success);
        return target;
    }

    private static InteractionResult Ignite(TestTarget target, ToolCapabilities capabilities) =>
        InteractionResolver.Resolve(
            new InteractionContext
            {
                Source = new TestSource(capabilities),
                Target = target,
                Action = WorldInteractionAction.Ignite,
                Environment = new EnvironmentContext()
            }
        );

    private static InteractionResult Extinguish(TestTarget target) =>
        InteractionResolver.Resolve(
            new InteractionContext
            {
                Source = new TestSource(new ToolCapabilities(CoolPower: 1.0, ExtinguishPower: 1.0)),
                Target = target,
                Action = WorldInteractionAction.Extinguish,
                Environment = new EnvironmentContext()
            }
        );
}
