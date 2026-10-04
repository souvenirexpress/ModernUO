using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Engines.LivingBritain;
using Server.Engines.WorldSimulation;
using Server.Items;
using Xunit;

namespace UOContent.Tests.Tests.Engines.LivingBritain;

[Collection("Sequential UOContent Tests")]
public class BritainDataTests
{
    private static string FindDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var copied = Path.Combine(directory.FullName, "Data", "Abadoria", "Britain");
            if (File.Exists(Path.Combine(copied, "residents.json")))
            {
                return copied;
            }

            var source = Path.Combine(directory.FullName, "Distribution", "Data", "Abadoria", "Britain");
            if (File.Exists(Path.Combine(source, "residents.json")))
            {
                return source;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Living Britain test data.");
    }

    private static BritainDataSet Load() => BritainData.Load(FindDataRoot());

    private static string CopyDataRoot()
    {
        var destination = Path.Combine(Path.GetTempPath(), "living-britain-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(FindDataRoot(), "*.json"))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        return destination;
    }

    [Fact]
    public void VerticalSliceHasRequiredPopulationAndPlaces()
    {
        var data = Load();

        Assert.True(data.Buildings.Count(x => x.Type.Contains("residence")) >= 10);
        Assert.True(data.Residents.Count >= 15);
        Assert.True(data.Residents.Count(x => x.Guard) >= 4);
        Assert.True(data.Residents.Count(x => x.Quest != null) >= 2);
        Assert.Contains(data.Buildings, x => x.Type.Contains("workshop"));
        Assert.Contains(data.Buildings, x => x.Type.Contains("shop"));
        Assert.Contains(data.Buildings, x => x.Type == "tavern");
    }

    [Fact]
    public void EveryResidentHasACompleteStableProfile()
    {
        var data = Load();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resident in data.Residents)
        {
            Assert.True(ids.Add(resident.Id));
            Assert.False(string.IsNullOrWhiteSpace(resident.FullName));
            Assert.False(string.IsNullOrWhiteSpace(resident.Profession));
            Assert.False(string.IsNullOrWhiteSpace(resident.HouseholdId));
            Assert.False(string.IsNullOrWhiteSpace(resident.FavoriteDish));
            Assert.False(string.IsNullOrWhiteSpace(resident.EverydayDrink));
            Assert.False(string.IsNullOrWhiteSpace(resident.DesiredStyle));
            Assert.False(string.IsNullOrWhiteSpace(resident.Need));
            Assert.False(string.IsNullOrWhiteSpace(resident.Worry));
            Assert.False(string.IsNullOrWhiteSpace(resident.Dream));
            Assert.False(string.IsNullOrWhiteSpace(resident.Secret));
            Assert.True(resident.PositiveTraits.Count >= 3);
            Assert.True(resident.NegativeTraits.Count >= 2);
            Assert.True(resident.Relationships.Count >= 3);
            Assert.NotNull(resident.SpawnFallback);
            Assert.NotNull(data.GetBuilding(resident.HomeId));
            Assert.NotNull(data.GetBuilding(resident.WorkplaceId));
            Assert.NotNull(data.GetSchedule(resident.ScheduleId));
            Assert.NotNull(data.GetDialogue(resident.DialogueId));
            Assert.NotNull(data.GetOutfit(resident.OutfitStyle));

            foreach (var relationship in resident.Relationships)
            {
                Assert.NotNull(data.GetResident(relationship.NpcId));
                Assert.InRange(relationship.Value, -100, 100);
            }
        }
    }

    [Fact]
    public void DialogueProfilesMeetPerPersonMinimums()
    {
        var data = Load();

        foreach (var dialogue in data.Dialogues)
        {
            Assert.True(dialogue.Greetings.Count >= 5);
            Assert.True(dialogue.General.Count >= 5);
            Assert.True(dialogue.Work.Count >= 5);
            Assert.True(dialogue.Relationships.Count >= 3);
            Assert.True(dialogue.Rumors.Count >= 3);
            Assert.True(dialogue.Situational.Count >= 3);
            Assert.True(dialogue.Rejecting.Count >= 2);
            Assert.True(dialogue.Friendly.Count >= 2);
            Assert.False(string.IsNullOrWhiteSpace(dialogue.Request));
            Assert.False(string.IsNullOrWhiteSpace(dialogue.Quest));
        }
    }

    [Fact]
    public void SevenDayScheduleSimulationAlwaysResolvesPlausibleActivities()
    {
        var data = Load();
        var start = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Unspecified);
        var distinctWeekdays = new HashSet<string>();

