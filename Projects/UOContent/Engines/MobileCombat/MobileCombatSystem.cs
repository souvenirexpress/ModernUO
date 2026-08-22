using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.MobileCombat;

public static class MobileCombatSystem
{
    private const int EncodedCommandId = 0x70;
    private const int StopAction = 1;
    private const int GuardAction = 2;
    private const int DodgeAction = 3;
    private const int SwitchWeaponAction = 4;

    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(MobileCombatSystem));
    private static readonly Dictionary<Mobile, CombatSession> Sessions = [];
    private static long _acceptedCommands;
    private static long _rejectedCommands;
    private static long _attackRequests;
    private static long _preventedDamage;

    public static unsafe void Configure()
    {
        IncomingPackets.RegisterEncoded(EncodedCommandId, true, &ReceiveCommand);
        CommandSystem.Register("CombatStats", AccessLevel.GameMaster, ShowStats);
        Logger.Information("Mobile combat protocol active on encoded command 0x{CommandId:X2}.", EncodedCommandId);
    }

    public static bool AllowAttackRequest(Mobile from, Mobile target)
    {
        if (from is not PlayerMobile player)
        {
            return true;
        }

        var now = Core.Now;
        var session = GetSession(player);
        if (now < session.NextAttackRequest || target == player || target.Deleted || !target.Alive ||
            target.Map != player.Map || !Utility.InUpdateRange(player.Location, target.Location))
        {
            _rejectedCommands++;
            return false;
        }

        session.NextAttackRequest = now + TimeSpan.FromMilliseconds(120);
        _attackRequests++;
        return true;
    }

    public static int ApplyDefense(PlayerMobile defender, Mobile attacker, int amount)
    {
        if (amount <= 0 || !Sessions.TryGetValue(defender, out var session))
        {
            return amount;
        }

        var now = Core.Now;
        double reduction;
        string feedback;

        if (now <= session.DodgeUntil)
        {
            reduction = 0.5;
            feedback = "Du weichst einem Teil des Schadens aus.";
            session.DodgeUntil = DateTime.MinValue;
        }
        else if (now <= session.GuardUntil)
        {
            var shield = defender.FindItemOnLayer<BaseShield>(Layer.TwoHanded) != null;
            reduction = shield ? 0.35 : 0.2;
            feedback = shield ? "Dein Schild faengt einen Teil des Schlages ab." : "Du faengst einen Teil des Schlages ab.";
            session.GuardUntil = DateTime.MinValue;
        }
        else
        {
            return amount;
        }

        var adjusted = Math.Max(1, (int)Math.Ceiling(amount * (1.0 - reduction)));
        _preventedDamage += amount - adjusted;
        defender.SendMessage(0x59, feedback);
        return adjusted;
    }

    private static void ReceiveCommand(NetState state, IEntity entity, EncodedReader reader)
    {
        if (state.Mobile is not PlayerMobile player || entity?.Serial != player.Serial)
        {
            _rejectedCommands++;
            return;
        }

        var action = reader.ReadInt32();
        var sequence = reader.ReadInt32();
        var argument = reader.ReadInt32();
        var now = Core.Now;
        var session = GetSession(player);

        if (action is < StopAction or > SwitchWeaponAction || sequence <= 0 ||
            now < session.NextCommand || sequence <= session.LastSequence && now - session.LastCommandAt < TimeSpan.FromSeconds(10))
        {
            _rejectedCommands++;
            return;
        }

        session.LastSequence = sequence;
        session.LastCommandAt = now;
        session.NextCommand = now + TimeSpan.FromMilliseconds(80);
        _acceptedCommands++;

        switch (action)
        {
            case StopAction:
                player.Combatant = null;
                break;
            case GuardAction:
                Guard(player, session, now);
                break;
            case DodgeAction:
                Dodge(player, session, now, argument);
                break;
            case SwitchWeaponAction:
                SwitchWeapon(player, session, now);
                break;
        }
    }

    private static void Guard(PlayerMobile player, CombatSession session, DateTime now)
    {
        const int staminaCost = 8;
        if (!player.Alive || now < session.GuardReadyAt)
        {
            player.SendMessage(0x35, "Blocken ist noch nicht bereit.");
            return;
        }
        if (player.Stam < staminaCost)
        {
            player.SendMessage(0x35, "Du hast nicht genug Ausdauer zum Blocken.");
            return;
        }

        player.Stam -= staminaCost;
        session.GuardUntil = now + TimeSpan.FromSeconds(1.5);
        session.GuardReadyAt = now + TimeSpan.FromSeconds(3);
        player.SendMessage(0x59, "Du gehst fuer einen Moment in Deckung.");
    }

    private static void Dodge(PlayerMobile player, CombatSession session, DateTime now, int direction)
    {
        const int staminaCost = 15;
        if (!player.Alive || now < session.DodgeReadyAt)
        {
            player.SendMessage(0x35, "Ausweichen ist noch nicht bereit.");
            return;
        }
        if (player.Stam < staminaCost)
        {
            player.SendMessage(0x35, "Du hast nicht genug Ausdauer zum Ausweichen.");
            return;
        }
        if (direction is < 0 or > 7 || !player.Move((Direction)direction))
        {
            player.SendMessage(0x35, "Dorthin kannst du nicht ausweichen.");
            return;
        }

        player.Stam -= staminaCost;
        session.DodgeUntil = now + TimeSpan.FromSeconds(0.55);
        session.DodgeReadyAt = now + TimeSpan.FromSeconds(4);
        player.SendMessage(0x59, "Du machst einen schnellen Ausweichschritt.");
    }

    private static void SwitchWeapon(PlayerMobile player, CombatSession session, DateTime now)
    {
        if (now < session.WeaponReadyAt)
        {
            return;
        }
        session.WeaponReadyAt = now + TimeSpan.FromMilliseconds(800);

        var backpack = player.Backpack;
        if (backpack == null)
        {
            player.SendMessage(0x35, "Du hast keinen Rucksack fuer einen Waffenwechsel.");
            return;
        }

        BaseWeapon selected = null;
        var current = player.FindItemOnLayer(Layer.OneHanded) as BaseWeapon ??
                      player.FindItemOnLayer(Layer.TwoHanded) as BaseWeapon;
        var currentOrder = WeaponOrder(current);
        var bestOrder = int.MaxValue;
        var wrappedOrder = int.MaxValue;
        BaseWeapon wrapped = null;

        foreach (var weapon in backpack.FindItemsByType<BaseWeapon>())
        {
            var order = WeaponOrder(weapon);
            if (order < 0)
            {
                continue;
            }
            if (order > currentOrder && order < bestOrder)
            {
                selected = weapon;
                bestOrder = order;
            }
            if (order < wrappedOrder)
            {
                wrapped = weapon;
                wrappedOrder = order;
            }
        }

        selected ??= wrapped;
        if (selected == null)
        {
            player.SendMessage(0x35, "Keine weitere unterstuetzte Waffe im Rucksack gefunden.");
            return;
        }

        player.ClearHands();
        if (player.EquipItem(selected))
        {
            player.SendMessage(0x59, $"Waffe gewechselt: {selected.GetType().Name}.");
            return;
        }

        player.SendMessage(0x35, "Diese Waffe kann im Moment nicht angelegt werden.");
    }

    private static int WeaponOrder(BaseWeapon weapon) => weapon switch
    {
        Longsword => 0,
        TwoHandedAxe => 1,
        Spear => 2,
        Dagger => 3,
        Bow => 4,
        Crossbow => 5,
        HeavyCrossbow => 6,
        _ => -1
    };

    private static CombatSession GetSession(Mobile mobile)
    {
        if (!Sessions.TryGetValue(mobile, out var session))
        {
            session = new CombatSession();
            Sessions[mobile] = session;
        }
        return session;
    }

    private static void ShowStats(CommandEventArgs e)
    {
        e.Mobile.SendMessage(
            $"MobileCombat: Sessions {Sessions.Count}, Befehle {_acceptedCommands}, abgelehnt {_rejectedCommands}, Angriffe {_attackRequests}, verhinderter Schaden {_preventedDamage}."
        );
    }

    private sealed class CombatSession
    {
        public int LastSequence { get; set; }
        public DateTime LastCommandAt { get; set; }
        public DateTime NextCommand { get; set; }
        public DateTime NextAttackRequest { get; set; }
        public DateTime GuardReadyAt { get; set; }
        public DateTime GuardUntil { get; set; }
        public DateTime DodgeReadyAt { get; set; }
        public DateTime DodgeUntil { get; set; }
        public DateTime WeaponReadyAt { get; set; }
    }
}
