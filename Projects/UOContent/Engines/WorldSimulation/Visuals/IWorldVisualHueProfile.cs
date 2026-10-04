namespace Server.Engines.WorldSimulation;

public interface IWorldVisualHueProfile
{
    int DefaultHue { get; }
    int BurningHue { get; }
    int CharredHue { get; }
    int AshHue { get; }
}
