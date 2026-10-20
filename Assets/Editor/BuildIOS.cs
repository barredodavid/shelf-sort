using UnityEditor;

// Exports the Xcode project to build/ios (codemagic.yaml runs this in batch mode, then Xcode builds the .ipa)
public static class BuildIOS
{
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "build/ios",
            target = BuildTarget.iOS,
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
