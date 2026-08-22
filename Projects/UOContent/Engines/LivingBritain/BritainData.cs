using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Json;
using Server.Logging;

namespace Server.Engines.LivingBritain;

public sealed record BritainPoint
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }

    public Point3D ToPoint3D() => new(X, Y, Z);
}

public sealed record BritainBuilding
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Type { get; init; }
    public BritainPoint Center { get; init; }
    public int Floor { get; init; }
    public int Rooms { get; init; }
    public int Beds { get; init; }
    public int Workplaces { get; init; }
    public int Seats { get; init; }
    public int Containers { get; init; }
    public int Capacity { get; init; }
    public bool Public { get; init; }
    public string SuggestedProfession { get; init; }
    public string Notes { get; init; }
}

public sealed record BritainRelationship
{
    public string NpcId { get; init; }
    public string Type { get; init; }
    public int Value { get; init; }
    public string Background { get; init; }
    public string State { get; init; }
    public string Topic { get; init; }
}

public sealed record BritainQuestProfile
{
    public string Id { get; init; }
    public string ItemType { get; init; }
    public int Amount { get; init; }
    public int RewardGold { get; init; }
    public string Offer { get; init; }
    public string Reminder { get; init; }
    public string Completion { get; init; }
}

public sealed record BritainResidentProfile
{
    public string Id { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string Gender { get; init; }
    public int Age { get; init; }
    public string HomeId { get; init; }
    public string WorkplaceId { get; init; }
    public string Profession { get; init; }
    public string SocialClass { get; init; }
    public string Origin { get; init; }
    public string MaritalStatus { get; init; }
    public string HouseholdId { get; init; }
    public string BodyBuild { get; init; }
    public string HairColor { get; init; }
    public string HairStyle { get; init; }
    public string Beard { get; init; }
    public int Body { get; init; } = 0x190;
    public int SkinHue { get; init; }
    public int HairHue { get; init; }
    public int HairItemId { get; init; }
    public int BeardItemId { get; init; }
    public string TypicalClothing { get; init; }
    public List<string> PreferredColors { get; init; } = [];
    public string WorkClothing { get; init; }
    public string LeisureClothing { get; init; }
    public string SpecialFeatures { get; init; }
    public string Jewelry { get; init; }
    public List<string> PositiveTraits { get; init; } = [];
    public List<string> NegativeTraits { get; init; } = [];
    public string Need { get; init; }
    public string Worry { get; init; }
    public string Weakness { get; init; }
    public string Belief { get; init; }
    public string Dislike { get; init; }
    public string Dream { get; init; }
    public string Secret { get; init; }
    public string FavoriteDish { get; init; }
    public string SimpleMeal { get; init; }
    public string DislikedFood { get; init; }
    public string HolidayDish { get; init; }
    public string EverydayDrink { get; init; }
    public string AlcoholicDrink { get; init; }
    public string DislikedDrink { get; init; }
    public string ClothingMaterial { get; init; }
    public string DesiredStyle { get; init; }
    public string RejectedStyle { get; init; }
    public string SpecialOccasionClothing { get; init; }
    public bool Guard { get; init; }
    public string GuardRank { get; init; }
    public string GuardDistrict { get; init; }
    public string ScheduleId { get; init; }
    public string DialogueId { get; init; }
    public string OutfitStyle { get; init; }
    public string MemoryProfile { get; init; }
    public BritainPoint SpawnFallback { get; init; }
    public List<BritainRelationship> Relationships { get; init; } = [];
    public BritainQuestProfile Quest { get; init; }

    public string FullName => $"{FirstName} {LastName}";
}

public sealed record BritainScheduleEntry
{
    public string Time { get; init; }
    public string Activity { get; init; }
    public string Detail { get; init; }
    public BritainPoint Destination { get; init; }
    public string Outfit { get; init; }
    public bool Sitting { get; init; }

