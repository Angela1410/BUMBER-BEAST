using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// this script builds the versions of the game we share (they go in the Builds folder):
// - Windows: Builds/Windows/<game>.exe, zipped as Builds/<game>-Windows-v<version>.zip
// - Web (plays in Chrome, Safari, etc.): Builds/Web/index.html, zipped with index.html at the top like itch.io wants
// - Android: Builds/<game>-Android-v<version>.apk (64-bit, you install it straight on the phone)
// - iPhone/iPad: an Xcode project zipped in Builds/. you need a Mac with Xcode to turn it into an app,
// so for now iPhone players use the web version in Safari
// before building it runs the pending stage setup (StageWaveLayouts), sets the app icon and makes
// the window portrait (540 x 960) instead of the default landscape size.
// how to start a build:
// - from the Bumper Beast menu in Unity
// - or make a file called build-windows.request / build-web.request / build-android.request in Builds/
// while the editor is open (it switches platform if needed and deletes the file when it starts)
// - or from the command line with the editor closed:
// Unity.exe -batchmode -projectPath <project> -buildTarget Android -executeMethod BumperBeastBuild.BuildAndroidFromCommandLine
// (use BuildWebFromCommandLine with -buildTarget WebGL, or BuildFromCommandLine for Windows)
[InitializeOnLoad]
public static class BumperBeastBuild
{
    // app icon: a square crop of the cover art. android 8+ cuts "adaptive" icons down to the middle 2/3,
    // so that layer uses the same crop sized to fit that middle part
    public const string IconPath = "Assets/Sprites/AppIconCover.png";
    private const string AdaptiveBackPath = "Assets/Sprites/AppIconAdaptiveBack.png";
    private const string AdaptiveFrontPath = "Assets/Sprites/AppIconAdaptiveFront.png";
    // launch screen: our cover art instead of the "Made with Unity" logo (Unity 6 lets us turn it off).
    // the invisible spacer "logo" just keeps the picture up for SplashSeconds
    private const string SplashPath = "Assets/Sprites/SplashCover.png";
    private const string SplashSpacerPath = "Assets/Sprites/SplashSpacer.png";
    private const float SplashSeconds = 2f;
    // icon for the browser tab and the iPhone home screen (web version only, so it's outside Assets)
    private const string WebIconPath = "BuildAssets/web-icon.png";
    public const string RequestFile = "Builds/build-windows.request";
    public const string WebRequestFile = "Builds/build-web.request";
    public const string AndroidRequestFile = "Builds/build-android.request";
    public const string IOSRequestFile = "Builds/build-ios.request";
    // only used if the android package name is empty or still the template one ("2D-URP" isn't allowed
    // on android). change it in Player Settings before putting the game on a store
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

    // if the editor is already on the right platform it builds now. if not, it switches first and keeps
    // the request file, so the build runs after the switch
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

    // app id and version for the phone builds. android and iOS use the same id, and the iOS build number
    // copies the android version code so every new upload counts as newer
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

    // makes an Xcode project for iPhone and iPad and zips it so we can copy it to a Mac.
    // gives back the zip path, or null if the build failed (the reason goes in the log)
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
        // aiming means dragging down, so a swipe up from the bottom shouldn't close the game right away.
        // with this iOS asks for a second swipe first
        PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        return BuildPackage(BuildTargetGroup.iOS, BuildTarget.iOS, IOSFolder, IOSFolder, "iOS-Xcode");
    }

    // puts the project back on the PC platform we normally work on
    private static void SwitchBackToDesktop()
    {
        if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
    }

    // builds the android app (an APK you install directly, not a Play Store bundle).
    // gives back the APK path, or null if the build failed (the reason goes in the log)
    public static string BuildAndroid()
    {
        StageWaveLayouts.ApplyPendingChanges();
        ApplyPhoneIdentity();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        // phones need 64-bit now (a lot of them can't run 32-bit apps at all)
        PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        string apk = "Builds/" + PlayerSettings.productName.Replace(' ', '-') + "-Android-v" +
            PlayerSettings.bundleVersion + ".apk";
        return BuildPackage(BuildTargetGroup.Android, BuildTarget.Android, null, apk, "Android");
    }

    // gives back the zip path, or null if the build failed (the reason goes in the log)
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

    // gives back the zip path, or null if the build failed (the reason goes in the log)
    public static string BuildWeb()
    {
        StageWaveLayouts.ApplyPendingChanges();
        PlayerSettings.defaultWebScreenWidth = ViewWidth;
        PlayerSettings.defaultWebScreenHeight = ViewHeight;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        // itch.io doesn't send the Content-Encoding header that compressed Unity files need,
        // so with this the loader unzips the files by itself
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        // phones play this version on itch.io (iPhone Safari, Android Chrome), so no debug overlay,
        // and the default template makes the game fill the whole phone screen
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        ApplyIcon();
        AssetDatabase.SaveAssets();

        return BuildPackage(BuildTargetGroup.WebGL, BuildTarget.WebGL, WebFolder, WebFolder, "Web");
    }

    // builds into "folder" and zips it (Windows, Web), or if there's no folder it builds one file
    // at "location" (the android APK). gives back the file to share, or null if it failed
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
                Directory.Delete(folder, true); // this folder only has the last build this script made
            Directory.CreateDirectory(folder);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(location));
            if (File.Exists(location))
                File.Delete(location); // the last APK this script made
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
            // debug symbol folders, we don't ship these with the game
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

    // the default web page is titled "Unity Web Player | <game>" and shows Unity's logo while loading.
    // we use our own name and icon instead. the loading background is already our cover art,
    // so we move the loading bar down to keep the logo and the Beasts visible
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

    // sets the app icon on every platform, then the launch screen. Unity 6 android ignores the default
    // icon and uses its own Legacy / Round / Adaptive slots, so we fill those ourselves
    // (when they were empty the phone showed Unity's logo)
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
                        slot.SetTextures(adaptiveBack, adaptiveFront); // layer 0 is the background, layer 1 is the front
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

    // load the icons at full quality: no compression, no mipmaps, no power-of-two resize
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
