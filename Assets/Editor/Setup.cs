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
        if (!AssetDatabase.LoadAssetAtPath<Material>(IcePath)) EditorApplication.delayCall += CreateIceMaterial;
        if (!AssetDatabase.LoadAssetAtPath<Material>(LitPath)) EditorApplication.delayCall += CreateBaseMaterials;
    }

    const string LitPath = "Assets/Resources/Lit.mat", UnlitPath = "Assets/Resources/Unlit.mat";

    // Fx.Lit / Fx.Unlit: every runtime material is copied from these. The pipeline's defaultMaterial is null in a
    // player, so they have to ship as assets (which also keeps their shader variants).
    [MenuItem("Shelf Sort/Create Base Materials")]
    public static void CreateBaseMaterials()
    {
        if (!AssetDatabase.LoadAssetAtPath<Material>(LitPath))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")), LitPath);
        if (!AssetDatabase.LoadAssetAtPath<Material>(UnlitPath))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")), UnlitPath);
        AssetDatabase.SaveAssets();
    }

    const string IcePath = "Assets/Resources/Ice.mat";

    // Translucent URP Lit for ice blocks. It has to exist as an asset: a transparent Lit set up only at runtime
    // would lose its shader variant to build stripping and render opaque on device.
    [MenuItem("Shelf Sort/Create Ice Material")]
    public static void CreateIceMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(IcePath)) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1); // transparent
        m.SetFloat("_Blend", 0); // alpha blending
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(.72f, .9f, 1f, .42f));
        m.SetFloat("_Smoothness", .92f);
        AssetDatabase.CreateAsset(m, IcePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Shelf Sort: ice material created");
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

    // Check: every baked level holds exactly 3 of each item, has a free slot and can be cleared.
    [MenuItem("Shelf Sort/Check Levels")]
    public static void CheckLevels()
    {
        var rng = new System.Random(1);
        for (int n = 1; n <= 200; n++)
        {
            var lv = LevelGen.Make(n, rng);
            var items = lv.Shelves.SelectMany(s => s).SelectMany(r => r).Where(LevelGen.IsProduct);
            if (items.GroupBy(LevelGen.Id).Any(g => g.Count() != 3)) throw new System.Exception($"Level {n}: bad item counts");
            if (lv.Shelves.All(s => s[0].All(x => x >= 0))) throw new System.Exception($"Level {n}: no free slot");
            if (!LevelGen.Solvable(lv.Shelves)) throw new System.Exception($"Level {n}: not solvable");
        }
        Debug.Log("CheckLevels OK (200 levels)");
    }
}