        foreach (var schedule in data.Schedules)
        {
            var activities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var step = 0; step < 7 * 24 * 4; step++)
            {
                var time = start.AddMinutes(step * 15);
                var entry = schedule.Resolve(time.DayOfWeek, time.Hour * 60 + time.Minute);
                Assert.NotNull(entry);
                Assert.NotNull(entry.Destination);
                Assert.InRange(entry.Destination.X, 1300, 1600);
                Assert.InRange(entry.Destination.Y, 1500, 1800);
                activities.Add(entry.Activity);
            }

            Assert.Contains("sleep", activities);
            Assert.True(activities.Contains("work") || activities.Contains("patrol") || activities.Contains("market"));
            Assert.True(schedule.Restday.Select(x => $"{x.Time}:{x.Activity}").SequenceEqual(
                schedule.Weekday.Select(x => $"{x.Time}:{x.Activity}")) == false);
            distinctWeekdays.Add(string.Join('|', schedule.Weekday.Select(x => $"{x.Time}:{x.Activity}:{x.Destination.X}:{x.Destination.Y}")));
        }

        Assert.True(distinctWeekdays.Count >= 15);
    }

    [Fact]
    public void GuardCoverageIncludesOfficerAndAllThreeShiftTypes()
    {
        var data = Load();
        var guards = data.Residents.Where(x => x.Guard).ToList();

        Assert.Contains(guards, x => x.GuardRank.Contains("Hauptmann"));
        Assert.Contains(guards, x => x.GuardRank.Contains("Frühschicht"));
        Assert.Contains(guards, x => x.GuardRank.Contains("Spätschicht"));
        Assert.Contains(guards, x => x.GuardRank.Contains("Nachtschicht"));
    }

    [Fact]
    public void StaticOverridesAreLoadedAndValidated()
    {
        var root = CopyDataRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "static-overrides.json"),
                """[{"id":"britain_static_wheel","name":"Spinning wheel","map":"Felucca","x":1462,"y":1655,"z":10,"itemId":4117,"hue":0,"operation":"add"}]"""
            );

            var data = BritainData.Load(root);
            var definition = Assert.Single(data.StaticOverrides);

            Assert.Equal("britain_static_wheel", definition.Id);
            Assert.Equal(4117, definition.ItemId);
            Assert.Equal(new Point3D(1462, 1655, 10), definition.ToPoint3D());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void InvalidStaticOverrideOperationIsRejected()
    {
        var root = CopyDataRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "static-overrides.json"),
                """[{"id":"britain_static_bad","map":"Felucca","x":1462,"y":1655,"z":10,"itemId":4117,"operation":"teleport"}]"""
            );

            var exception = Assert.Throws<InvalidDataException>(() => BritainData.Load(root));
            Assert.Contains("invalid static operation", exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(0x675, 0x675, 0x676, DoorFacing.WestCW)]
    [InlineData(0x676, 0x675, 0x676, DoorFacing.WestCW)]
    [InlineData(0x6A9, 0x6A9, 0x6AA, DoorFacing.WestCCW)]
    [InlineData(0x31A, 0x31A, 0x31B, DoorFacing.EastCW)]
    [InlineData(0x2421, 0x2421, 0x2422, DoorFacing.SouthCCW)]
    [InlineData(0xB53, 0, 0, DoorFacing.WestCW)]
    public void StaticSemanticsRecognizesModernUODoors(
        int itemId,
        int expectedClosedId,
        int expectedOpenedId,
        DoorFacing expectedFacing
    )
    {
        var recognized = LivingBritainStaticSemantics.TryGetDoorSpec(itemId, out var spec);

        if (expectedClosedId == 0)
        {
            Assert.False(recognized);
            return;
        }

        Assert.True(recognized);
        Assert.Equal(expectedClosedId, spec.ClosedId);
        Assert.Equal(expectedOpenedId, spec.OpenedId);
        Assert.Equal(expectedFacing, spec.Facing);
    }

    [Theory]
    [InlineData(0xA2A)]
    [InlineData(0xB53)]
    [InlineData(0xB5D)]
    [InlineData(0x1218)]
    [InlineData(0x2DEB)]
    public void StaticSemanticsRecognizesSeats(int itemId) =>
        Assert.True(LivingBritainStaticSemantics.IsSeat(itemId));

    [Theory]
    [InlineData("container", 0x2816, LivingBritainStaticKind.Container)]
    [InlineData("seat", 0xB53, LivingBritainStaticKind.Functional)]
    [InlineData("spinning-wheel", 0x1015, LivingBritainStaticKind.SpinningWheel)]
    [InlineData("light", 0xA05, LivingBritainStaticKind.Functional)]
    [InlineData("lever", 0x108C, LivingBritainStaticKind.Functional)]
    [InlineData("trapdoor", 0x6A5, LivingBritainStaticKind.Functional)]
    [InlineData("", 0x6A5, LivingBritainStaticKind.Door)]
    [InlineData("", 0xB53, LivingBritainStaticKind.Item)]
    public void StaticSemanticsUsesExplicitFunctionalType(string functionType, int itemId, LivingBritainStaticKind expected)
    {
        var definition = new BritainStaticOverride { Id = "test", FunctionType = functionType, ItemId = itemId };

        Assert.Equal(expected, LivingBritainStaticSemantics.Classify(definition));
    }

    [Fact]
    public void StudioMaterialObjectUsesSimulationSemanticsAndPreservesRuntimeState()
    {
        var definition = new BritainStaticOverride
        {
            Id = "studio_oak_log",
            Name = "Eichenstamm",
            ItemId = 0x1BDD,
            Equippable = true,
            AnimationId = 615,
            EquipLayer = (int)Layer.OneHanded,
            Simulation = new BritainWorldSimulationDefinition
            {
                Enabled = true,
                Material = "OakWood",
                State = new BritainWorldSimulationState { Moisture = 0.05, FuelRemaining = 1.0 },
                Capabilities = new BritainWorldSimulationCapabilities { IgnitePower = 0.8, HeatPower = 0.7 },
                Visuals = new BritainWorldSimulationVisuals { DefaultItemId = 0x1BDD, BurningItemId = 0xDE3, CharredItemId = 0xDE9, AshItemId = 0xDEA }
            }
        };

        var item = Assert.IsType<LivingBritainWorldSimulationItem>(LivingBritainStaticSemantics.Create(definition));
        try
        {
            Assert.Equal(LivingBritainStaticKind.WorldSimulation, LivingBritainStaticSemantics.Classify(definition));
            Assert.Equal(MaterialId.OakWood, item.PrimaryMaterial);
            Assert.Equal(0.8, item.Capabilities.IgnitePower);
            Assert.Equal(0.05, item.State.Moisture);
            Assert.Equal(0xDE3, item.BurningItemID);
            Assert.True(item.Movable);
            Assert.Equal(Layer.OneHanded, item.Layer);

            item.State.Moisture = 0.65;
            item.OnSimulationStateChanged();
            item.Apply(definition);
            Assert.Equal(0.65, item.State.Moisture);

            item.Apply(definition with
            {
                Simulation = definition.Simulation with { State = definition.Simulation.State with { Moisture = 0.2 } }
            });
            Assert.Equal(0.2, item.State.Moisture);

            item.Apply(definition with { Equippable = false, Movable = false });
            Assert.False(item.Movable);
            Assert.Equal(Layer.Invalid, item.Layer);
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void StudioMaterialObjectSerializationPreservesDefinitionAndDynamicState()
    {
        var definition = new BritainStaticOverride
        {
            Id = "studio_fire_bowl",
            Name = "Feuerschale",
            ItemId = 0x19AA,
            Simulation = new BritainWorldSimulationDefinition
            {
                Enabled = true,
                Material = "Iron",
                State = new BritainWorldSimulationState { Temperature = 450, FuelRemaining = 0.8, CombustionIntensity = 0.7 },
                Capabilities = new BritainWorldSimulationCapabilities { HeatPower = 0.9, IgnitePower = 0.8, LightPower = 1.0 },
                Visuals = new BritainWorldSimulationVisuals { DefaultItemId = 0x19AA, BurningItemId = 0x19AB, CharredItemId = 0x19AA, AshItemId = 0x19AA }
            }
        };
        var original = new LivingBritainWorldSimulationItem(definition);
        var loaded = new LivingBritainWorldSimulationItem();
        var path = Path.Combine(Path.GetTempPath(), $"living-britain-simulation-{Guid.NewGuid():N}.bin");

        try
        {
            original.State.Soot = 0.42;
            original.OnSimulationStateChanged();
            var writer = new BufferWriter(true);
            original.Serialize(writer);
            File.WriteAllBytes(path, writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());

            using var reader = new BinaryFileReader(path);
            loaded.Deserialize(reader);

            Assert.Equal("studio_fire_bowl", loaded.OverrideId);
            Assert.Equal(MaterialId.Iron, loaded.PrimaryMaterial);
            Assert.Equal(0.8, loaded.Capabilities.IgnitePower);
            Assert.Equal(0x19AB, loaded.BurningItemID);
            Assert.Equal(0.42, loaded.State.Soot);
            Assert.Equal(0.7, loaded.State.CombustionIntensity);
        }
        finally
        {
            original.Delete();
            loaded.Delete();
            File.Delete(path);
        }
    }
}
