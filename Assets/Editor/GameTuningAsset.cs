#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// makes Assets/Resources/GameTuning.asset (all the gameplay tuning numbers) if it's missing, and
// saves it once more when GameTuning gets new settings, so they show up in the file too.
// it runs by itself after every script reload (StageWaveLayouts.ApplyPendingChanges calls it)
// and before every build, so there's no menu step
public static class GameTuningAsset
{
    public const string AssetPath = "Assets/Resources/GameTuning.asset";

    // change this when GameTuning gets new settings, so the file gets saved once with them.
    // values already in the file are kept, new settings start at the numbers written in GameTuning
    // 2: v1.4 settings (screen fit, bounce power, recall, return, power meter, waves, aiming, idle)
    private const int FileVersion = 2;

    public static void Ensure()
    {
        GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetPath);
        if (tuning == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            tuning.fileVersion = FileVersion;
            AssetDatabase.CreateAsset(tuning, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Bumper Beast: made " + AssetPath + ". Click it to change the gameplay numbers.");
            return;
        }

        if (tuning.fileVersion < FileVersion)
        {
            tuning.fileVersion = FileVersion;
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssetIfDirty(tuning);
        }
    }
}
#endif
