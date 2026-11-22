// The speed the stage should run at whenever nothing else holds the clock: 1 normally, lower
// while a tutorial lesson slows or freezes the action. Short effects (hit-stop) and the pause menu
// always return to this value instead of whatever they found, so a slow-motion lesson can never
// leave the game stuck slow. StageManager resets it for each stage and keeps the clock on it.
public static class GameTime
{
    public static float BaseScale { get; private set; } = 1f;

    public static void SetBase(float scale)
    {
        BaseScale = scale < 0f ? 0f : scale;
    }

    public static void ResetBase()
    {
        BaseScale = 1f;
    }
}
