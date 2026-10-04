namespace Server.Engines.LivingBritain;

public partial class LivingBritainWorldSimulationItem
{
    private void MigrateFrom(V0Content content)
    {
        _overrideId = content.OverrideId;
        _definitionFingerprint = content.DefinitionFingerprint;
        _cutPower = content.CutPower;
        _chopPower = content.ChopPower;
        _piercePower = content.PiercePower;
        _strikePower = content.StrikePower;
        _crushPower = content.CrushPower;
        _pryPower = content.PryPower;
        _heatPower = content.HeatPower;
        _ignitePower = content.IgnitePower;
        _coolPower = content.CoolPower;
        _extinguishPower = content.ExtinguishPower;
        _lightPower = content.LightPower;
        _defaultItemId = content.DefaultItemId;
        _burningItemId = content.BurningItemId;
        _charredItemId = content.CharredItemId;
        _ashItemId = content.AshItemId;
        // Version 0 used the item's Hue for every visual state.
        _defaultHue = _burningHue = _charredHue = _ashHue = Hue;
    }
}
