using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Setup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    // Runs after every script reload: configures URP once, the first time the package is present.
    [InitializeOnLoadMethod]
    static void EnsureUrp()
    {
        if (GraphicsSettings.defaultRenderPipeline == null) EditorApplication.delayCall += SetupUrp;
    }

    [MenuItem("Shelf Sort/Setup URP")]
    public static void SetupUrp()
    {
        if (GraphicsSettings.defaultRenderPipeline != null) return;
        Directory.CreateDirectory("Assets/Settings");
        var asset = UniversalRenderPipelineAsset.Create();
        AssetDatabase.CreateAsset(asset, "Assets/Settings/URP.asset");
        asset.LoadBuiltinRendererData(); // writes Assets/UniversalRenderer.asset, with post-processing data
        asset.msaaSampleCount = 4;
        asset.shadowDistance = 25;
        asset.supportsHDR = true;
        EditorUtility.SetDirty(asset);
        GraphicsSettings.defaultRenderPipeline = asset;
        AssetDatabase.SaveAssets();
        Debug.Log("Shelf Sort: URP configured");
    }

    // Creates the single scene, registers it for builds and sets mobile player settings.
    [MenuItem("Shelf Sort/Setup Project")]
    public static void Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        new GameObject("Game").AddComponent<Game>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.productName = "Shelf Sort";
        PlayerSettings.companyName = "Barredo";
        // Bundle id is permanent once on the App Store — change it before the first upload if needed
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.barredo.shelfsort");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.barredo.shelfsort");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        AssetDatabase.SaveAssets();
    }

    // Batch check: every generated level holds exactly 3 of each item and has a free slot.
    [MenuItem("Shelf Sort/Check Levels")]
    public static void CheckLevels()
    {
        var rng = new System.Random(1);
        for (int n = 1; n <= 200; n++)
        {
            var lv = LevelGen.Make(n, rng);
            var items = lv.Shelves.SelectMany(s => s).SelectMany(r => r).Where(x => x >= 0);
            if (items.GroupBy(x => x).Any(g => g.Count() != 3)) throw new System.Exception($"Level {n}: bad item counts");
            if (lv.Shelves.All(s => s[0].All(x => x >= 0))) throw new System.Exception($"Level {n}: no free slot");
        }
        Debug.Log("CheckLevels OK (200 levels)");
    }
}
