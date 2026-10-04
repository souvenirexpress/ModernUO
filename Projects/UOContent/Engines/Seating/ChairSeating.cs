using System;

namespace Server.Engines.Seating;

public static class ChairSeating
{
    public static int Facing(int itemId, Direction current) => itemId switch
    {
        0xB4E or 0xB52 or 0xB56 or 0xB5A or 0x1219 or 0xB2F or 0xB33 or 0x2DE3 or 0x2DED => 2,
        0xB4F or 0xB53 or 0xB57 or 0xB5B or 0x1218 or 0xB2E or 0xB32 or 0x2DE4 or 0x2DEC => 4,
        0xB50 or 0xB54 or 0xB59 or 0xB5C or 0x121A or 0xB31 or 0x2DE6 or 0x2DEB => 0,
        0xB51 or 0xB55 or 0xB58 or 0xB5D or 0x121B or 0xB30 or 0x2DE5 or 0x2DEE => 6,
        0xA2A or 0xB5E or 0x2DF5 or 0x2DF6 => (int)current & 6,
        0xB2C => ((int)current & 4) + 2,
        0xB2D => (int)current & 4,
        _ => -1
    };

    public static bool TrySit(Mobile from, Item chair)
    {
        if (from?.Deleted != false || chair?.Deleted != false || chair.Parent != null ||
            chair.Map == null || chair.Map == Map.Internal || chair.Map != from.Map ||
            Facing(chair.ItemID, from.Direction) < 0)
        {
            return false;
        }

        if (!from.Alive || !from.Body.IsHuman || from.Mounted || from.Flying ||
            from.Frozen || from.Paralyzed || from.Warmode)
        {
            from.SendMessage("Ihr koennt Euch gerade nicht setzen.");
            return false;
        }

        var location = chair.Location;
        if (!from.InRange(location, 1) || Math.Abs(from.Z - location.Z) > 2)
        {
            from.SendMessage("Geht naeher an den Stuhl heran.");
            return false;
        }

        if (!from.CanSee(chair) || !from.InLOS(chair) || !chair.IsAccessibleTo(from) ||
            !chair.Map.CanFit(location, 16, false, false, false))
        {
            from.SendMessage("Dieser Sitzplatz ist nicht erreichbar.");
            return false;
        }

        foreach (var other in chair.Map.GetMobilesInRange(location, 0))
        {
            if (other != from && !other.Deleted && Math.Abs(other.Z - location.Z) < 16)
            {
                from.SendMessage("Dieser Sitzplatz ist bereits besetzt.");
                return false;
            }
        }

        // UO clients derive sitting from a stationary human on the chair tile.
        // No saved pose or timer: movement, removal and relocation end it naturally.
        from.Direction = (Direction)Facing(chair.ItemID, from.Direction);
        from.Location = location;
        return true;
    }
}
