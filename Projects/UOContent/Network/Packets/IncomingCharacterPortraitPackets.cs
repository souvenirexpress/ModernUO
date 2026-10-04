using System.Buffers;
using Server.Accounting;

namespace Server.Network;

public static partial class IncomingExtendedCommandPackets
{
    private const byte CharacterPortraitPacketVersion = 1;

    public static void CharacterPortraitRequest(NetState state, SpanReader reader)
    {
        if (state.Account is not IAccount account)
        {
            return;
        }

        var length = 7;
        byte portraitCount = 0;

        for (var slot = 0; slot < account.Length; slot++)
        {
            var mobile = account[slot];
            if (mobile == null)
            {
                continue;
            }

            portraitCount++;
            length += 6;
            for (var itemIndex = 0; itemIndex < mobile.Items.Count; itemIndex++)
            {
                if (IsPortraitEquipment(mobile.Items[itemIndex]))
                {
                    length += 5;
                }
            }
        }

        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write((ushort)0x007F);
        writer.Write(CharacterPortraitPacketVersion);
        writer.Write(portraitCount);

        for (var slot = 0; slot < account.Length; slot++)
        {
            var mobile = account[slot];
            if (mobile == null)
            {
                continue;
            }

            byte equipmentCount = 0;
            for (var itemIndex = 0; itemIndex < mobile.Items.Count; itemIndex++)
            {
                if (IsPortraitEquipment(mobile.Items[itemIndex]))
                {
                    equipmentCount++;
                }
            }

            writer.Write((byte)slot);
            writer.Write((ushort)mobile.Body);
            writer.Write((ushort)mobile.Hue);
            writer.Write(equipmentCount);

            for (var itemIndex = 0; itemIndex < mobile.Items.Count; itemIndex++)
            {
                var item = mobile.Items[itemIndex];
                if (!IsPortraitEquipment(item))
                {
                    continue;
                }

                writer.Write((ushort)item.ItemID);
                writer.Write((ushort)item.Hue);
                writer.Write((byte)item.Layer);
            }
        }

        state.Send(writer.Span);
    }

    private static bool IsPortraitEquipment(Item item) =>
        !item.Deleted &&
        item.Layer is >= Layer.FirstValid and <= Layer.LastUserValid &&
        item.Layer != Layer.Backpack;
}
