using System.Buffers;
using System.Globalization;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.CharacterPortraits;

public static class CharacterPortraitSystem
{
    public const ushort Command = 0x80;
    public const int PortraitCount = 100;

    public static unsafe void Configure()
    {
        IncomingExtendedCommandPackets.RegisterExtended(Command, false, &Receive);
    }

    public static bool IsValidPortrait(int id) => id is >= 1 and <= PortraitCount;

    private static string TagName(Mobile player) => $"Abadoria.Portrait.{player.Serial.Value}";

    public static int GetPortrait(Account account, Mobile player)
    {
        return account != null && player != null && player.Account == account &&
               int.TryParse(account.GetTag(TagName(player)), NumberStyles.None, CultureInfo.InvariantCulture, out var id) &&
               IsValidPortrait(id) ? id : 0;
    }

    public static bool SetPortrait(Account account, PlayerMobile player, int id)
    {
        if (account == null || player == null || player.Deleted || player.Account != account || !IsValidPortrait(id))
        {
            return false;
        }

        if (GetPortrait(account, player) != id)
        {
            account.SetTag(TagName(player), id.ToString(CultureInfo.InvariantCulture));
        }
        return true;
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile player)
    {
        if (player.Account is Account account)
        {
            account.RemoveTag(TagName(player));
        }
    }

    public static void Receive(NetState state, SpanReader reader)
    {
        if (state.Account is not Account account || reader.Remaining < 1)
        {
            return;
        }

        var action = reader.ReadByte();
        if (action == 1)
        {
            if (reader.Remaining != 5 || state.Mobile is not PlayerMobile player)
            {
                return;
            }
            var serial = reader.ReadUInt32();
            var id = reader.ReadByte();
            if (serial != player.Serial.Value || !SetPortrait(account, player, id))
            {
                return;
            }
        }
        else if (action != 0 || reader.Remaining != 0)
        {
            return;
        }

        SendPortraits(state, account);
    }

    public static void SendPortraits(NetState state, Account account)
    {
        byte count = 0;
        for (var slot = 0; slot < account.Length; slot++)
        {
            if (account[slot] is { Deleted: false })
            {
                count++;
            }
        }

        var length = 7 + count * 6;
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(Command);
        writer.Write((byte)1);
        writer.Write(count);
        for (var slot = 0; slot < account.Length; slot++)
        {
            var player = account[slot];
            if (player == null || player.Deleted)
            {
                continue;
            }
            writer.Write((byte)slot);
            writer.Write(player.Serial.Value);
            writer.Write((byte)GetPortrait(account, player));
        }
        state.Send(writer.Span);
    }
}