    public int MinuteOfDay
    {
        get
        {
            if (!TimeSpan.TryParse(Time, out var parsed))
            {
                throw new FormatException($"Invalid Britain schedule time '{Time}'.");
            }

            return parsed.Hours * 60 + parsed.Minutes;
        }
    }
}

public sealed record BritainSchedule
{
    public string Id { get; init; }
    public List<BritainScheduleEntry> Weekday { get; init; } = [];
    public List<BritainScheduleEntry> Restday { get; init; } = [];

    public BritainScheduleEntry Resolve(DayOfWeek day, int minuteOfDay)
    {
        var entries = day == DayOfWeek.Sunday ? Restday : Weekday;
        if (entries.Count == 0)
        {
            return null;
        }

        BritainScheduleEntry result = null;
        foreach (var entry in entries.OrderBy(e => e.MinuteOfDay))
        {
            if (entry.MinuteOfDay > minuteOfDay)
            {
                break;
            }

            result = entry;
        }

        return result ?? entries.MaxBy(e => e.MinuteOfDay);
    }
}

public sealed record BritainDialogueProfile
{
    public string Id { get; init; }
    public List<string> Greetings { get; init; } = [];
    public List<string> General { get; init; } = [];
    public List<string> Work { get; init; } = [];
    public List<string> Relationships { get; init; } = [];
    public List<string> Rumors { get; init; } = [];
    public List<string> Situational { get; init; } = [];
    public List<string> Rejecting { get; init; } = [];
    public List<string> Friendly { get; init; } = [];
    public string Request { get; init; }
    public string Quest { get; init; }
}

public sealed record BritainOutfitPiece
{
    public int ItemId { get; init; }
    public Layer Layer { get; init; }
    public int Hue { get; init; }
    public string Name { get; init; }
}

public sealed record BritainOutfitStyle
{
    public string Id { get; init; }
    public Dictionary<string, List<BritainOutfitPiece>> Sets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record BritainInteriorResident
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Profession { get; init; }
}

public sealed record BritainInteriorPoint
{
    public string Id { get; init; }
    public string Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public bool Stationary { get; init; }
    public string OwnerNpcId { get; init; }
    public Point3D ToPoint3D() => new(X, Y, Z);
}

public sealed record BritainInteriorObject
{
    public string StableId { get; init; }
    public string TemplateId { get; init; }
    public string Name { get; init; }
    public string Category { get; init; }
    public int ItemId { get; init; }
    public int Hue { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public string Purpose { get; init; }
    public string OwnerNpcId { get; init; }
    public bool Decorative { get; init; }
    public bool Interactive { get; init; }
    public bool Movable { get; init; }
    public bool Locked { get; init; }
    public string Access { get; init; }
    public string TheftStatus { get; init; }
    public bool Container { get; init; }
    public List<string> ContainerContents { get; init; } = [];
    public string RespawnBehavior { get; init; }
    public string QuestRelation { get; init; }
    public string NpcUse { get; init; }
    public string PlayerUse { get; init; }
    public string Reason { get; init; }
    public bool Unique { get; init; }
    public bool Light { get; init; }
    public bool Personal { get; init; }
    public Point3D ToPoint3D() => new(X, Y, Z);
}

public sealed record BritainInteriorRoom
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Function { get; init; }
    public string Zone { get; init; }
    public BritainPoint Entry { get; init; }
    public List<BritainInteriorPoint> InteractionPoints { get; init; } = [];
    public List<BritainInteriorObject> Objects { get; init; } = [];
}

public sealed record BritainInteriorVariant
{
    public string Id { get; init; }
    public string Name { get; init; }
    public int Density { get; init; }
    public List<BritainInteriorRoom> Rooms { get; init; } = [];
}

public sealed record BritainInteriorPlan
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string BuildingId { get; init; }
    public string HouseholdId { get; init; }
    public string Archetype { get; init; }
    public string Status { get; init; }
    public bool ManualApprovalRequired { get; init; }
    public bool LivePublicationAllowed { get; init; }
    public string SelectedVariant { get; init; }
    public List<BritainInteriorResident> Residents { get; init; } = [];
    public List<BritainInteriorVariant> Variants { get; init; } = [];

