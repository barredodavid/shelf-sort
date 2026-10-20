using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// iOS build settings for the App Store, applied when the iOS build is made (Unity Build Automation calls PreExport,
// set as the build target's "Pre-Export method": BuildIOS.PreExport):
//  - a build number that always goes up (the date and time), since App Store Connect refuses one already uploaded;
//  - iPhone only, so the store asks for no iPad screenshots;
//  - Info.plist says the app uses no encryption beyond Apple's, so TestFlight doesn't hold each build on that question.
public class BuildIOS : IPostprocessBuildWithReport
{
    public static void PreExport()
    {
        PlayerSettings.iOS.buildNumber = System.DateTime.UtcNow.ToString("yyMMdd.HHmm"); // e.g. 261021.1435: two numbers, always rising
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
    }

    // a local export, for testing: build/ios
    public static void Build()
    {
        PreExport();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "build/ios",
            target = BuildTarget.iOS,
        });
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
#if UNITY_IOS
        if (report.summary.platform != BuildTarget.iOS) return;
        string path = report.summary.outputPath + "/Info.plist";
        var plist = new UnityEditor.iOS.Xcode.PlistDocument();
        plist.ReadFromFile(path);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(path);
#endif
    }
}
