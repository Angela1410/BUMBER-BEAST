using UnityEngine;

// settings for each platform, done once before the first scene:
// - phones and tablets (Android, iPhone/iPad) run at 60 fps. Unity's phone default is 30, which
// makes aiming and fast bounces look choppy
// - the web version (itch.io, also played in phone browsers) keeps -1 so the browser times the
// frames with the screen, any other value makes it use a slower timer
// - phones never dim the screen in the middle of a stage (the player might be lining up a shot)
public static class PlatformSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.targetFrameRate = -1;
#else
        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
#endif
        Input.multiTouchEnabled = true;
    }
}
