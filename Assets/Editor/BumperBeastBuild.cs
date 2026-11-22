using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the shareable versions of the game into Builds/, ready to upload (itch.io...):
//  - Windows: Builds/Windows/<product>.exe -> Builds/<product>-Windows-v<version>.zip
//  - Web (plays in Chrome and other browsers): Builds/Web/index.html -> Builds/<product>-Web-v<version>.zip
//    with index.html at the zip root, as itch.io expects for HTML5 games.
//  - Android app: Builds/<product>-Android-v<version>.apk (64-bit, installable by sideloading).
//  - iPhone/iPad: Builds/iOS-Xcode -> Builds/<product>-iOS-Xcode-v<version>.zip, an Xcode project.
//    Windows cannot make the app itself: open the project on a Mac with Xcode, pick a signing
//    team, then run it on a device or upload it to TestFlight/App Store. Needs the iOS Build
//    Support module. iPhone players on itch.io use the Web build in Safari instead.
// Before building it applies every pending project step (StageWaveLayouts.ApplyPendingChanges), the
// app icon and the platform's window settings: the game is portrait, so desktop and web both use a
// 540 x 960 view instead of the default landscape full screen / 960 x 600 canvas.
//
// Ways to run it:
//  - the Bumper Beast menu: Build Windows (Downloadable), Build Web (Play in Chrome), Build Android App (APK);
//  - an open editor builds by itself when Builds/build-windows.request, build-web.request or
//    build-android.request exists (switching platform first if needed; the request is deleted when
//    the build starts);
//  - command line (editor closed): Unity.exe -batchmode -projectPath <project> -buildTarget Android
//    -executeMethod BumperBeastBuild.BuildAndroidFromCommandLine (BuildWebFromCommandLine with
//    -buildTarget WebGL; BuildFromCommandLine for Windows).
[InitializeOnLoad]
public static class BumperBeastBuild
{
    // App icon: a square crop of the cover art (logo + Beasts). Android 8+ launchers mask "adaptive"
    // icons to their middle 2/3, so that layer has the same crop filling exactly that middle part.
    public const string IconPath = "Assets/Sprites/AppIconCover.png";
    private const string AdaptiveBackPath = "Assets/Sprites/AppIconAdaptiveBack.png";
    private const string AdaptiveFrontPath = "Assets/Sprites/AppIconAdaptiveFront.png";
    // Launch screen: the full cover art instead of the "Made with Unity" logo (optional since Unity 6).
    // The clear spacer "logo" keeps the picture on screen for SplashSeconds.
    private const string SplashPath = "Assets/Sprites/SplashCover.png";
    private const string SplashSpacerPath = "Assets/Sprites/SplashSpacer.png";
    private const float SplashSeconds = 2f;
    // Browser tab / iPhone home-screen icon for the Web build (outside Assets, so not in the game).
    private const string WebIconPath = "BuildAssets/web-icon.png";
    public const string RequestFile = "Builds/build-windows.request";
    public const string WebRequestFile = "Builds/build-web.request";
    public const string AndroidRequestFile = "Builds/build-android.request";
    public const string IOSRequestFile = "Builds/build-ios.request";
    // Used only while the Android package name is unset or still the template's (whose "2D-URP" part
    // is not a valid Android package name). Change it in Player Settings before publishing to a store.
    private const string DefaultAndroidPackage = "com.bumperbeasts.game";
    private const string WindowsFolder = "Builds/Windows";
    private const string WebFolder = "Builds/Web";
    private const string IOSFolder = "Builds/iOS-Xcode";
    private const int ViewWidth = 540;
    private const int ViewHeight = 960;

    private static double nextRequestCheck;
    private static bool switchStarted;

    static BumperBeastBuild()
    {
        if (Application.isBatchMode)
            return;
        EditorApplication.update += WatchForRequest;
    }

