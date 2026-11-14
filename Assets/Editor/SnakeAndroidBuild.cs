using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android: the app icon and APK builds.
///
/// Tools > Snake > Apply App Icon: puts the icons in Assets/Icons (drawn by Art/make_app_icon.py)
///   into Player Settings: adaptive layers, a round and a legacy icon, and the default icon.
/// Tools > Snake > Build APK: apks/snake.apk (release), icons applied first.
///
/// Without the editor (Unity closed):
///   Unity -batchmode -nographics -projectPath . -executeMethod SnakeAndroidBuild.CommandLine -logFile Logs/android-build.log
/// </summary>
public static class SnakeAndroidBuild
{
    public const string ApkPath = "apks/snake.apk";
    const string IconFolder = "Assets/Icons/";

    [MenuItem("Tools/Snake/Apply App Icon")]
    public static void ApplyIcons()
    {
        var legacy = LoadIcon("AppIcon.png");
        var round = LoadIcon("AppIcon_Round.png");
        var background = LoadIcon("AppIcon_Background.png");
        var foreground = LoadIcon("AppIcon_Foreground.png");
        if (legacy == null || round == null || background == null || foreground == null)
        {
            Debug.LogError($"App icon PNGs missing from {IconFolder}; run Art/make_app_icon.py");
            return;
        }

        var android = NamedBuildTarget.Android;
        SetAllSizes(android, AndroidPlatformIconKind.Legacy, legacy);
        SetAllSizes(android, AndroidPlatformIconKind.Round, round);
        var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
        foreach (var icon in adaptive) icon.SetTextures(background, foreground);
        PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log("Snake: app icon applied.");
    }

    [MenuItem("Tools/Snake/Build APK")]
    static void MenuBuild() => Build();

    public static bool Build(string path = ApkPath)
    {
        ApplyIcons();
        EditorUserBuildSettings.buildAppBundle = false;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        });
        var s = report.summary;
        if (s.result == BuildResult.Succeeded)
        {
            Debug.Log($"Snake: built {path} ({new FileInfo(path).Length / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0} s).");
            return true;
        }
        Debug.LogError($"Snake: Android build {s.result} with {s.totalErrors} errors.");
        return false;
    }

    /// <summary>Batch mode entry point. Switches back to the previous build target; exits 0 when the APK built.</summary>
    public static void CommandLine()
    {
        var previous = EditorUserBuildSettings.activeBuildTarget;
        var previousGroup = BuildPipeline.GetBuildTargetGroup(previous);
        bool ok;
        try { ok = Build(); }
        catch (Exception e) { Debug.LogException(e); ok = false; }
        if (previous != BuildTarget.Android && EditorUserBuildSettings.activeBuildTarget != previous)
            EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previous);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void SetAllSizes(NamedBuildTarget target, PlatformIconKind kind, Texture2D texture)
    {
        var icons = PlayerSettings.GetPlatformIcons(target, kind);
        foreach (var icon in icons) icon.SetTexture(texture);
        PlayerSettings.SetPlatformIcons(target, kind, icons);
    }

    /// <summary>Loads an icon PNG, importing it as a plain uncompressed texture (no sprite, no mipmaps).</summary>
    static Texture2D LoadIcon(string file)
    {
        string path = IconFolder + file;
        if (!File.Exists(path)) return null;
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            AssetDatabase.ImportAsset(path);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer != null && (importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                                 importer.npotScale != TextureImporterNPOTScale.None ||
                                 importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
