using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// The app's icon is Assets/AppIcon.png (1024x1024, opaque, as Apple wants). After every script reload, this makes sure
// it is the default icon in Player Settings (used for every size and platform), so replacing the PNG is enough.
public static class AppIcon
{
    const string PngPath = "Assets/AppIcon.png";

    [InitializeOnLoadMethod]
    static void Ensure() => EditorApplication.delayCall += Apply;

    [MenuItem("Shelf Sort/Apply App Icon")]
    public static void Apply()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
        if (!icon) return;
        var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        if (current.Length == 1 && current[0] == icon) return;
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log("App icon set from " + PngPath);
    }
}
