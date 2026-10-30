using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ad Capture > Record: plays Assets/AdCapture/AdCapture.unity and films it with Unity Recorder, MP4 1080x1920
// at 30 fps, into AdCapture/Output/<scenario>.mp4 (next to Assets, ignored by git). The AdDirector leaves play mode
// once its end card is over, which stops the recording. Record All films every scenario of Assets/AdCapture in turn.
[InitializeOnLoad]
static class AdCaptureMenu
{
    const string ScenePath = "Assets/AdCapture/AdCapture.unity", Queue = "AdCapture.queue", Quit = "AdCapture.quit", Resume = "AdCapture.resume";
    static RecorderController recorder;

    static AdCaptureMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        // scripts changed during a take recompile after it, and the reload drops the delayCall that films the next one
        if (SessionState.GetBool(Resume, false)) EditorApplication.delayCall += Next;
    }

    [MenuItem("Tools/Ad Capture/Record")]
    static void Record()
    {
        if (!OpenScene()) return;
        var director = Object.FindAnyObjectByType<AdDirector>();
        if (!director || !director.scenario)
        {
            Debug.LogError("Ad Capture: pick a scenario on the AdDirector of AdCapture.unity");
            return;
        }
        Start(AssetDatabase.GetAssetPath(director.scenario));
    }

    [MenuItem("Tools/Ad Capture/Record All")]
    static void RecordAll()
    {
        if (!OpenScene()) return;
        Start(AssetDatabase.FindAssets("t:AdScenario", new[] { "Assets/AdCapture" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray());
    }

    // command line: Unity -projectPath . -executeMethod AdCaptureMenu.RecordAllAndQuit (not -batchmode: it films the Game view)
    static void RecordAllAndQuit()
    {
        SessionState.SetBool(Quit, true);
        RecordAll();
    }

    static bool OpenScene()
    {
        if (EditorApplication.isPlaying) return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        return true;
    }

    static void Start(params string[] scenarios)
    {
        SessionState.SetString(Queue, string.Join("\n", scenarios));
        Next();
    }

    // the next scenario of the queue, in a play mode of its own
    static void Next()
    {
        SessionState.EraseBool(Resume);
        var queue = SessionState.GetString(Queue, "").Split('\n').Where(p => p != "").ToList();
        if (queue.Count == 0)
        {
            Debug.Log($"Ad Capture: videos in {Path.GetFullPath("AdCapture/Output")}");
            if (SessionState.GetBool(Quit, false)) EditorApplication.Exit(0);
            return;
        }
        SessionState.SetString(Queue, string.Join("\n", queue.Skip(1)));
        SessionState.SetString(AdDirector.RecordKey, queue[0]);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        string scenario = SessionState.GetString(AdDirector.RecordKey, "");
        if (scenario == "") return;
        switch (change)
        {
            case PlayModeStateChange.EnteredPlayMode:
                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = "Ad";
                movie.Enabled = true;
                movie.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                };
                movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1080, OutputHeight = 1920 };
                movie.AudioInputSettings.PreserveAudio = true;
                movie.OutputFile = Path.GetFullPath($"AdCapture/Output/{Path.GetFileNameWithoutExtension(scenario)}"); // the Recorder adds .mp4
                var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                settings.AddRecorderSettings(movie);
                settings.SetRecordModeToManual();
                settings.FrameRatePlayback = FrameRatePlayback.Constant; // game time steps 1/30 s a frame, however long a frame takes
                settings.FrameRate = 30;
                settings.CapFrameRate = true;
                recorder = new RecorderController(settings);
                recorder.PrepareRecording();
                recorder.StartRecording();
                break;
            case PlayModeStateChange.ExitingPlayMode:
                if (recorder != null && recorder.IsRecording()) recorder.StopRecording();
                recorder = null;
                break;
            case PlayModeStateChange.EnteredEditMode:
                SessionState.EraseString(AdDirector.RecordKey);
                SessionState.SetBool(Resume, true);
                EditorApplication.delayCall += Next;
                break;
        }
    }
}
