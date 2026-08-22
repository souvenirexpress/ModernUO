using System;
using System.Collections.Generic;

namespace Server.Commands;

public static class TownTeleportCommand
{
    private static readonly Dictionary<string, Point3D> Towns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Yew"] = new(633, 858, 0),
        ["Minoc"] = new(2476, 413, 15),
        ["Britain"] = new(1496, 1628, 10),
        ["Moonglow"] = new(4408, 1168, 0),
        ["Trinsic"] = new(1845, 2745, 0),
        ["Magincia"] = new(3734, 2222, 20),
        ["Jhelom"] = new(1374, 3826, 0),
        ["Skara"] = new(618, 2234, 0),
        ["Vesper"] = new(2771, 976, 0),
    };

    public static void Configure() => CommandSystem.Register("Town", AccessLevel.Player, Teleport);

    [Usage("Town <name>")]
    [Description("Teleportiert den Spieler zu einer sicheren Stadtposition in Felucca.")]
    private static void Teleport(CommandEventArgs e)
    {
        if (e.Arguments.Length != 1 || !Towns.TryGetValue(e.Arguments[0], out var location))
        {
            e.Mobile.SendMessage("Stadt nicht gefunden.");
            return;
        }

        e.Mobile.MoveToWorld(location, Map.Felucca);
        e.Mobile.SendMessage($"Nach {e.Arguments[0]} teleportiert.");
    }
}
