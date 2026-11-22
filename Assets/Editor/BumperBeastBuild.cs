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
    public const string IconPath = "Assets/Sprites/AppIcon.png";
    public const string RequestFile = "Builds/build-windows.request";
    public const string WebRequestFile = "Builds/build-web.request";
    public const string AndroidRequestFile = "Builds/build-android.request";
    // Used only while the Android package name is unset or still the template's (whose "2D-URP" part
    // is not a valid Android package name). Change it in Player Settings before publishing to a store.
    private const string DefaultAndroidPackage = "com.bumperbeasts.game";
    private const string WindowsFolder = "Builds/Windows";
    private const string WebFolder = "Builds/Web";
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
        string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (string.IsNullOrEmpty(package) || package.Contains("DefaultCompany") || package.Contains("-"))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DefaultAndroidPackage);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        // 64-bit is required by current phones (many cannot run 32-bit apps at all).
        PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
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

    private static void ApplyIcon()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
            Debug.LogWarning("Bumper Beast: " + IconPath + " is missing, so the build keeps the default Unity icon.");
        else
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
    }
}