    public BritainInteriorVariant ActiveVariant =>
        Variants.FirstOrDefault(x => x.Id.Equals(SelectedVariant, StringComparison.OrdinalIgnoreCase)) ?? Variants.FirstOrDefault();
}

public sealed record BritainStaticOverride
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Map { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public int ItemId { get; init; }
    public int Hue { get; init; }
    public string Operation { get; init; }
    public string SourceKey { get; init; }
    public string DoorGroupId { get; init; }
    public string DoorRole { get; init; }
    public string FunctionType { get; init; }
    public bool Locked { get; init; }
    public bool Movable { get; init; }
    public BritainWorldSimulationDefinition Simulation { get; init; }
    public Point3D ToPoint3D() => new(X, Y, Z);
}

public sealed record BritainWorldSimulationDefinition
{
    public bool Enabled { get; init; }
    public string Material { get; init; }
    public BritainWorldSimulationState State { get; init; } = new();
    public BritainWorldSimulationCapabilities Capabilities { get; init; } = new();
    public BritainWorldSimulationVisuals Visuals { get; init; } = new();
}

public sealed record BritainWorldSimulationState
{
    public double Condition { get; init; } = 1.0;
    public double Temperature { get; init; } = 20.0;
    public double Moisture { get; init; }
    public double Sharpness { get; init; }
    public double Deformation { get; init; }
    public double Cracking { get; init; }
    public double CombustionIntensity { get; init; }
    public double FuelRemaining { get; init; } = 1.0;
    public double CharLevel { get; init; }
    public double Dirt { get; init; }
    public double Blood { get; init; }
    public double Oil { get; init; }
    public double Poison { get; init; }
    public double Soot { get; init; }
    public double Freshness { get; init; } = 1.0;
    public double Decay { get; init; }
    public double Fermentation { get; init; }
}

public sealed record BritainWorldSimulationCapabilities
{
    public double CutPower { get; init; }
    public double ChopPower { get; init; }
    public double PiercePower { get; init; }
    public double StrikePower { get; init; }
    public double CrushPower { get; init; }
    public double PryPower { get; init; }
    public double HeatPower { get; init; }
    public double IgnitePower { get; init; }
    public double CoolPower { get; init; }
    public double ExtinguishPower { get; init; }
    public double LightPower { get; init; }
}

public sealed record BritainWorldSimulationVisuals
{
    public int DefaultItemId { get; init; }
    public int BurningItemId { get; init; }
    public int CharredItemId { get; init; }
    public int AshItemId { get; init; }
}

public sealed record BritainTerrainOverride
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Map { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public int LandId { get; init; }
}

public sealed class BritainDataSet
{
    private readonly Dictionary<string, BritainBuilding> _buildings;
    private readonly Dictionary<string, BritainResidentProfile> _residents;
    private readonly Dictionary<string, BritainSchedule> _schedules;
    private readonly Dictionary<string, BritainDialogueProfile> _dialogues;
    private readonly Dictionary<string, BritainOutfitStyle> _outfits;
    private readonly Dictionary<string, BritainInteriorPlan> _interiors;
    private readonly Dictionary<string, BritainStaticOverride> _staticOverrides;
    private readonly Dictionary<string, BritainTerrainOverride> _terrainOverrides;

