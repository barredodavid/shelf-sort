#if UNITY_EDITOR // ad capture only: nothing of it goes into a player build (and AdCapture.unity is not in the Build Settings)
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Films an ad: a fake finger plays an AdScenario's moves on its level, then an end card shows. The real Game runs the
// level (rules, views, tweens, confetti, sounds) but with its Update and OnGUI off: no real input, no clock, no
// country twist of its own. This drives them instead, through the Ad* members below (an editor-only part of Game).
// Tools > Ad Capture > Record films it with Unity Recorder (Editor/AdCaptureMenu.cs).
public class AdDirector : MonoBehaviour
{
    public AdScenario scenario;
    [Tooltip("Texts in English: sets the game's \"lang\" setting while playing, and puts it back after")]
    public bool english = true;
    [Header("Fake finger")]
    [Tooltip("Image of the hand (a PNG with transparency, set to Sprite or Default); empty: the hand drawn by code")]
    public Texture2D hand;
    [Tooltip("Where the fingertip is in that image: 0..1 from its top-left corner")]
    public Vector2 handTip = new(.42f, .03f);
    public float handSize = 280, handAngle = -18;

    public const string RecordKey = "AdCapture.recording"; // SessionState: the scenario being filmed, set by AdCaptureMenu
    const float EndCardTime = 2;

    // read by Game.AdGui
    [System.NonSerialized] public Vector3 finger, tapAt; // world points: under the fingertip, where it last tapped
    [System.NonSerialized] public bool fingerOn, pressed;
    [System.NonSerialized] public float tappedAt = -9, outroAt = -1, endAt = -1;

    Game game;
    float zoom = 1, zoomTo = 1;
    string langBefore;

    // a move of the scenario: t < 0 a tap only, j < 0 the first empty slot, wait < 0 the scenario's delay
    struct Step { public int s, i, t, j; public float wait; }

    static List<Step> Parse(string text) => text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Select(w =>
    {
        var at = w.Split('@');
        var ends = at[0].Split('>');
        var a = ends[0].Split('.');
        var b = ends.Length > 1 ? ends[1].Split('.') : null;
        return new Step
        {
            s = int.Parse(a[0]), i = int.Parse(a[1]),
            t = b == null ? -1 : int.Parse(b[0]), j = b is { Length: > 1 } ? int.Parse(b[1]) : -1,
            wait = at.Length > 1 ? float.Parse(at[1], CultureInfo.InvariantCulture) : -1,
        };
    }).ToList();

    IEnumerator Start()
    {
        string rec = SessionState.GetString(RecordKey, "");
        if (rec != "") scenario = AssetDatabase.LoadAssetAtPath<AdScenario>(rec);
        if (!scenario)
        {
            Debug.LogError("Ad Capture: no scenario on the AdDirector");
            yield break;
        }
        langBefore = PlayerPrefs.HasKey("lang") ? PlayerPrefs.GetString("lang") : null;
        if (english) PlayerPrefs.SetString("lang", "en");
        game = gameObject.AddComponent<Game>();
        game.enabled = false; // before its Start: AdStart calls that one
        game.AdStart(scenario);
        int frame0 = Time.frameCount;

        var steps = Parse(scenario.moves);
        finger = game.AdAim(steps[0].s, steps[0].i) + new Vector3(1.5f, -3, 0); // comes in from below
        yield return new WaitForSeconds(1);
        fingerOn = true;
        for (int n = 0; n < steps.Count; n++)
        {
            yield return Play(steps[n], n == steps.Count - 1);
            zoomTo = Mathf.Lerp(1, scenario.zoom, (n + 1f) / steps.Count);
            if (n + 1 < steps.Count) yield return new WaitForSeconds(steps[n + 1].wait >= 0 ? steps[n + 1].wait : scenario.delay);
        }
        while (game.AdBusy) yield return null;
        Time.timeScale = 1;
        if (game.AdCleared) yield return game.AdWin();
        yield return new WaitForSeconds(1.2f);
        if (scenario.outro != "")
        {
            outroAt = Time.time;
            Fx.Play("lose");
            yield return new WaitForSeconds(1.8f);
        }
        fingerOn = false;
        endAt = Time.time;
        Fx.Play("match");
        yield return new WaitForSeconds(EndCardTime);
        if (rec != "") Debug.Log($"Ad Capture: {scenario.name}, {(Time.frameCount - frame0) / 30f:F1} s of video"); // the Recorder steps 1/30 s a frame
        if (rec != "") EditorApplication.isPlaying = false; // AdCaptureMenu stops the Recorder on the way out
    }