    private static void WatchForRequest()
    {
        if (EditorApplication.timeSinceStartup < nextRequestCheck)
            return;
        nextRequestCheck = EditorApplication.timeSinceStartup + 2.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            return;

        if (File.Exists(RequestFile))
            HandleRequest(RequestFile, BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        else if (File.Exists(WebRequestFile))
            HandleRequest(WebRequestFile, BuildTargetGroup.WebGL, BuildTarget.WebGL);
        else if (File.Exists(AndroidRequestFile))
            HandleRequest(AndroidRequestFile, BuildTargetGroup.Android, BuildTarget.Android);
        else if (File.Exists(IOSRequestFile))
            HandleRequest(IOSRequestFile, BuildTargetGroup.iOS, BuildTarget.iOS);
    }

    // Builds once the editor is on the request's platform; otherwise starts the switch and keeps the
    // request, so the build runs after the switch and its script reload.
    private static void HandleRequest(string request, BuildTargetGroup group, BuildTarget target)
    {
        if (!BuildPipeline.IsBuildTargetSupported(group, target))
        {
            File.Delete(request);
            Debug.LogError("Bumper Beast: " + target + " build support is not installed for this Unity version " +
                "(Unity Hub > Installs > Add modules), or the editor was not restarted after installing it.");
            return;
        }
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            if (!switchStarted)
            {
                switchStarted = true;
                Debug.Log("Bumper Beast: switching the editor to " + target + " before building.");
                EditorUserBuildSettings.SwitchActiveBuildTargetAsync(group, target);
            }
            return;
        }

        File.Delete(request);
        string output = target == BuildTarget.WebGL ? BuildWeb()
            : target == BuildTarget.Android ? BuildAndroid()
            : target == BuildTarget.iOS ? BuildIOS()
            : BuildWindows();
        if (output != null)
            EditorUtility.RevealInFinder(output);
    }

    [MenuItem("Bumper Beast/Build Windows (Downloadable)")]
    public static void BuildWindowsFromMenu()
    {
        QueueRequest(RequestFile);
    }

    [MenuItem("Bumper Beast/Build Web (Play in Chrome)")]
    public static void BuildWebFromMenu()
    {
        QueueRequest(WebRequestFile);
    }

    [MenuItem("Bumper Beast/Build Android App (APK)")]
    public static void BuildAndroidFromMenu()
    {
        QueueRequest(AndroidRequestFile);
    }

    [MenuItem("Bumper Beast/Export iPhone + iPad (Xcode project)")]
    public static void BuildIOSFromMenu()
    {
        QueueRequest(IOSRequestFile);
    }

