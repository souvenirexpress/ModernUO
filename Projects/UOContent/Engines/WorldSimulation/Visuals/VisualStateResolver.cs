namespace Server.Engines.WorldSimulation;

public static class VisualStateResolver
{
    public static void Apply(IWorldSimulatedObject target)
    {
        if (target is not IWorldVisualProfile profile)
        {
            return;
        }

        var state = target.State;
        var itemID = state.FuelRemaining <= 0.0
            ? profile.AshItemID
            : state.IsBurning
                ? profile.BurningItemID
                : state.IsCharred
                    ? profile.CharredItemID
                    : profile.DefaultItemID;

        if (itemID > 0 && target.Item.ItemID != itemID)
        {
            target.Item.ItemID = itemID;
        }

        target.Item.Light = state.IsBurning ? LightType.Circle300 : LightType.Empty;
    }
}