    IEnumerator Play(Step st, bool last)
    {
        yield return Glide(game.AdAim(st.s, st.i), .4f, false);
        pressed = true;
        tapAt = finger;
        tappedAt = Time.time;
        yield return new WaitForSeconds(.1f);
        if (st.t < 0)
        {
            game.AdTap(st.s, st.i);
            yield return new WaitForSeconds(.15f);
            pressed = false;
            yield break;
        }
        if (!game.AdPick(st.s, st.i))
        {
            Debug.LogError($"Ad Capture: {scenario.name}: nothing to pick up at {st.s}.{st.i}");
            pressed = false;
            yield break;
        }
        while (game.AdLocked(st.t)) yield return null; // that shelf is still popping a trio or sliding a row forward
        int j = game.AdSlot(st.t, st.j);
        if (j < 0) Debug.LogError($"Ad Capture: {scenario.name}: no empty slot at {st.t}.{st.j}");
        yield return Glide(game.AdAim(st.t, Mathf.Max(j, 0)), .5f, true);
        pressed = false;
        if (last) Time.timeScale = scenario.slowMo;
        game.AdDrop(st.t, j);
    }

    // the finger travels in a little arc; holding a product, it carries it like a real drag
    IEnumerator Glide(Vector3 to, float dur, bool holding)
    {
        var from = finger;
        for (float t = 0; t < 1; t += Time.deltaTime / dur)
        {
            float e = Mathf.SmoothStep(0, 1, t);
            finger = Vector3.Lerp(from, to, e) + Vector3.up * (holding ? .6f : .25f) * Mathf.Sin(Mathf.PI * e);
            if (holding) game.AdHold(finger);
            yield return null;
        }
        finger = to;
        if (holding) game.AdHold(finger);
    }

    void LateUpdate()
    {
        if (!game) return;
        zoom = Mathf.Lerp(zoom, zoomTo, 1.5f * Time.deltaTime);
        game.AdLate(scenario.clockSpeed, zoom);
    }

    void OnGUI()
    {
        if (game) game.AdGui(this);
    }

    void OnDisable()
    {
        Time.timeScale = 1;
        if (!english) return;
        if (langBefore == null) PlayerPrefs.DeleteKey("lang");
        else PlayerPrefs.SetString("lang", langBefore);
        PlayerPrefs.Save();
    }
}

// What AdDirector needs from the game, in this editor-only part of Game: Game.cs itself is left as shipped.
public partial class Game
{
    float adSize; // the camera size FitCamera gives, before the ad's zoom
    Vector2Int adScreen;
    bool adCleared;
    Texture2D adHand, adRing;

    public bool AdBusy => busy;
    public bool AdCleared => adCleared;
    public bool AdLocked(int s) => locked.Contains(s);
    public Vector3 AdAim(int s, int i) => SlotAim(s, i);

    // slot j of shelf t if empty, or its first empty slot (j < 0); -1 if none
    public int AdSlot(int t, int j)
    {
        if (j < 0) j = System.Array.IndexOf(shelves[t][0], -1);
        return j >= 0 && shelves[t][0][j] < 0 ? j : -1;
    }

    // StartLevel on the scenario's level, minus analytics, the country's twist, the golden product and the intro bubbles
    public void AdStart(AdScenario sc)
    {
        var lv = LevelGen.Decode(sc.level);
        menu = Menu.None;
        shelves = lv.Shelves; // Start renders the board straight away
        Start();
        AudioListener.volume = 1; // even if the game was muted
        world = sc.country;
        lvl = 0;
        coins = 120;
        time = total = baseTime = lv.Time;
        hard = lv.Hard;
        par = lv.Par;
        undoLeft = shuffleLeft = timeLeft = 1;
        lastMatch = -10;
        foreach (var c in sc.locks.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var p = c.Split('.');
            seals[(int.Parse(p[0]), int.Parse(p[1]))] = BentoMoves;
            twist = 2; // the padlocks are Japan's twist: UpdateEvents would place them, AdLate does
        }
        Render();
        adScreen = new Vector2Int(Screen.width, Screen.height);
        adSize = cam.orthographicSize;
    }

