using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.LivingBritain;

[SerializationGenerator(0, false)]
public partial class LivingBritainResident : BaseCreature
{
    private static readonly Layer[] OutfitLayers =
    {
        Layer.OneHanded, Layer.TwoHanded, Layer.Shoes, Layer.Pants, Layer.Shirt, Layer.Helm, Layer.Gloves,
        Layer.Waist, Layer.InnerTorso, Layer.MiddleTorso, Layer.OuterTorso, Layer.OuterLegs, Layer.Cloak
    };

    [SerializableField(0)]
    private string _profileId;

    [SerializableField(1)]
    private Dictionary<Mobile, int> _reputation;

    [SerializableField(2)]
    private Dictionary<Mobile, int> _visits;

    [SerializableField(3)]
    private Dictionary<Mobile, int> _questStates;

    [SerializableField(4)]
    private string _currentActivity;

    [SerializableField(5)]
    private string _currentOutfit;

    private BritainResidentProfile _profile;
    private BritainScheduleEntry _scheduleEntry;
    private PathFollower _path;
    private Point3D _activityDestination;
    private bool _arrived;
    private DateTime _blockedSince;
    private DateTime _nextAmbientMove;
    private DateTime _nextSleepAnimation;
    private readonly Dictionary<Mobile, DateTime> _speechCooldowns = [];
    private readonly Dictionary<Mobile, int> _intrusionWarnings = [];

    public BritainResidentProfile Profile => _profile;

    [Constructible]
    public LivingBritainResident() : this("britain_baker_mara_finch")
    {
    }

    public LivingBritainResident(string profileId) : base(AIType.AI_Animal, FightMode.None, 12, 1)
    {
        _profileId = profileId;
        _reputation = [];
        _visits = [];
        _questStates = [];
        _currentActivity = "spawn";
        _currentOutfit = "";
        SpeechHue = 0x3B2;
        InitStats(75, 75, 75);
        Karma = 2000;
    }

    public override bool ClickTitle => true;
    public override bool CanOpenDoors => true;
    public override bool HandlesOnSpeech(Mobile from) => from.Alive && InRange(from, 10) || base.HandlesOnSpeech(from);

    public void BindProfile(BritainResidentProfile profile)
    {
        _profile = profile;
        _profileId = profile.Id;
        Name = profile.FullName;
        Title = profile.Guard ? profile.GuardRank : profile.Profession;
        Body = profile.Body;
        Hue = profile.SkinHue;
        Female = profile.Gender.Equals("female", StringComparison.OrdinalIgnoreCase);
        HairItemID = profile.HairItemId;
        HairHue = profile.HairHue;
        FacialHairItemID = Female ? 0 : profile.BeardItemId;
        FacialHairHue = profile.HairHue;
        SpeechHue = profile.Guard ? 0x59 : 0x3B2;
        FightMode = profile.Guard ? FightMode.Aggressor : FightMode.None;
        if (profile.Guard)
        {
            ChangeAIType(AIType.AI_Melee);
            InitStats(100, 90, 80);
            SetSkill(SkillName.Swords, 75, 90);
            SetSkill(SkillName.Tactics, 75, 90);
        }

        if (string.IsNullOrWhiteSpace(_currentOutfit))
        {
            ApplyOutfit("everyday");
        }
    }

    public void SimulationTick(DateTime localTime)
    {
        if (_profile == null)
        {
            BindProfile(BritainData.Current?.GetResident(_profileId));
            if (_profile == null)
            {
                return;
            }
        }

        if (!_profile.Guard && Combatant != null)
        {
            HandleDanger();
            return;
        }

        var schedule = BritainData.Current.GetSchedule(_profile.ScheduleId);
        var nextEntry = schedule?.Resolve(localTime.DayOfWeek, localTime.Hour * 60 + localTime.Minute);
        if (nextEntry == null)
        {
            return;
        }

        if (!ReferenceEquals(nextEntry, _scheduleEntry))
        {
            BeginActivity(nextEntry);
        }

        MoveTowardActivity(localTime);
        CheckPrivateSpace(localTime);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.Alive || !InRange(from, 3))
        {
            from.SendMessage("Du musst näher herantreten.");
            return;
        }

