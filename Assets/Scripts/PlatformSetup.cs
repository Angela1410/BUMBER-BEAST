using UnityEngine;

// Per-platform defaults applied once before the first scene:
//  - Phones and tablets (Android, iPhone/iPad) run at 60 fps; Unity's mobile default is 30, which
//    makes aiming and fast bounces feel choppy.
//  - The Web build (itch.io, also played in iPhone/Android browsers) keeps -1 so the browser
//    paces frames with the display; any other value makes it fall back to a slower timer.
//  - Phones never dim the screen in the middle of a stage (the player may be lining up a shot).
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
