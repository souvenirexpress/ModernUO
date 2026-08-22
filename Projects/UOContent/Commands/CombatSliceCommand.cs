using System;
using Server.Items;
using Server.Mobiles;

namespace Server.Commands;

public static class CombatSliceCommand
{
    private static readonly Point3D ArenaCenter = new(5445, 1153, 0);

    public static void Configure()
    {
        CommandSystem.Register("CombatSlice", AccessLevel.Player, Enter);
        CommandSystem.Register("CombatLeave", AccessLevel.Player, Leave);
    }

    [Usage("CombatSlice")]
    [Description("Betritt den isolierten Testbereich fuer das mobile Kampfsystem.")]
    private static void Enter(CommandEventArgs e)
    {
        var player = e.Mobile;
        EnsureArena();
        EnsureTestKit(player);
        player.MoveToWorld(ArenaCenter, Map.Felucca);
        player.SendMessage(0x59, "Mobiler Kampf-Testbereich. Mit [CombatLeave kehrst du nach Britain zurueck.");
    }

    [Usage("CombatLeave")]
    [Description("Verlaesst den mobilen Kampf-Testbereich.")]
    private static void Leave(CommandEventArgs e)
    {
        e.Mobile.Combatant = null;
        e.Mobile.Warmode = false;
        e.Mobile.MoveToWorld(new Point3D(1496, 1628, 10), Map.Felucca);
    }

    private static void EnsureArena()
    {
        EnsureCreature("Abadoria Trainingsziel", new Point3D(5449, 1153, 0), () => new MobileCombatTrainingDummy());
        EnsureCreature("Trainingsratte", new Point3D(5441, 1150, 0), () => new Rat());
        EnsureCreature("Trainingswolf", new Point3D(5440, 1154, 0), () => new TimberWolf());
        EnsureCreature("Bandit Nahkampf", new Point3D(5450, 1148, 0), () => new Brigand());
        EnsureCreature("Bandit Fernkampf", new Point3D(5453, 1158, 0), () => new RatmanArcher());
        EnsureCreature("Schwerer Gegner", new Point3D(5438, 1160, 0), () => new Ogre());
        EnsureCreature("Bandit Gruppe A", new Point3D(5454, 1146, 0), () => new Brigand());
        EnsureCreature("Bandit Gruppe B", new Point3D(5456, 1148, 0), () => new Brigand());
    }

    private static void EnsureCreature(string name, Point3D location, Func<BaseCreature> factory)
    {
        foreach (var mobile in World.Mobiles.Values)
        {
            if (!mobile.Deleted && mobile.Map == Map.Felucca && mobile.Name == name)
            {
                return;
            }
        }

        var creature = factory();
        creature.Name = name;
        creature.Home = location;
        creature.RangeHome = 8;
        creature.Tamable = false;
        creature.MoveToWorld(location, Map.Felucca);
    }

    private static void EnsureTestKit(Mobile player)
    {
        AddWeapon<Longsword>(player, () => new Longsword());
        AddWeapon<TwoHandedAxe>(player, () => new TwoHandedAxe());
        AddWeapon<Spear>(player, () => new Spear());
        AddWeapon<Dagger>(player, () => new Dagger());
        AddWeapon<Bow>(player, () => new Bow());
        AddWeapon<Crossbow>(player, () => new Crossbow());
        if (player.FindItemOnLayer(Layer.TwoHanded) is not BaseShield && player.Backpack != null)
        {
            var hasShield = false;
            foreach (var _ in player.Backpack.FindItemsByType<BaseShield>())
            {
                hasShield = true;
                break;
            }
            if (!hasShield) player.AddToBackpack(new HeaterShield());
        }

        var arrows = 0;
        var bolts = 0;
        if (player.Backpack != null)
        {
            foreach (var arrow in player.Backpack.FindItemsByType<Arrow>()) arrows += arrow.Amount;
            foreach (var bolt in player.Backpack.FindItemsByType<Bolt>()) bolts += bolt.Amount;
        }
        if (arrows < 100) player.AddToBackpack(new Arrow(250));
        if (bolts < 100) player.AddToBackpack(new Bolt(250));
    }

    private static void AddWeapon<T>(Mobile player, Func<T> factory) where T : BaseWeapon
    {
        if (player.FindItemOnLayer(Layer.OneHanded) is T || player.FindItemOnLayer(Layer.TwoHanded) is T)
        {
            return;
        }
        if (player.Backpack != null)
        {
            foreach (var _ in player.Backpack.FindItemsByType<T>()) return;
        }
        player.AddToBackpack(factory());
    }
}
