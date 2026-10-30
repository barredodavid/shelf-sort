using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// iOS build settings for the App Store, applied when the iOS build is made (Unity Build Automation calls PreExport,
// set as the build target's "Pre-Export method": BuildIOS.PreExport):
//  - a build number that always goes up (the date and time), since App Store Connect refuses one already uploaded;
//  - iPhone only, so the store asks for no iPad screenshots;
//  - Info.plist says the app uses no encryption beyond Apple's, so TestFlight doesn't hold each build on that question;
//  - GameAnalytics' keys, from the build target's GA_GAME_KEY / GA_SECRET_KEY variables.
public class BuildIOS : IPostprocessBuildWithReport
{
    public static void PreExport()
    {
        PlayerSettings.iOS.buildNumber = System.DateTime.UtcNow.ToString("yyMMdd.HHmm"); // e.g. 261021.1435: two numbers, always rising
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        Setup.SetGameAnalyticsKeys();
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

    // a local Windows player, for checking player-only behaviour without spending cloud build minutes: build/win
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "build/win/ShelfSort.exe",
            target = BuildTarget.StandaloneWindows64,
        });
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // the web build for the portfolio, the game in a phone-shaped frame: build/webgl. The default template pins the
    // canvas at 960x600 on desktop; it fills its frame instead.
    public static void BuildWebGL()
    {
        const string dir = "build/webgl";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = dir,
            target = BuildTarget.WebGL,
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
            return;
        }
        string index = $"{dir}/index.html", css = $"{dir}/TemplateData/style.css";
        System.IO.File.WriteAllText(index, System.IO.File.ReadAllText(index)
            .Replace("canvas.style.width = \"960px\";", "canvas.style.width = \"100%\";")
            .Replace("canvas.style.height = \"600px\";", "canvas.style.height = \"100%\";"));
        System.IO.File.WriteAllText(css, System.IO.File.ReadAllText(css)
            .Replace("#unity-container.unity-desktop { left: 50%; top: 50%; transform: translate(-50%, -50%) }",
                     "#unity-container.unity-desktop { position: fixed; width: 100%; height: 100% }")
            .Replace("#unity-canvas { background: #231F20 }", "#unity-canvas { background: #231F20; width: 100%; height: 100% }"));
    }

    // App Store preview: the frames a player saved with -record <dir> (see Game.Record) into <dir>.mp4, H.264 at 30 fps
    // usage: Unity -batchmode -quit -executeMethod BuildIOS.EncodePreview -frames <dir>
    public static void EncodePreview()
    {
        var args = System.Environment.GetCommandLineArgs();
        string dir = args[System.Array.IndexOf(args, "-frames") + 1];
        var files = System.IO.Directory.GetFiles(dir, "*.png");
        System.Array.Sort(files);
        var png = new Texture2D(2, 2); // LoadImage gives ARGB32; the encoder takes RGBA32 only
        png.LoadImage(System.IO.File.ReadAllBytes(files[0]));
        var tex = new Texture2D(png.width, png.height, TextureFormat.RGBA32, false);
        var video = new UnityEditor.Media.VideoTrackAttributes
        {
            frameRate = new UnityEditor.Media.MediaRational(30),
            width = (uint)tex.width, height = (uint)tex.height,
            bitRateMode = VideoBitrateMode.High,
        };
        // App Store Connect wants an audio track: a synthesized 120 bpm loop, whose beats fall on the 15-frame cuts
        const int Rate = 48000, PerFrame = Rate / 30;
        var audio = new UnityEditor.Media.AudioTrackAttributes
        {
            sampleRate = new UnityEditor.Media.MediaRational(Rate), channelCount = 2, language = "fr",
        };
        var music = Music(files.Length * PerFrame, Rate);
        using var encoder = new UnityEditor.Media.MediaEncoder(dir.TrimEnd('/', '\\') + ".mp4", video, audio);
        using var chunk = new Unity.Collections.NativeArray<float>(PerFrame * 2, Unity.Collections.Allocator.Persistent);
        var stereo = new float[PerFrame * 2];
        for (int f = 0; f < files.Length; f++)
        {
            png.LoadImage(System.IO.File.ReadAllBytes(files[f]));
            tex.SetPixels32(png.GetPixels32());
            tex.Apply();
            encoder.AddFrame(tex);
            for (int i = 0; i < PerFrame; i++) stereo[2 * i] = stereo[2 * i + 1] = music[f * PerFrame + i];
            chunk.CopyFrom(stereo);
            encoder.AddSamples(chunk);
        }
    }

    // Upbeat pop loop: kick on every beat, clap on 2 and 4, off-beat hats, eighth-note bass and a sixteenth-note
    // arpeggio over C - G - Am - F, one chord a bar; the last bar rings out on C.
    static float[] Music(int n, int rate)
    {
        float[][] chords = { new[] { 523.25f, 659.25f, 783.99f }, new[] { 392f, 493.88f, 587.33f }, new[] { 440f, 523.25f, 659.25f }, new[] { 349.23f, 440f, 523.25f } };
        float[] roots = { 65.41f, 49f, 55f, 43.65f };
        const float Beat = .5f;
        var rnd = new System.Random(1);
        var o = new float[n];
        float end = (float)n / rate;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate, b = t / Beat;
            int beat = (int)b, bar = beat / 4, ch = bar % 4;
            bool last = t > end - 4 * Beat;
            if (last) ch = 0;
            float tb = (b - beat) * Beat, t8 = (b * 2 - Mathf.Floor(b * 2)) * Beat / 2, t16 = (b * 4 - Mathf.Floor(b * 4)) * Beat / 4;
            float noise = (float)rnd.NextDouble() * 2 - 1, s = 0;
            if (!last || beat == (int)((end - 4 * Beat) / Beat))
            {
                s += .9f * Mathf.Sin(2 * Mathf.PI * (45 * tb + 75 * (1 - Mathf.Exp(-tb * 25)) / 25)) * Mathf.Exp(-tb * 14); // kick
                if (beat % 2 == 1) s += .28f * noise * Mathf.Exp(-tb * 22); // clap
                if ((int)(b * 2) % 2 == 1) s += .1f * noise * Mathf.Exp(-t8 * 70); // hat
                s += .2f * Mathf.Clamp(3 * Mathf.Sin(2 * Mathf.PI * roots[ch] * 2 * t), -1, 1) * Mathf.Exp(-t8 * 6); // bass
            }
            float note = chords[ch][(int)(b * 4) % 3] * ((int)(b * 4) % 6 >= 3 ? 2 : 1);
            float arp = 2 * Mathf.Abs(2 * (note * t - Mathf.Floor(note * t + .5f))) - 1; // triangle
            s += .13f * arp * (last ? Mathf.Exp(-(t - (end - 4 * Beat)) * 1.2f) : Mathf.Exp(-t16 * 12));
            if (last) foreach (var f in chords[0]) s += .07f * Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-(t - (end - 4 * Beat)) * .8f);
            o[i] = (float)System.Math.Tanh(s * 1.1f) * .8f * Mathf.Clamp01((end - t) * 3); // soft clip, short fade at the end
        }
        return o;
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
