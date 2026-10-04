using System.Buffers;
using Server.Items;
using Server.Network;

namespace Server.Engines.WorldSimulation;

public static class WorldInteractionPackets
{
    public const ushort Command = 0x81;

    public static unsafe void Configure() => IncomingExtendedCommandPackets.RegisterExtended(Command, true, &Receive);

    public static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not { Deleted: false } actor || reader.Remaining < 9)
        {
            return;
        }
        var kind = reader.ReadByte();
        var request = reader.ReadUInt32();
        var target = World.FindItem((Serial)reader.ReadUInt32());
        if (kind == 0 && reader.Remaining == 0)
        {
            SendOffers(state, request, target);
        }
        else if (kind == 1 && reader.Remaining == 5)
        {
            var source = World.FindItem((Serial)reader.ReadUInt32());
            var action = (WorldInteractionAction)reader.ReadByte();
            if (target is not IWorldSimulatedObject)
            {
                return;
            }
            var result = InteractionResolver.Resolve(new InteractionContext
                { Actor = actor, Source = source, Target = target, Action = action });
            WorldSimulationMessaging.Send(actor, result);
            SendOffers(state, request, target);
        }
    }

    private static void SendOffers(NetState state, uint request, Item target)
    {
        var offers = WorldInteractionMenu.GetOffers(state.Mobile, target);
        var length = 16 + offers.Count * 5;
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(Command);
        writer.Write((byte)1);
        writer.Write(request);
        writer.Write(target?.Serial.Value ?? 0u);
        writer.Write((byte)(target is IWorldSimulatedObject ? 1 : 0));
        writer.Write((byte)offers.Count);
        foreach (var offer in offers)
        {
            writer.Write((byte)offer.Action);
            writer.Write(offer.Source?.Serial.Value ?? 0u);
        }
        state.Send(writer.Span);
    }
}