    public BritainDataSet(
        IEnumerable<BritainBuilding> buildings,
        IEnumerable<BritainResidentProfile> residents,
        IEnumerable<BritainSchedule> schedules,
        IEnumerable<BritainDialogueProfile> dialogues,
        IEnumerable<BritainOutfitStyle> outfits,
        IEnumerable<BritainInteriorPlan> interiors,
        IEnumerable<BritainStaticOverride> staticOverrides,
        IEnumerable<BritainTerrainOverride> terrainOverrides
    )
    {
        _buildings = buildings.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _residents = residents.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _schedules = schedules.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _dialogues = dialogues.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _outfits = outfits.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _interiors = interiors.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _staticOverrides = staticOverrides.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _terrainOverrides = terrainOverrides.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<BritainBuilding> Buildings => _buildings.Values;
    public IReadOnlyCollection<BritainResidentProfile> Residents => _residents.Values;
    public IReadOnlyCollection<BritainSchedule> Schedules => _schedules.Values;
    public IReadOnlyCollection<BritainDialogueProfile> Dialogues => _dialogues.Values;
    public IReadOnlyCollection<BritainInteriorPlan> Interiors => _interiors.Values;
    public IReadOnlyCollection<BritainStaticOverride> StaticOverrides => _staticOverrides.Values;
    public IReadOnlyCollection<BritainTerrainOverride> TerrainOverrides => _terrainOverrides.Values;

    public BritainResidentProfile GetResident(string id) => id != null && _residents.TryGetValue(id, out var value) ? value : null;
    public BritainSchedule GetSchedule(string id) => id != null && _schedules.TryGetValue(id, out var value) ? value : null;
    public BritainDialogueProfile GetDialogue(string id) => id != null && _dialogues.TryGetValue(id, out var value) ? value : null;
    public BritainOutfitStyle GetOutfit(string id) => id != null && _outfits.TryGetValue(id, out var value) ? value : null;
    public BritainBuilding GetBuilding(string id) => id != null && _buildings.TryGetValue(id, out var value) ? value : null;

    public BritainInteriorPoint ResolveInteriorDestination(BritainResidentProfile resident, BritainScheduleEntry entry)
    {
        if (resident == null || entry?.Destination == null)
        {
            return null;
        }

        var requestedType = entry.Activity.ToLowerInvariant() switch
        {
            "sleep" => "sleep",
            "breakfast" or "meal" or "home" => "meal",
            "work" => "work",
            "leisure" or "visit" => "conversation",
            _ => entry.Activity.ToLowerInvariant()
        };
        var original = entry.Destination.ToPoint3D();
        return _interiors.Values
            .Where(plan => plan.Residents.Any(x => x.Id.Equals(resident.Id, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(plan => plan.ActiveVariant?.Rooms ?? [])
            .SelectMany(room => room.InteractionPoints)
            .Where(point => point.Type.Equals(requestedType, StringComparison.OrdinalIgnoreCase) ||
                            requestedType == "work" && point.Type is "trade" or "write" or "cook" ||
                            requestedType == "conversation" && point.Type is "guest" or "meal")
            .OrderBy(point => Math.Abs(point.X - original.X) + Math.Abs(point.Y - original.Y) + Math.Abs(point.Z - original.Z))
            .FirstOrDefault();
    }
}

public static class BritainData
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(BritainData));
    private static readonly JsonSerializerOptions Options = new(JsonConfig.DefaultOptions)
    {
        PropertyNameCaseInsensitive = true
    };

    public static BritainDataSet Current { get; private set; }

    public static BritainDataSet Load(string root = null)
    {
        root ??= Path.Combine(Core.BaseDirectory, "Data", "Abadoria", "Britain");

        var buildings = Read<List<BritainBuilding>>(root, "buildings.json") ?? [];
        var residents = Read<List<BritainResidentProfile>>(root, "residents.json") ?? [];
        var schedules = Read<List<BritainSchedule>>(root, "schedules.json") ?? [];
        var dialogues = Read<List<BritainDialogueProfile>>(root, "dialogues.json") ?? [];
        var outfits = Read<List<BritainOutfitStyle>>(root, "outfits.json") ?? [];
        var interiorsPath = Path.Combine(root, "interiors.json");
        var interiors = File.Exists(interiorsPath) ? Read<List<BritainInteriorPlan>>(root, "interiors.json") ?? [] : [];
        var staticOverridesPath = Path.Combine(root, "static-overrides.json");
        var staticOverrides = File.Exists(staticOverridesPath)
            ? Read<List<BritainStaticOverride>>(root, "static-overrides.json") ?? []
            : [];
        var terrainOverridesPath = Path.Combine(root, "terrain-overrides.json");
        var terrainOverrides = File.Exists(terrainOverridesPath)
            ? Read<List<BritainTerrainOverride>>(root, "terrain-overrides.json") ?? []
            : [];

        Current = new BritainDataSet(buildings, residents, schedules, dialogues, outfits, interiors, staticOverrides, terrainOverrides);
        Validate(Current);
        Logger.Information(
            "Living Britain data loaded: {BuildingCount} buildings, {ResidentCount} residents, {ScheduleCount} schedules.",
            Current.Buildings.Count,
            Current.Residents.Count,
            Current.Schedules.Count
        );
        Logger.Information("Living Britain interiors loaded: {InteriorCount} plans.", Current.Interiors.Count);
        Logger.Information("Living Britain static overrides loaded: {StaticOverrideCount} definitions.", Current.StaticOverrides.Count);
        Logger.Information("Living Britain terrain overrides loaded: {TerrainOverrideCount} definitions.", Current.TerrainOverrides.Count);
        return Current;
    }

    public static List<string> Validate(BritainDataSet data)
    {
        var errors = new List<string>();

        foreach (var resident in data.Residents)
        {
            if (string.IsNullOrWhiteSpace(resident.Id) || string.IsNullOrWhiteSpace(resident.FullName))
            {
                errors.Add("A resident has no stable identity.");
            }

            if (data.GetBuilding(resident.HomeId) == null)
            {
                errors.Add($"{resident.Id}: unknown home '{resident.HomeId}'.");
            }

            if (data.GetBuilding(resident.WorkplaceId) == null)
            {
                errors.Add($"{resident.Id}: unknown workplace '{resident.WorkplaceId}'.");
            }

            if (data.GetSchedule(resident.ScheduleId) == null)
            {
                errors.Add($"{resident.Id}: unknown schedule '{resident.ScheduleId}'.");
            }

            if (data.GetDialogue(resident.DialogueId) == null)
            {
                errors.Add($"{resident.Id}: unknown dialogue '{resident.DialogueId}'.");
            }

            if (data.GetOutfit(resident.OutfitStyle) == null)
            {
                errors.Add($"{resident.Id}: unknown outfit style '{resident.OutfitStyle}'.");
            }

            if (resident.Relationships.Count < 3)
            {
                errors.Add($"{resident.Id}: fewer than three relationships.");
            }

            if (resident.PositiveTraits.Count < 3 || resident.NegativeTraits.Count < 2)
            {
                errors.Add($"{resident.Id}: incomplete personality.");
            }

            foreach (var relationship in resident.Relationships)
            {
                if (data.GetResident(relationship.NpcId) == null)
                {
                    errors.Add($"{resident.Id}: unknown relationship target '{relationship.NpcId}'.");
                }

                if (relationship.Value is < -100 or > 100 || string.IsNullOrWhiteSpace(relationship.Topic))
                {
                    errors.Add($"{resident.Id}: invalid relationship with '{relationship.NpcId}'.");
                }
            }
        }

        foreach (var schedule in data.Schedules)
        {
            if (schedule.Weekday.Count < 6 || schedule.Restday.Count < 4)
            {
                errors.Add($"{schedule.Id}: incomplete weekday/rest-day coverage.");
            }

            foreach (var entry in schedule.Weekday.Concat(schedule.Restday))
            {
                _ = entry.MinuteOfDay;
                if (entry.Destination == null)
                {
                    errors.Add($"{schedule.Id}: '{entry.Time}' has no destination.");
                }
            }
        }

        foreach (var dialogue in data.Dialogues)
        {
            if (dialogue.Greetings.Count < 5 || dialogue.General.Count < 5 || dialogue.Work.Count < 5 ||
                dialogue.Relationships.Count < 3 || dialogue.Rumors.Count < 3 || dialogue.Situational.Count < 3 ||
                dialogue.Rejecting.Count < 2 || dialogue.Friendly.Count < 2 || string.IsNullOrWhiteSpace(dialogue.Request))
            {
                errors.Add($"{dialogue.Id}: dialogue minimum is not met.");
            }
        }

        foreach (var plan in data.Interiors)
        {
            if (data.GetBuilding(plan.BuildingId) == null)
            {
                errors.Add($"{plan.Id}: unknown interior building '{plan.BuildingId}'.");
            }

            if (!plan.ManualApprovalRequired || plan.LivePublicationAllowed)
            {
                errors.Add($"{plan.Id}: generated interior may not bypass manual approval.");
            }

            if (plan.Variants.Count != 3 || plan.ActiveVariant == null)
            {
                errors.Add($"{plan.Id}: three variants and a selected variant are required.");
                continue;
            }

            foreach (var resident in plan.Residents)
            {
                if (data.GetResident(resident.Id) == null)
                {
                    errors.Add($"{plan.Id}: unknown resident '{resident.Id}'.");
                }
            }

            var objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in plan.ActiveVariant.Rooms)
            {
                if (room.InteractionPoints.Count == 0)
                {
                    errors.Add($"{plan.Id}/{room.Id}: no interaction point.");
                }

                foreach (var definition in room.Objects)
                {
                    if (definition.ItemId <= 0 || string.IsNullOrWhiteSpace(definition.StableId) || !objectIds.Add(definition.StableId))
                    {
                        errors.Add($"{plan.Id}/{room.Id}: invalid or duplicate interior object '{definition.StableId}'.");
                    }
                }
            }
        }

        foreach (var definition in data.StaticOverrides)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                errors.Add("A static override has no stable identity.");
            }

            if (definition.Operation is not ("add" or "replace" or "remove"))
            {
                errors.Add($"{definition.Id}: invalid static operation '{definition.Operation}'.");
            }

            if (definition.Operation != "remove" && definition.ItemId <= 0)
            {
                errors.Add($"{definition.Id}: static override requires a positive item ID.");
            }

            if (!string.IsNullOrWhiteSpace(definition.Map) && !definition.Map.Equals("Felucca", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{definition.Id}: unsupported static map '{definition.Map}'.");
            }

            if (definition.X is < 0 or >= 6144 || definition.Y is < 0 or >= 4096 || definition.Z is < -128 or > 127)
            {
                errors.Add($"{definition.Id}: invalid static coordinates ({definition.X}, {definition.Y}, {definition.Z}).");
            }
        }

        var terrainCoordinates = new HashSet<(int X, int Y)>();
        foreach (var definition in data.TerrainOverrides)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                errors.Add("A terrain override has no stable identity.");
            }

            if (!string.IsNullOrWhiteSpace(definition.Map) && !definition.Map.Equals("Felucca", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{definition.Id}: unsupported terrain map '{definition.Map}'.");
            }

            if (definition.X is < 0 or >= 6144 || definition.Y is < 0 or >= 4096 || definition.Z is < -128 or > 127 || definition.LandId is < 0 or > 0x3fff)
            {
                errors.Add($"{definition.Id}: invalid terrain tile ({definition.X}, {definition.Y}, {definition.Z}, {definition.LandId}).");
            }
            else if (!terrainCoordinates.Add((definition.X, definition.Y)))
            {
                errors.Add($"{definition.Id}: duplicate terrain coordinate ({definition.X}, {definition.Y}).");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException("Living Britain data is invalid:\n" + string.Join("\n", errors));
        }

        return errors;
    }

    private static T Read<T>(string root, string fileName) =>
        JsonConfig.Deserialize<T>(Path.Combine(root, fileName), Options);
}