        RememberVisit(from);
        Direction = GetDirectionTo(from);
        Say(GetGreeting(from));
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);
        if (e.Handled || !e.Mobile.Alive || !InRange(e.Mobile, 10) || _profile == null)
        {
            return;
        }

        RememberVisit(e.Mobile);
        Direction = GetDirectionTo(e.Mobile);
        var text = Normalize(e.Speech);
        var response = ResolveSpeech(e.Mobile, text);
        if (response == null)
        {
            return;
        }

        e.Handled = true;
        Say(response);
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        if (from?.Player == true)
        {
            ChangeReputation(from, -Math.Max(5, amount / 2));
            Say(_profile?.Guard == true ? "Waffen nieder! Ihr greift die Wache von Britain an!" : "Wachen! Zu Hilfe!");
            AlertNearbyGuards(from);
        }

        base.OnDamage(amount, from, willKill);
    }

    public override bool OnBeforeDeath()
    {
        // The vertical slice keeps named citizens persistent while the future mortality ledger is developed.
        Hits = Math.Max(1, HitsMax / 4);
        Combatant = null;
        Warmode = false;
        MoveToWorld(_profile?.SpawnFallback.ToPoint3D() ?? Location, Map.Felucca);
        Say("Ich brauche einen Heiler. Merkt euch diesen Angriff!");
        return false;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (!from.Alive || !InRange(from, 2) || _profile == null)
        {
            return false;
        }

        var quest = _profile.Quest;
        if (quest != null && GetQuestState(from) == 1 &&
            dropped.GetType().Name.Equals(quest.ItemType, StringComparison.OrdinalIgnoreCase) && dropped.Amount >= quest.Amount)
        {
            dropped.Consume(quest.Amount);
            from.AddToBackpack(new Gold(quest.RewardGold));
            _questStates[from] = 2;
            ChangeReputation(from, 25);
            Say(quest.Completion);
            return true;
        }

        // Modest gifts are remembered, but NPCs do not accept equipped or quest-bound items blindly.
        if (dropped is Food or BeverageBottle)
        {
            dropped.Consume();
            ChangeReputation(from, 3);
            Say("Das ist freundlich. Ich werde mich daran erinnern.");
            return true;
        }

        Say("Behaltet das lieber. Ich nehme nur an, worum ich gebeten habe.");
        return false;
    }

    private void BeginActivity(BritainScheduleEntry entry)
    {
        _scheduleEntry = entry;
        _currentActivity = entry.Activity;
        var interiorDestination = BritainData.Current.ResolveInteriorDestination(_profile, entry);
        _activityDestination = LivingBritainSimulation.ReserveActivityDestination(this, interiorDestination?.ToPoint3D() ?? entry.Destination.ToPoint3D());
        _path = new PathFollower(this, _activityDestination);
        _arrived = false;
        _blockedSince = Core.Now;
        CantWalk = false;
        ApplyOutfit(entry.Outfit);

        if (!IsObserved(24))
        {
            MoveToWorld(_activityDestination, Map.Felucca);
            Arrive(entry);
        }
    }

    private void MoveTowardActivity(DateTime localTime)
    {
        if (_scheduleEntry?.Destination == null)
        {
            return;
        }

        var destination = _activityDestination;
        if (_arrived && PathFollower.Check(Location, destination, ActivityRadius(_scheduleEntry) + 1))
        {
            PerformAmbientMovement(_scheduleEntry, destination);
            PlayScheduledAnimation(_scheduleEntry, localTime);
            return;
        }

        _arrived = false;
        if (PathFollower.Check(Location, destination, 1))
        {
            Arrive(_scheduleEntry);
            PlayScheduledAnimation(_scheduleEntry, localTime);
            return;
        }

        CantWalk = false;
        if (!IsObserved(24))
        {
            MoveToWorld(destination, Map.Felucca);
            Arrive(_scheduleEntry);
            return;
        }

        if (_path?.Follow(false, 1) == true)
        {
            Arrive(_scheduleEntry);
            return;
        }

        if (Core.Now - _blockedSince > TimeSpan.FromSeconds(20))
        {
            _path = new PathFollower(this, destination);
            _blockedSince = Core.Now;
        }
    }

    private void Arrive(BritainScheduleEntry entry)
    {
        _arrived = true;
        Home = _activityDestination;
        HomeMap = Map.Felucca;
        RangeHome = ActivityRadius(entry);
        CantWalk = IsStationary(entry);
        Warmode = false;
        _nextAmbientMove = Core.Now.AddSeconds(Utility.RandomMinMax(3, 7));
        if (entry.Sitting)
        {
            Direction = Direction.South;
        }
    }

    private void PerformAmbientMovement(BritainScheduleEntry entry, Point3D destination)
    {
        if (IsStationary(entry) || Core.Now < _nextAmbientMove)
        {
            return;
        }

        _nextAmbientMove = Core.Now.AddSeconds(Utility.RandomMinMax(4, 9));
        var radius = ActivityRadius(entry);
        if (!InRange(destination, radius))
        {
            Move(GetDirectionTo(destination));
            return;
        }

        var firstDirection = Utility.Random(8);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (Move((Direction)((firstDirection + attempt * 2) & 0x7)))
            {
                return;
            }
        }
    }

    private void PlayScheduledAnimation(BritainScheduleEntry entry, DateTime localTime)
    {
        if (entry.Activity.Equals("sleep", StringComparison.OrdinalIgnoreCase) && localTime >= _nextSleepAnimation)
        {
            Animate(22, 5, 1, false, false, 0);
            _nextSleepAnimation = localTime.AddSeconds(12);
        }
    }

    private static bool IsStationary(BritainScheduleEntry entry) =>
        entry.Sitting || entry.Activity.Equals("sleep", StringComparison.OrdinalIgnoreCase);

    private static int ActivityRadius(BritainScheduleEntry entry) => entry.Activity.ToLowerInvariant() switch
    {
        "patrol" => 5,
        "errand" or "leisure" or "visit" => 3,
        "work" => 2,
        _ => 1
    };

    private void HandleDanger()
    {
        CantWalk = false;
        Warmode = false;
        var refuge = BritainData.Current.GetBuilding(_profile.HomeId)?.Center?.ToPoint3D() ?? _profile.SpawnFallback.ToPoint3D();
        _path ??= new PathFollower(this, refuge);
        if (!IsObserved(24))
        {
            MoveToWorld(refuge, Map.Felucca);
            Combatant = null;
            return;
        }

        _path.Follow(true, 1);
    }

    private void CheckPrivateSpace(DateTime now)
    {
        if (!_currentActivity.Equals("sleep", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var mobile in GetMobilesInRange(2))
        {
            if (!mobile.Player || !mobile.Alive || !CanSpeakTo(mobile, now, TimeSpan.FromSeconds(30)))
            {
                continue;
            }

            _intrusionWarnings.TryGetValue(mobile, out var warnings);
            _intrusionWarnings[mobile] = ++warnings;
            ChangeReputation(mobile, -1);
            Say(warnings >= 3 ? "Zum dritten Mal: Verlasst mein Schlafgemach. Ich rufe die Wache!" : "Dies ist mein Schlafgemach. Bitte geht hinaus.");
            if (warnings >= 3)
            {
                AlertNearbyGuards(mobile);
            }
        }
    }

    private string ResolveSpeech(Mobile from, string text)
    {
        var dialogue = BritainData.Current.GetDialogue(_profile.DialogueId);
        if (dialogue == null)
        {
            return null;
        }

        if (ContainsAny(text, "hallo", "gruss", "guten tag", "morgen", "abend", "hello", "hail"))
        {
            return GetGreeting(from);
        }

        if (ContainsAny(text, "name", "heisst", "wer bist"))
        {
            return $"Ich heiße {_profile.FullName}.";
        }

        if (ContainsAny(text, "arbeit", "beruf", "job", "work"))
        {
            return Pick(dialogue.Work);
        }

        if (ContainsAny(text, "wohn", "zuhause", "home", "haus"))
        {
            return $"Mein Zuhause ist {BritainData.Current.GetBuilding(_profile.HomeId)?.Name}.";
        }

        if (ContainsAny(text, "famil", "family", "nachbar", "kennst"))
        {
            return Pick(dialogue.Relationships);
        }

        if (ContainsAny(text, "britain", "stadt", "king", "konig", "markt", "handel", "preis"))
        {
            return Pick(dialogue.General);
        }

        if (ContainsAny(text, "wache", "guard", "krimin", "dieb"))
        {
            return _profile.Guard ? Pick(dialogue.Work) : Pick(dialogue.Rumors);
        }

        if (ContainsAny(text, "essen", "food", "hungrig"))
        {
            return $"Am liebsten esse ich {_profile.FavoriteDish}; im Alltag genügt mir {_profile.SimpleMeal}.";
        }

        if (ContainsAny(text, "trink", "drink", "ale", "wein", "bier"))
        {
            return $"Meist trinke ich {_profile.EverydayDrink}, nach Feierabend gern {_profile.AlcoholicDrink}.";
        }

        if (ContainsAny(text, "gerucht", "neues", "rumor"))
        {
            return Pick(dialogue.Rumors);
        }

        if (ContainsAny(text, "hilfe", "arbeit fur", "help", "aufgabe"))
        {
            return HandleQuestSpeech(from, dialogue);
        }

        foreach (var relationship in _profile.Relationships)
        {
            var related = BritainData.Current.GetResident(relationship.NpcId);
            if (related != null && (text.Contains(Normalize(related.FirstName)) || text.Contains(Normalize(related.LastName))))
            {
                return $"{related.FullName}? {relationship.Background} {relationship.State}";
            }
        }

        var reputation = GetReputation(from);
        return reputation <= -20 ? Pick(dialogue.Rejecting) : Pick(dialogue.Situational);
    }

    private string HandleQuestSpeech(Mobile from, BritainDialogueProfile dialogue)
    {
        var quest = _profile.Quest;
        if (quest == null)
        {
            return dialogue.Request;
        }

        return GetQuestState(from) switch
        {
            0 => AcceptQuest(from, quest),
            1 => quest.Reminder,
            _ => "Ihr habt mir bereits geholfen. Das vergesse ich nicht."
        };
    }

    private string AcceptQuest(Mobile from, BritainQuestProfile quest)
    {
        _questStates[from] = 1;
        return quest.Offer;
    }

    private string GetGreeting(Mobile from)
    {
        var dialogue = BritainData.Current.GetDialogue(_profile.DialogueId);
        var reputation = GetReputation(from);
        if (reputation <= -20)
        {
            return Pick(dialogue.Rejecting);
        }

        if (reputation >= 20)
        {
            return Pick(dialogue.Friendly);
        }

        var hour = Clock.WorldTime.Hour;
        var index = hour switch
        {
            < 6 => 3,
            < 11 => 0,
            < 17 => 1,
            < 22 => 2,
            _ => 3
        };
        if (_currentActivity is "work" or "patrol")
        {
            index = 4;
        }

        return dialogue.Greetings[index % dialogue.Greetings.Count];
    }

    private void RememberVisit(Mobile from)
    {
        _visits.TryGetValue(from, out var count);
        _visits[from] = Math.Min(1000, count + 1);
    }

    private int GetQuestState(Mobile from) => _questStates.TryGetValue(from, out var state) ? state : 0;
    private int GetReputation(Mobile from) => _reputation.TryGetValue(from, out var value) ? value : 0;

    private void ChangeReputation(Mobile from, int amount)
    {
        if (from == null)
        {
            return;
        }

        _reputation[from] = Math.Clamp(GetReputation(from) + amount, -100, 100);
    }

    private void AlertNearbyGuards(Mobile aggressor)
    {
        foreach (var mobile in Map.GetMobilesInRange(Location, 16))
        {
            if (mobile is not LivingBritainResident { Profile.Guard: true } guard)
            {
                continue;
            }

            guard.CantWalk = false;
            guard.Combatant = aggressor;
            guard.Warmode = true;
            guard.Say("Im Namen Britains: Stehenbleiben!");
        }
    }

    private void ApplyOutfit(string outfitName)
    {
        if (_profile == null || string.IsNullOrWhiteSpace(outfitName) ||
            outfitName.Equals(_currentOutfit, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var style = BritainData.Current.GetOutfit(_profile.OutfitStyle);
        if (style == null || !style.Sets.TryGetValue(outfitName, out var pieces))
        {
            if (style == null || !style.Sets.TryGetValue("everyday", out pieces))
            {
                return;
            }
        }

        foreach (var item in Items.ToArray())
        {
            if (OutfitLayers.Contains(item.Layer))
            {
                item.Delete();
            }
        }

        foreach (var piece in pieces)
        {
            AddItem(new Item(piece.ItemId)
            {
                Layer = piece.Layer,
                Hue = piece.Hue,
                Name = piece.Name,
                Movable = false,
                LootType = LootType.Blessed
            });
        }

        _currentOutfit = outfitName;
    }

    private bool IsObserved(int range)
    {
        foreach (var state in GetClientsInRange(range))
        {
            if (state.Mobile?.Player == true)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanSpeakTo(Mobile mobile, DateTime now, TimeSpan cooldown)
    {
        if (_speechCooldowns.TryGetValue(mobile, out var next) && next > now)
        {
            return false;
        }

        _speechCooldowns[mobile] = now + cooldown;
        return true;
    }

    private static bool ContainsAny(string text, params string[] needles) => needles.Any(text.Contains);

    private static string Pick(IReadOnlyList<string> values) =>
        values == null || values.Count == 0 ? "Dazu habe ich heute nichts zu sagen." : values[Utility.Random(values.Count)];

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        _reputation ??= [];
        _visits ??= [];
        _questStates ??= [];
        _currentActivity ??= "resume";
        _currentOutfit ??= "";
    }
}
