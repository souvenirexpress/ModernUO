using System;
using System.Buffers;

namespace Server.Engines.CharacterCreation;

public static class BrowserCreationAppearance
{
    // Versioned browser data in the creation packet's fifteen reserved bytes.
    public static int[] ReadHues(ref SpanReader reader)
    {
        var first = reader.ReadByte();
        var second = reader.ReadByte();
        var version = reader.ReadByte();
        int[] hues = [reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16()];
        reader.ReadUInt32();
        if (first != 0x41 || second != 0x42 || version != 1)
        {
            return null;
        }

        foreach (var hue in hues)
        {
            if (!IsSupportedHue(hue))
            {
                return null;
            }
        }
        return hues;
    }

    public static bool IsSupportedHue(int hue) => hue is 0 or 0x25 or 0x58 or 0x44 or 0x1C2 or 0x21E or 0x3E9 or 0x47E;
}
