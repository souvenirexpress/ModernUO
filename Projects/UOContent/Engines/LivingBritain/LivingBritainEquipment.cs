using System;
using System.Linq;

namespace Server.Engines.LivingBritain;

public static class LivingBritainEquipment
{
    internal static void RefreshMissingLayer(Item item, string overrideId, BritainDataSet data)
    {
        if (item.Deleted || item.Layer != Layer.Invalid || string.IsNullOrWhiteSpace(overrideId) || data == null)
        {
            return;
        }

        // Old carried items are outside the world-placement synchronizer. Repair
        // equipment metadata only; never reapply a template's state or location.
        var definition = data.StaticOverrides.FirstOrDefault(entry =>
            string.Equals(entry.Id, overrideId, StringComparison.OrdinalIgnoreCase));
        if (definition is not { Equippable: true, Operation: "add" or "replace" })
        {
            return;
        }

        LivingBritainStaticSemantics.ApplyEquipment(item, definition);
    }
}

public partial class LivingBritainStaticItem
{
    public override bool CanEquip(Mobile from)
    {
        LivingBritainEquipment.RefreshMissingLayer(this, OverrideId, BritainData.Current);
        return base.CanEquip(from);
    }
}

public partial class LivingBritainWorldSimulationItem
{
    public override bool CanEquip(Mobile from)
    {
        LivingBritainEquipment.RefreshMissingLayer(this, OverrideId, BritainData.Current);
        return base.CanEquip(from);
    }
}
