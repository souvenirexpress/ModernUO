using Server.Items;

namespace Server.Engines.WorldSimulation;

public static class SourceCapabilityResolver
{
    public static ToolCapabilities GetCapabilities(object source)
    {
        if (source is Item { Deleted: true } || source is IWorldSimulatedObject { State.Condition: <= 0 })
        {
            return default;
        }
        if (source is IWorldInteractionSource interactionSource)
        {
            return interactionSource.Capabilities;
        }

        if (source is BaseWeapon { MaxHitPoints: > 0, HitPoints: <= 0 })
        {
            return default;
        }
        if (source is BaseAxe)
        {
            return new ToolCapabilities(CutPower: 0.55, ChopPower: 0.9);
        }
        if (source is BaseKnife)
        {
            return new ToolCapabilities(CutPower: 0.75);
        }

        if (source is BaseLight { Burning: true })
        {
            return new ToolCapabilities(HeatPower: 0.8, IgnitePower: 0.8, LightPower: 1.0);
        }

        if (source is BaseBeverage { IsEmpty: false, Content: BeverageType.Water, Pourable: true })
        {
            return new ToolCapabilities(CoolPower: 1.0, ExtinguishPower: 1.0);
        }

        if (source is BaseWaterContainer { IsEmpty: false })
        {
            return new ToolCapabilities(CoolPower: 1.0, ExtinguishPower: 1.0);
        }

        if (source is IWorldSimulatedObject { PrimaryMaterial: MaterialId.Water })
        {
            return new ToolCapabilities(CoolPower: 1.0, ExtinguishPower: 1.0);
        }

        return default;
    }
}
