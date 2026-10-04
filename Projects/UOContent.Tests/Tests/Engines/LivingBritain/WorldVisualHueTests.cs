using System;
using System.IO;
using Server;
using Server.Engines.LivingBritain;
using Xunit;

namespace UOContent.Tests.Tests.Engines.LivingBritain;

[Collection("Sequential UOContent Tests")]
public class WorldVisualHueTests
{
    private static BritainStaticOverride Definition => new()
    {
        Id = "hue_test", Name = "Tree", ItemId = 0x1BDD, Hue = 34,
        Simulation = new BritainWorldSimulationDefinition
        {
            Enabled = true, Material = "OakWood",
            State = new BritainWorldSimulationState { FuelRemaining = 1.0 },
            Visuals = new BritainWorldSimulationVisuals
            {
                DefaultItemId = 0x1BDD, BurningItemId = 0x1BDD, CharredItemId = 0x1BDD, AshItemId = 0x1BDD,
                BurningHue = 33, CharredHue = 1, AshHue = 0
            }
        }
    };

    [Fact]
    public void StateChangesApplyIndependentHuesAndRestoreInheritedHue()
    {
        var item = new LivingBritainWorldSimulationItem(Definition);
        try
        {
            Assert.Equal(34, item.Hue);
            item.State.CharLevel = 0.8;
            item.OnSimulationStateChanged();
            Assert.Equal(1, item.Hue);
            item.State.CombustionIntensity = 1;
            item.OnSimulationStateChanged();
            Assert.Equal(33, item.Hue);
            item.State.FuelRemaining = 0;
            item.OnSimulationStateChanged();
            Assert.Equal(0, item.Hue);
            item.State.CombustionIntensity = 0;
            item.State.FuelRemaining = 1;
            item.State.CharLevel = 0;
            item.OnSimulationStateChanged();
            Assert.Equal(34, item.Hue);
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void SaveAndLoadPreserveAllHueStates()
    {
        var item = new LivingBritainWorldSimulationItem(Definition);
        var loaded = new LivingBritainWorldSimulationItem();
        var path = Path.Combine(Path.GetTempPath(), $"world-hues-{Guid.NewGuid():N}.bin");
        try
        {
            item.State.CharLevel = 0.8;
            item.OnSimulationStateChanged();
            var writer = new BufferWriter(true);
            item.Serialize(writer);
            File.WriteAllBytes(path, writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
            using var reader = new BinaryFileReader(path);
            loaded.Deserialize(reader);
            Assert.Equal(34, loaded.DefaultHue);
            Assert.Equal(33, loaded.BurningHue);
            Assert.Equal(1, loaded.CharredHue);
            Assert.Equal(0, loaded.AshHue);
            loaded.OnSimulationStateChanged();
            Assert.Equal(1, loaded.Hue);
        }
        finally
        {
            item.Delete();
            loaded.Delete();
            File.Delete(path);
        }
    }
}
