using Server.Items;

namespace Server.Engines.WorldSimulation;

public static class SourceCapabilityResolver
{
    public static ToolCapabilities GetCapabilities(object source)
    {
        if (source is IWorldInteractionSource interactionSource)
        {
            return interactionSource.Capabilities;
        }

        if (source is BaseLight { Burning: true })
        {
            return new ToolCapabilities(HeatPower: 0.8, IgnitePower: 0.8, LightPower: 1.0);
        }

        if (source is BaseBeverage { IsEmpty: false, Content: BeverageType.Water })
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