    // a tap that takes nothing: the game's answer for a frozen product, a crate or a padlock
    public void AdTap(int s, int i)
    {
        int x = shelves[s][0][i];
        if (LevelGen.Movable(x))
        {
            HeldBack(s, i);
            return;
        }
        Fx.Play("back");
        StartCoroutine(Fx.Squash(views[(s, i)].transform));
        Toast(x == LevelGen.Crate ? "Fais un trio à côté !" : "Glacé !");
    }

    // Update's pick-up: a carton opens in the hand
    public bool AdPick(int s, int i)
    {
        int x = shelves[s][0][i];
        if (!LevelGen.Movable(x) || HeldBack(s, i)) return false;
        if (LevelGen.IsBoxed(x))
        {
            shelves[s][0][i] = LevelGen.Id(x);
            Destroy(views[(s, i)]);
            views[(s, i)] = Item(shelves[s][0][i], SlotPos(s, i), 1f, false, $"{s},{i}");
            Fx.Burst(SlotAim(s, i) + new Vector3(0, 0, -1), 12);
            Fx.Play("match", 1.4f);
        }
        dragS = s;
        dragI = i;
        started = true;
        dragGo = views[(s, i)]; // the drag state, so a Render meanwhile hands the product over (KeepDrag)
        dragGo.transform.localScale = Vector3.one * 1.2f;
        Fx.Play("pick");
        return true;
    }

    public void AdHold(Vector3 finger)
    {
        if (dragGo) dragGo.transform.position = Vector3.Lerp(dragGo.transform.position, finger + new Vector3(0, 0, -1.5f), 25 * Time.deltaTime);
    }

    // Update's drop
    public void AdDrop(int t, int j)
    {
        var go = dragGo.transform;
        int s = dragS, i = dragI;
        dragGo = null;
        dragS = -1;
        if (j < 0)
        {
            Fx.Play("back");
            StartCoroutine(Fx.Move(go, go.position, SlotPos(s, i), go.localScale, Vector3.one, .15f));
            return;
        }
        Fx.Play("drop"); // no random pitch: every take sounds the same
        Move(s, i, t, j, go);
    }

    public void AdLate(float clockSpeed, float zoom)
    {
        if (Screen.width != adScreen.x || Screen.height != adScreen.y) // the Recorder sets the Game view to 1080x1920 as it starts
        {
            adScreen = new Vector2Int(Screen.width, Screen.height);
            Render();
            KeepDrag();
            adSize = cam.orthographicSize;
        }
        cam.orthographicSize = adSize / zoom;
        if (clockSpeed > 0) time = Mathf.Max(.5f, time - clockSpeed * Time.deltaTime);
        if (twist == 2) PlaceBentos();

        // Resolve ends a cleared board with Win(), which saves the level as won (ScoreLevel). Once only the trios being
        // popped are left, a product nobody sees is slipped onto an empty shelf: the board never counts as cleared.
        if (!adCleared && busy && locked.Count > 0 && Enumerable.Range(0, shelves.Count).All(s =>
                shelves[s].All(r => r.All(x => x < 0)) || locked.Contains(s) && shelves[s].Count == 1 && LevelGen.Triple(shelves[s][0])))
        {
            // ponytail: needs a shelf that is not popping; a level whose six shelves pop together would still Win
            int e = Enumerable.Range(0, shelves.Count).Where(s => !locked.Contains(s)).DefaultIfEmpty(-1).First();
            if (e >= 0)
            {
                shelves[e][0][0] = 0;
                adCleared = true;
            }
        }
    }

    // Win()'s fanfare and confetti, more of it, without Win itself
    public IEnumerator AdWin()
    {
        Fx.Play("win");
        for (int n = 0; n < 5; n++)
        {
            Fx.Burst(new Vector3(-2.4f + 1.2f * n, boardY + (n % 2 == 0 ? 1.5f : -.5f), -2), 40);
            yield return new WaitForSeconds(.12f);
        }
    }