    private static void QueueRequest(string request)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Bumper Beast: leave Play Mode before building.");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(request));
        File.WriteAllText(request, "Requested from the menu.");
        nextRequestCheck = 0;
        Debug.Log("Bumper Beast: build queued (" + Path.GetFileName(request) + "); it starts in a moment.");
    }

    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(BuildWindows() != null ? 0 : 1);
    }

    public static void BuildWebFromCommandLine()
    {
        string zip = BuildWeb();
        SwitchBackToDesktop();
        EditorApplication.Exit(zip != null ? 0 : 1);
    }

    public static void BuildAndroidFromCommandLine()
    {
        string apk = BuildAndroid();
        SwitchBackToDesktop();
        EditorApplication.Exit(apk != null ? 0 : 1);
    }

    public static void BuildIOSFromCommandLine()
    {
        string zip = BuildIOS();
        SwitchBackToDesktop();
        EditorApplication.Exit(zip != null ? 0 : 1);
    }

    // The app id and version shared by the phone builds: the same id on Android and iOS, and the
    // iOS build number follows the Android version code so each upload is newer than the last.
    private static void ApplyPhoneIdentity()
    {
        string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (string.IsNullOrEmpty(package) || package.Contains("DefaultCompany") || package.Contains("-"))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DefaultAndroidPackage);
        string bundle = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
        if (string.IsNullOrEmpty(bundle) || bundle.Contains("DefaultCompany") || bundle.Contains("2D-URP"))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        PlayerSettings.iOS.buildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
    }

    // An Xcode project for iPhone and iPad, zipped for copying to a Mac. Returns the zip path, or
    // null when the build failed (the reason is logged).
    public static string BuildIOS()
    {
        StageWaveLayouts.ApplyPendingChanges();
        ApplyPhoneIdentity();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.requiresFullScreen = true;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.statusBarHidden = true;
        // A swipe up from the bottom edge (aiming pulls down!) should not send the game home on the
        // first try: iOS asks for a second swipe instead.
        PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        return BuildPackage(BuildTargetGroup.iOS, BuildTarget.iOS, IOSFolder, IOSFolder, "iOS-Xcode");
    }

    // Leaves the project on the desktop platform it is normally edited on.
    private static void SwitchBackToDesktop()
    {
        if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
    }

    // An installable app for Android phones (sideloaded APK, not a Play Store bundle). Returns the APK
    // path, or null when the build failed (the reason is logged).
    public static string BuildAndroid()
    {
        StageWaveLayouts.ApplyPendingChanges();
        ApplyPhoneIdentity();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        // 64-bit is required by current phones (many cannot run 32-bit apps at all).
        PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        string apk = "Builds/" + PlayerSettings.productName.Replace(' ', '-') + "-Android-v" +
            PlayerSettings.bundleVersion + ".apk";
        return BuildPackage(BuildTargetGroup.Android, BuildTarget.Android, null, apk, "Android");
    }

    // Returns the zip path, or null when the build failed (the reason is logged).
    public static string BuildWindows()
    {
        StageWaveLayouts.ApplyPendingChanges();
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = ViewWidth;
        PlayerSettings.defaultScreenHeight = ViewHeight;
        PlayerSettings.resizableWindow = false;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        return BuildPackage(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, WindowsFolder,
            WindowsFolder + "/" + PlayerSettings.productName + ".exe", "Windows");
    }

    // Returns the zip path, or null when the build failed (the reason is logged).
    public static string BuildWeb()
    {
        StageWaveLayouts.ApplyPendingChanges();
        PlayerSettings.defaultWebScreenWidth = ViewWidth;
        PlayerSettings.defaultWebScreenHeight = ViewHeight;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        // itch.io and most simple hosts do not send the Content-Encoding header compressed Unity files
        // need; with the fallback the loader decompresses them itself.
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        // Phones (iPhone Safari, Android Chrome) play this build on itch.io: no diagnostics overlay,
        // and the default template fills the phone's screen with the canvas.
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        return BuildPackage(BuildTargetGroup.WebGL, BuildTarget.WebGL, WebFolder, WebFolder, "Web");
    }

    // Builds into "folder" and zips it (Windows, Web), or, with no folder, builds the single file at
    // "location" (the Android APK). Returns the file to share, or null when the build failed.
    private static string BuildPackage(BuildTargetGroup group, BuildTarget target, string folder, string location, string label)
    {
        List<string> scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && File.Exists(scene.path))
                scenes.Add(scene.path);
        }
        if (scenes.Count == 0)
        {
            Debug.LogError("Bumper Beast: no enabled scenes in File > Build Profiles, so nothing was built.");
            return null;
        }

        if (folder != null)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);   // only ever holds the previous build made by this script
            Directory.CreateDirectory(folder);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(location));
            if (File.Exists(location))
                File.Delete(location);            // the previous APK made by this script
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = location,
            target = target,
            targetGroup = group,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Bumper Beast: " + label + " build " + summary.result + " with " + summary.totalErrors +
                " error(s); see the Console above for the first one.");
            return null;
        }

        string shared = location;
        if (target == BuildTarget.WebGL)
            BrandWebPage(folder);
        if (folder != null)
        {
            // Debug-symbol folders that must not ship with the game.
            foreach (string child in Directory.GetDirectories(folder))
            {
                string name = Path.GetFileName(child);
                if (name.EndsWith("_DoNotShip") || name.EndsWith("_ButDontShipItWithYourGame"))
                    Directory.Delete(child, true);
            }

            shared = "Builds/" + PlayerSettings.productName.Replace(' ', '-') + "-" + label + "-v" +
                PlayerSettings.bundleVersion + ".zip";
            if (File.Exists(shared))
                File.Delete(shared);
            ZipFile.CreateFromDirectory(folder, shared, System.IO.Compression.CompressionLevel.Optimal, false);
        }
        else if (!File.Exists(shared))
        {
            Debug.LogError("Bumper Beast: the " + label + " build reported success but " + shared + " was not written.");
            return null;
        }

        long bytes = new FileInfo(shared).Length;
        Debug.Log("Bumper Beast: " + label + " build ready (" + scenes.Count + " scenes, " +
            summary.totalTime.TotalSeconds.ToString("0") + " s): " + Path.GetFullPath(shared) + " (" +
            (bytes / (1024f * 1024f)).ToString("0.0") + " MB).");
        return shared;
    }

    // The Default web template titles the page "Unity Web Player | <product>" and shows Unity's logo
    // while loading. Use the game's name and icon instead; the loading background is already the
    // cover art (the launch-screen picture), so the bar moves low to keep the logo and Beasts visible.
    private static void BrandWebPage(string folder)
    {
        string product = PlayerSettings.productName;
        string index = Path.Combine(folder, "index.html");
        if (File.Exists(index))
        {
            string html = File.ReadAllText(index);
            html = html.Replace("<title>Unity Web Player | " + product + "</title>", "<title>" + product + "</title>");
            if (File.Exists(WebIconPath))
            {
                File.Copy(WebIconPath, Path.Combine(folder, "TemplateData", "icon.png"), true);
                html = html.Replace("TemplateData/favicon.ico", "TemplateData/icon.png");
                if (!html.Contains("apple-touch-icon"))
                    html = html.Replace("</head>", "    <link rel=\"apple-touch-icon\" href=\"TemplateData/icon.png\">\n  </head>");
            }
            File.WriteAllText(index, html);
        }
        string css = Path.Combine(folder, "TemplateData", "style.css");
        if (File.Exists(css) && !File.ReadAllText(css).Contains("Bumper Beast"))
            File.AppendAllText(css, "\n/* Bumper Beast: no Unity logo; the loading bar sits low so the cover art shows. */\n" +
                "#unity-logo, #unity-logo-title-footer { display: none }\n" +
                "#unity-loading-bar { top: auto; bottom: 9%; transform: translate(-50%, 0) }\n");
    }

    // App icon on every platform, then the launch screen. Unity 6 Android builds ignore the default
    // icon and use their own Legacy / Round / Adaptive slots, so those are filled explicitly (an
    // empty set left the phone showing Unity's logo).
    private static void ApplyIcon()
    {
        Texture2D icon = LoadIconTexture(IconPath);
        if (icon == null)
        {
            Debug.LogWarning("Bumper Beast: " + IconPath + " is missing, so the build keeps the default Unity icon.");
        }
        else
        {
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            Texture2D adaptiveBack = LoadIconTexture(AdaptiveBackPath);
            Texture2D adaptiveFront = LoadIconTexture(AdaptiveFrontPath);
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                bool adaptive = kind.ToString().Contains("Adaptive");
                foreach (PlatformIcon slot in icons)
                {
                    if (adaptive && adaptiveBack != null && adaptiveFront != null && slot.maxLayerCount >= 2)
                        slot.SetTextures(adaptiveBack, adaptiveFront);   // layer 0 background, 1 foreground
                    else if (adaptive)
                        slot.SetTextures(icon, icon);
                    else
                        slot.SetTexture(icon, 0);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
        }
        ApplySplash();
    }

    private static void ApplySplash()
    {
        Sprite cover = LoadSprite(SplashPath);
        if (cover == null)
        {
            Debug.LogWarning("Bumper Beast: " + SplashPath + " is missing, so the launch screen is unchanged.");
            return;
        }
        Sprite spacer = LoadSprite(SplashSpacerPath);
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.background = cover;
        PlayerSettings.SplashScreen.backgroundPortrait = cover;
        PlayerSettings.SplashScreen.backgroundColor = new Color32(56, 150, 228, 255);
        PlayerSettings.SplashScreen.overlayOpacity = 0f;
        PlayerSettings.SplashScreen.blurBackgroundImage = false;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.logos = spacer != null
            ? new[] { PlayerSettings.SplashScreenLogo.Create(SplashSeconds, spacer) }
            : new PlayerSettings.SplashScreenLogo[0];
        if (PlayerSettings.SplashScreen.showUnityLogo)
            Debug.LogWarning("Bumper Beast: this Unity license keeps the Unity logo on the launch screen.");
    }

    // Icons are read at full quality: no compression, no mipmaps, no power-of-two resize.
    private static Texture2D LoadIconTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return null;
        if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled ||
            importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency || importer.maxTextureSize < 1024)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, 1024);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
            importer.maxTextureSize < 2048)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, 2048);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