    public void AdGui(AdDirector d)
    {
        if (!round) InitGui();
        var sc = d.scenario;
        float k = GuiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
        float W = 1080, H = Screen.height / k;
        var safe = Screen.safeArea;
        float top = (Screen.height - safe.yMax) / k + 30, bottom = H - safe.y / k - 50;

        // the level's HUD as OnGUI lays it out, only the parts the scenario shows; nothing in it can be pressed
        if (sc.topBar)
        {
            Panel(new Rect(40, top, 420, 110), hard ? Red : Cream);
            Label(new Rect(40, top, 420, 110), hard ? Tf("NIVEAU {0} · HARD", LevelName) : LevelTitle, hard ? 42 : 52, hard ? Color.white : Ink);
            CoinsPill(500, top);
            hudIcons ??= new[] { Fx.Icon(6), Fx.Icon(7) };
            for (int b = 0; b < 2; b++)
            {
                var r = new Rect(828 + b * 116, top, 96, 96);
                Button(r, "", Mist);
                GUI.color = Ink;
                GUI.DrawTexture(new Rect(r.x + 16, r.y + 14, 64, 64), hudIcons[b]);
                GUI.color = Color.white;
            }
        }
        coinTarget = new Vector2(555, top + 48);
        var bar = new Rect(40, top + 145, W - 80, 64);
        clockTarget = new Vector2(bar.x + 90, bar.center.y);
        if (sc.timer)
        {
            Panel(bar, new Color(0, 0, 0, .2f));
            bool low = time < 10;
            var fill = low && Mathf.Repeat(Time.time, .5f) < .25f ? Color.white : low ? Red : Gold;
            Panel(new Rect(bar.x, bar.y, Mathf.Max(64, bar.width * time / total), bar.height), fill);
            Label(new Rect(bar.x + 30, bar.y, bar.width / 2, bar.height), $"{Mathf.CeilToInt(time)} s", 40, Color.white, true, TextAnchor.MiddleLeft);
            Label(new Rect(bar.center.x, bar.y, bar.width / 2 - 30, bar.height), Tf("{0} / {1} coups", moves, par), 40,
                moves > par ? new Color(1f, .75f, .75f) : Color.white, true, TextAnchor.MiddleRight);
        }
        if (sc.shopSign)
        {
            var boardTop = cam.WorldToScreenPoint(new Vector3(0, BoardTopY + .3f, 0));
            StoreFront(bar.yMax + 30, (Screen.height - boardTop.y) / k - 20);
        }
        if (sc.bonuses)
        {
            float bw = 300, bh = 150, by = bottom - bh;
            icons ??= new[] { Fx.Icon(0), Fx.Icon(1), Fx.Icon(2) };
            string[] names = { "Annuler", "Mélanger", "+15 s" };
            for (int b = 0; b < 3; b++)
            {
                float x = 45 + b * (bw + 45);
                Button(new Rect(x, by, bw, bh), names[b], b != 0 || history.Count > 0 ? Gold : Mist, 38, icons[b]);
                Badge(x + bw - 44, by - 22, "1");
            }
        }
        DrawEvents(k); // the moves left written on the padlocks
        if (sc.toasts)
        {
            DrawFlyers();
            if (Time.time < toastUntil)
                Label(new Rect(0, H * .2f - (1 - (toastUntil - Time.time)) * 60, W, 140), toast, 84, Gold, true);
        }

        if (sc.hook != "" && d.endAt < 0)
        {
            float y = sc.timer ? bar.yMax + 40 : sc.topBar ? top + 150 : top + 40;
            var keep = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(.2f), new Vector2(W / 2 * k, (y + 120) * k));
            Logo(new Rect(50, y, W - 100, 240), sc.hook, 96);
            GUI.matrix = keep;
        }

        if (d.fingerOn)
        {
            adHand ??= AdHandTexture();
            adRing ??= AdRingTexture();
            float rt = (Time.time - d.tappedAt) / .35f; // the tap: a ring spreading out from the fingertip
            if (rt < 1)
            {
                var c = ToGui(d.tapAt);
                float r = 30 + 90 * rt;
                GUI.color = new Color(1, 1, 1, .85f * (1 - rt));
                GUI.DrawTexture(new Rect(c.x - r, c.y - r, 2 * r, 2 * r), adRing);
            }
            var p = ToGui(d.finger);
            var img = d.hand ? d.hand : adHand;
            float s = d.handSize * (d.pressed ? .9f : 1); // pressing, the hand sinks a little toward the screen
            float w = s * img.width / img.height;
            GUI.color = Color.white;
            var keep = GUI.matrix;
            GUIUtility.RotateAroundPivot(d.handAngle, p * k);
            GUI.DrawTexture(new Rect(p.x - w * d.handTip.x, p.y - s * d.handTip.y, w, s), img);
            GUI.matrix = keep;
        }

        if (d.outroAt >= 0 && d.endAt < 0)
        {
            Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .35f));
            var keep = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(d.outroAt), new Vector2(W / 2 * k, H / 2 * k));
            Logo(new Rect(60, H / 2 - 250, W - 120, 500), sc.outro, 150);
            GUI.matrix = keep;
        }

        if (d.endAt >= 0) AdEndCard(d.endAt, W, H, k);
    }

    // the logo as on the title screen, a breathing PLAY NOW button, and the release line
    void AdEndCard(float since, float W, float H, float k)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .6f));
        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(since), new Vector2(W / 2 * k, H / 2 * k));
        float ly = H / 2 - 520 + 8 * Mathf.Sin(Time.time * 2);
        var tilt = GUI.matrix;
        GUIUtility.RotateAroundPivot(-4, new Vector2(W / 2 * k, (ly + 170) * k));
        Logo(new Rect(0, ly, W, 190), "SHELF", 185);
        Logo(new Rect(0, ly + 160, W, 190), "SORT", 185);
        GUI.matrix = tilt;

        var play = new Rect(120, H / 2 + 60, W - 240, 210);
        GUIUtility.ScaleAroundPivot(Vector2.one * (1 + .06f * Mathf.Sin((Time.time - since) * 7)), play.center * k);
        Button(play, "", Gold);
        Label(new Rect(play.x, play.y, play.width, play.height - 10), "PLAY NOW", 110, Ink);
        float t = Mathf.Repeat(Time.time - since, 1.2f); // a shine sweeping across
        if (t < .6f)
        {
            GUI.BeginGroup(play);
            Panel(new Rect(Mathf.Lerp(-140, play.width + 40, t / .6f), 0, 70, play.height), new Color(1, 1, 1, .35f));
            GUI.EndGroup();
        }
        GUI.matrix = tilt;
        Label(new Rect(0, play.yMax + 70, W, 80), "Coming soon to the App Store", 56, Color.white, true);
        GUI.matrix = keep;
    }

    // the fake finger: a cartoon hand pointing up, its fingertip at (.42, .97), in a red cuff. Each part is outlined in ink
    // and laid over the ones behind it (so the folded fingers and the thumb read apart), shaded toward its rim, with a
    // soft shadow under the whole.
    static Texture2D AdHandTexture(int size = 256)
    {
        // back to front, in a 0..1 box with y up; a part is its distance function and its color
        var parts = new (System.Func<Vector2, float> d, Color c)[]
        {
            (p => AdBox(p, new(.57f, .11f), new(.19f, .075f), .035f), Red), // cuff
            (p => AdBox(p, new(.56f, .33f), new(.18f, .16f), .11f), Color.white), // palm
            (p => AdSegment(p, new(.79f, .43f), new(.79f, .37f)) - .062f, Color.white), // little finger
            (p => AdSegment(p, new(.68f, .48f), new(.68f, .40f)) - .07f, Color.white), // ring finger
            (p => AdSegment(p, new(.56f, .51f), new(.56f, .42f)) - .075f, Color.white), // middle finger
            (p => AdSegment(p, new(.42f, .89f), new(.42f, .40f)) - .082f, Color.white), // index
            (p => AdSegment(p, new(.28f, .43f), new(.44f, .33f)) - .075f, Color.white), // thumb
        };
        const float line = .03f;
        var rim = new Color(.83f, .86f, .93f);
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) / size, (y + .5f) / size);
                float ds = parts.Min(q => q.d(p + new Vector2(-.025f, .03f)));
                var c = new Color(0, 0, 0, .22f * Mathf.Clamp01((line - ds) * size * .25f + .5f));
                foreach (var (dist, color) in parts)
                {
                    float d = dist(p), a = Mathf.Clamp01((line - d) * size + .5f);
                    if (a <= 0) continue;
                    float inside = Mathf.Clamp01(-d / .05f);
                    var face = color == Color.white ? Color.Lerp(rim, Color.white, inside) : color * (.9f + .1f * inside);
                    var layer = Color.Lerp(Ink, face, Mathf.Clamp01(-d * size + .5f));
                    c = new Color(Mathf.Lerp(c.r, layer.r, a), Mathf.Lerp(c.g, layer.g, a), Mathf.Lerp(c.b, layer.b, a), c.a + (1 - c.a) * a);
                }
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    // rounded box: center, half size, corner radius
    static float AdBox(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x - c.x) - half.x + r, Mathf.Abs(p.y - c.y) - half.y + r);
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
    }

    static Texture2D AdRingTexture(int size = 128)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2((x + .5f) / size, (y + .5f) / size), new Vector2(.5f, .5f)) - .42f);
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((.06f - d) * size + .5f)));
            }
        t.Apply();
        return t;
    }

    static float AdSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        return (p - a - ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude)).magnitude;
    }
}
#endif
