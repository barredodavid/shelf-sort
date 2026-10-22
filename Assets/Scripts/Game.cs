using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Whole game in one component: board state, 3D views, drag & drop, timer, bonuses, IMGUI HUD.
public partial class Game : MonoBehaviour
{
    // products come from the current world's theme (Worlds.cs)
    static readonly Color Wall = new(.47f, .74f, .8f), Wood = new(.72f, .5f, .32f), Backboard = new(.93f, .85f, .72f);

    const float SlotW = 1.1f, ShelfW = 3.35f, ColGap = .3f;
    float rowH = 2.5f, boardY; // set by FitCamera so the shelving unit fills the space between HUD and bonus bar
    Transform wall;
    Texture2D wallTex;
    // BackOff/BackScale: where and how big the next row waits, relative to its slot. The riser grows with the row
    // height so the waiting row fills the space above the front one.
    float BackLift => rowH * .36f;
    Vector3 BackOff => new(0, BackLift, .55f);
    // Camera looks slightly down (CamTilt) at the board's origin, so shelf tops and the shadows on them show
    const float CamTilt = 10;
    static readonly Vector3 CamHome = Quaternion.Euler(CamTilt, 0, 0) * new Vector3(0, 0, -10);

    // where the screen point lands on the board plane (z = 0); the tilted camera makes the screen y != world y
    Vector3 OnBoard(Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float d);
        return ray.GetPoint(d);
    }
    const float BackScale = .8f;

    List<List<int[]>> shelves;
    readonly Stack<List<List<int[]>>> history = new();
    readonly Dictionary<(int, int), GameObject> views = new();
    int coins, combo, undoLeft, shuffleLeft, timeLeft, moves, par;
    readonly HashSet<int> locked = new(); // shelves mid-animation (popping trio, row sliding forward): no picking or dropping there
    float time, total, lastMatch, toastUntil, overAt;
    bool hard, busy, over, adUsed, won, started;
    string title, subtitle, toast, stickerMsg; // stickerMsg: a sticker won with the level (Collection.cs)
    Camera cam;
    Transform root;
    System.Random rng = new();

    // drag state
    GameObject dragGo;
    int dragS = -1, dragI;

    void Start()
    {
        Application.targetFrameRate = 60;
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.gameObject.AddComponent<AudioListener>(); // without one, no sound plays at all
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Wall;
        cam.transform.SetPositionAndRotation(CamHome, Quaternion.Euler(CamTilt, 0, 0));
        sun = new GameObject("Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .65f;
        // steep and mostly frontal: short shadows that land on the shelf right under/behind each product
        sun.transform.rotation = Quaternion.Euler(55, 15, 0);

        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        PostFx();
        BakeWall();
        ApplyLighting();
        root = new GameObject("Board").transform;
        Fx.Init(gameObject);
        ApplySound();
        InitAds();
        InitAnalytics();
        // -record <dir>: saves every frame at twice the window size, 30 fps of game time, for the App Store preview
        // video (BuildIOS.EncodePreview turns the frames into an .mp4)
        var args = System.Environment.GetCommandLineArgs();
        int rec = System.Array.IndexOf(args, "-record");
        if (rec >= 0)
        {
            recordDir = args[rec + 1];
            Time.captureFramerate = 30; // game time steps 1/30 s per frame however long the capture takes
            Application.runInBackground = true;
        }
        // -shots <dir>: F12 saves the screen at three times the window size, for the App Store screenshots
        // (a 430x932 window gives the 1290x2796 of a 6.9" iPhone)
        int shots = System.Array.IndexOf(args, "-shots");
        if (shots >= 0) shotsDir = args[shots + 1];
        // -set key=1,lang=en: saved progress to start from (stars, coins, language...), to film the later countries
        int set = System.Array.IndexOf(args, "-set");
        if (set >= 0)
            foreach (var kv in args[set + 1].Split(','))
            {
                var p = kv.Split('=');
                if (int.TryParse(p[1], out int v)) PlayerPrefs.SetInt(p[0], v);
                else PlayerPrefs.SetString(p[0], p[1]);
            }

        coins = PlayerPrefs.GetInt("coins", 0);
        world = PlayerPrefs.GetInt("world", 0); // where the truck waits on the map
        lvl = PlayerPrefs.GetInt("lvl", 0);
        Render();
    }

    // Warm, saturated "candy" grade with a soft glow; tune these numbers to taste
    void PostFx()
    {
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = p.Add<Bloom>(true);
        bloom.intensity.value = .35f;
        bloom.threshold.value = .95f;
        bloom.scatter.value = .6f;
        colorAdj = p.Add<ColorAdjustments>(true); // exposure is set by ApplyLighting
        colorAdj.saturation.value = 18;
        colorAdj.contrast.value = 10;
        p.Add<WhiteBalance>(true).temperature.value = 8;
        p.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
        var vignette = p.Add<Vignette>(true);
        vignette.intensity.value = .22f;
        vignette.smoothness.value = .5f;
        var vol = new GameObject("PostFX").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.profile = p;
    }

    // Shop wall: tiles with a light-to-deep vertical gradient and a skirting band at the bottom, baked into one
    // texture. Re-run when the tiles upgrade is bought.
    void BakeWall()
    {
        const int w = 256, h = 512, tile = 32;
        var (low, high) = WallColors;
        var tex = new Texture2D(w, h) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = (float)y / h; // 0 bottom, 1 top
                var c = Color.Lerp(low, high, t);
                bool grout = x % tile < 2 || y % tile < 2;
                if (grout) c *= .9f;
                else if ((x / tile + y / tile) % 2 == 0) c *= 1.03f; // faint checker
                if (t < .12f) c = new Color(.85f, .55f, .45f) * (y % 8 < 1 ? .9f : 1); // skirting
                c.a = 1;
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();

        if (!wall)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Quad).transform; // sized to the view in FitCamera
            Destroy(wall.GetComponent<Collider>());
            wall.position = new Vector3(0, 0, 1);
            // Unlit (the pipeline's particle material, always shipped): no light and no shadows on the wall, whose
            // colors show exactly as baked. URP Lit ignores Renderer.receiveShadows, so stray shelf shadows came through.
            var wr = wall.GetComponent<Renderer>();
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wr.material = new Material(Fx.Unlit) { color = Color.white };
        }
        if (wallTex) Destroy(wallTex); // only our own previous bake: the material starts with a built-in texture asset
        wallTex = tex;
        wall.GetComponent<Renderer>().material.mainTexture = tex;
    }

    string recordDir; // set by -record; Update saves the frame (not a coroutine: StartLevel stops them all)
    int recordFrame;
    string shotsDir; // set by -shots

    void StartLevel()
    {
        StopAllCoroutines();
        cam.transform.position = CamHome;
        var lv = daily ? DailyLevel() : LevelGen.Make(world * LevelsPerWorld + lvl + 1, rng);
        shelves = lv.Shelves;
        time = total = baseTime = lv.Time;
        hard = lv.Hard;
        par = lv.Par;
        moves = 0;
        locked.Clear();
        dragGo = null;
        dragS = -1;
        busy = over = adUsed = won = usedBonus = started = false;
        TrackLevel(GameAnalyticsSDK.GAProgressionStatus.Start);
        history.Clear();
        undoLeft = shuffleLeft = timeLeft = daily && DailyTwist == 2 ? 0 : 1; // the "no bonus" challenge
        combo = 0;
        lastMatch = -10;
        // now and then one product of the level is golden: its trio pays coins (never on the very first level)
        var ids = shelves.SelectMany(sh => sh).SelectMany(r => r).Where(LevelGen.IsProduct).Select(LevelGen.Id).Distinct().ToList();
        goldenId = daily ? (DailyTwist == 3 ? AllGolden : -1) : world + lvl > 0 && rng.NextDouble() < GoldenChance ? ids[rng.Next(ids.Count)] : -1;
        Render();
        StartEvents();

        if (daily) Toast(DailyTwists[DailyTwist]);
        else if (hard) Toast("NIVEAU DIFFICILE !");
        else if (goldenId >= 0) Toast("PRODUIT DORÉ !");
        StartIntros(); // a bubble for each new thing in this level (Intro.cs)
    }

    // ---------- layout ----------

    int Rows => shelves == null ? 3 : (shelves.Count + 1) / 2; // no level dealt yet (title screen): standard layout

    // plank center of shelf s; row 0 is the top row
    Vector3 ShelfPos(int s)
    {
        float x = (s % 2 == 0 ? -1 : 1) * (ShelfW + ColGap) / 2;
        float y = boardY - Rows * rowH / 2 + (Rows - 1 - s / 2) * rowH;
        return new Vector3(x, y, 0);
    }

    float BoardTopY => boardY + Rows * rowH / 2;
    const float UnitHalfW = ShelfW + ColGap / 2 + .2f; // outer edge of the side posts

    // where a product's feet go: on the plank top (.11)
    Vector3 SlotPos(int s, int i) => ShelfPos(s) + new Vector3((i - 1) * SlotW, .11f, 0);
    // where the finger aims: roughly the product's middle
    Vector3 SlotAim(int s, int i) => SlotPos(s, i) + new Vector3(0, .55f, 0);

    // Width fits the shelving unit; rows then stretch (2.4..3.6) to fill the band left free by the HUD.
    // If the rows don't fit even at minimum height, zoom out instead.
    void FitCamera()
    {
        float hv = Screen.height * 1080f / Screen.width; // screen height on the 1080-wide GUI canvas
        float free = 1 - (HudTopPx() + HudBottomPx()) / hv; // share of the screen height left for the board
        float size = (UnitHalfW + .1f) / cam.aspect;
        const float minRow = 2.4f, maxRow = 3.6f, crown = .3f;
        if (2 * size * free < Rows * minRow + crown) size = (Rows * minRow + crown) / (2 * free);
        rowH = Mathf.Min((2 * size * free - crown) / Rows, maxRow);
        cam.orthographicSize = size;
        float top = size - 2 * size * HudTopPx() / hv, bottom = -size + 2 * size * HudBottomPx() / hv;
        boardY = (top + bottom) / 2 - crown / 2;
        wall.localScale = new Vector3(2 * size * cam.aspect + 2, 2 * size + 4, 1); // + margin for the tilt
    }

    // GUI canvas pixels reserved above/below the board: these mirror the OnGUI layout
    float GuiScale => Screen.width / 1080f;
    float SafeTopPx() => (Screen.height - Screen.safeArea.yMax) / GuiScale;
    float SafeBottomPx() => Screen.safeArea.y / GuiScale;
    float HudTopPx() => SafeTopPx() + 30 + 209 + 30 + 270 + 20; // top bar + timer, then the shop sign
    float HudBottomPx() => SafeBottomPx() + 50 + 150 + 22 + 30; // bonus buttons + their badges

    // ---------- views ----------

    void Render()
    {
        if (homeDemo != null) StopCoroutine(homeDemo); // the title-screen demo animates objects about to go
        foreach (Transform c in root) Destroy(c.gameObject);
        views.Clear();
        FitCamera();
        if (menu is not (Menu.Map or Menu.World)) cam.transform.SetPositionAndRotation(CamHome, Quaternion.Euler(CamTilt, 0, 0)); // back from the maps
        if (menu != Menu.World) RestoreCamera(); // the globe darkens the background
        if (menu == Menu.Home)
        {
            RenderHome();
            return;
        }
        if (menu == Menu.World)
        {
            BuildGlobe();
            return;
        }
        if (menu == Menu.Trophies) return; // all GUI (Trophies.cs)
        if (menu == Menu.Catalog)
        {
            RenderCatalog();
            return;
        }
        if (menu == Menu.Map)
        {
            BuildMap(); // the camera moves over to it in UpdateMap
            return;
        }

        // one shelving unit for the whole board: back panel, three posts, crown on top
        float bottom = boardY - Rows * rowH / 2 - .11f, height = Rows * rowH + .11f;
        Box(new Vector3(0, bottom + height / 2, .85f), new Vector3(2 * UnitHalfW, height, .1f), BackboardC);
        foreach (float x in new[] { -UnitHalfW + .1f, 0, UnitHalfW - .1f })
            Box(new Vector3(x, bottom + height / 2, 0), new Vector3(x == 0 ? ColGap : .2f, height, 1.7f), WoodC);
        Box(new Vector3(0, BoardTopY + .15f, 0), new Vector3(2 * UnitHalfW + .3f, .3f, 1.9f), WoodC * .85f);

        for (int s = 0; s < shelves.Count; s++)
        {
            var p = ShelfPos(s);
            Box(p, new Vector3(ShelfW, .22f, 1.6f), WoodC);
            Box(p + new Vector3(0, -.02f, -.81f), new Vector3(ShelfW, .16f, .02f), Cream); // price-label strip
            for (int i = 0; i < 3; i++)
                Box(SlotPos(s, i) + new Vector3(0, -.13f, -.83f), new Vector3(.34f, .11f, .02f), TagC); // price tag
            Box(p + new Vector3(0, .11f + BackLift / 2, .55f), new Vector3(ShelfW, BackLift, .5f), WoodC * .9f); // riser: its top meets the back row's feet

            var sh = shelves[s];
            for (int i = 0; i < 3; i++)
            {
                if (sh[0][i] >= 0) views[(s, i)] = Item(sh[0][i], SlotPos(s, i), 1f, false, $"{s},{i}");
                if (LevelGen.IsFrozen(sh[0][i])) IceFrame(views[(s, i)].transform);
                // next row waits behind, on the riser, so it peeks over the front one
                if (sh.Count > 1 && sh[1][i] >= 0) Item(sh[1][i], SlotPos(s, i) + BackOff, BackScale, true, "back");
            }
        }
    }

    GameObject Box(Vector3 pos, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().material = new Material(Fx.Lit) { color = c };
        return go;
    }

    // Wraps the model in a pivot at its feet, so every product stands on its slot and fits a 1.04 wide x 1.45 tall box, whatever its native size.
    GameObject Item(int id, Vector3 pos, float scale, bool ghost, string name)
    {
        var model = Instantiate(id == LevelGen.Crate ? crateModel ??= Hidden(Products.Crate())
            : LevelGen.IsBoxed(id) ? mysteryModel ??= Hidden(Products.Mystery())
            : Model(LevelGen.Id(id)));
        model.SetActive(true); // procedural templates are kept inactive
        if (LevelGen.IsProduct(id) && !LevelGen.IsBoxed(id) && Golden(id))
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(Goldify).ToArray();
        // the waiting row keeps its real colors, only darker: in the shade at the back, yet still recognizable
        if (ghost)
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(Dim).ToArray();
        return Wrap(model, pos, scale, name, null);
    }

    // ---------- the golden product: one product of some levels, gilded; each of its trios pays GoldenCoins ----------

    const float GoldenChance = .35f;
    const int GoldenCoins = 15;
    int goldenId = -1; // AllGolden: every product (a daily challenge)
    bool Golden(int x) => goldenId == AllGolden || LevelGen.Id(x) == goldenId;
    int GoldPay => goldenId == AllGolden ? 5 : GoldenCoins; // when all are golden, each trio pays less
    readonly Dictionary<Material, Material> gilded = new();

    Material Goldify(Material m)
    {
        if (!gilded.TryGetValue(m, out var g))
        {
            gilded[m] = g = new Material(m) { color = Color.Lerp(m.color, new Color(1f, .8f, .25f), .55f) };
            g.EnableKeyword("_EMISSION");
            g.SetColor("_EmissionColor", new Color(1f, .7f, .15f) * .35f);
            g.SetFloat("_Smoothness", .9f);
        }
        return g;
    }

    // ---------- flyers: little things flying across the HUD (coins to the purse, "+2 s" to the clock) ----------

    readonly List<(Vector2 from, Vector2 to, float at, string text)> flyers = new();
    Vector2 coinTarget, clockTarget; // where they land, set by the HUD as it lays out

    void Fly(Vector3 worldFrom, Vector2 to, string text, float delay = 0) => flyers.Add((ToGui(worldFrom), to, Time.time + delay, text));

    void DrawFlyers()
    {
        flyers.RemoveAll(f => Time.time > f.at + .6f);
        foreach (var (from, to, at, text) in flyers)
        {
            float t = (Time.time - at) / .6f;
            if (t < 0) continue;
            var p = Vector2.Lerp(from, to, t * t) + Vector2.down * 160 * Mathf.Sin(Mathf.PI * t); // speeding up, on an arc
            if (text == null) // a coin
            {
                Panel(new Rect(p.x - 30, p.y - 30, 60, 60), Darker(Gold));
                Panel(new Rect(p.x - 26, p.y - 30, 52, 52), Gold);
            }
            else Label(new Rect(p.x - 120, p.y - 40, 240, 80), text, 52, Color.white, true);
        }
    }

    readonly Dictionary<Material, Material> dimmed = new();

    Material Dim(Material m)
    {
        if (!dimmed.TryGetValue(m, out var d))
            dimmed[m] = d = new Material(m) { color = m.color * new Color(.5f, .5f, .58f) };
        return d;
    }

    GameObject crateModel, mysteryModel; // obstacle templates, shared by every world

    static GameObject Hidden(GameObject template)
    {
        template.SetActive(false);
        return template;
    }

    // Puts a model instance in a pivot at its feet, sized to the slot; mat (if any) replaces its materials
    GameObject Wrap(GameObject model, Vector3 pos, float scale, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root);
        go.transform.position = pos;
        model.transform.SetParent(go.transform, false);
        model.transform.localRotation = Quaternion.Euler(-20, 0, 0) * Quaternion.Euler(0, 35, 0); // 3/4 view; with the camera tilt, seen from ~30° above
        var rs = model.GetComponentsInChildren<Renderer>();
        if (mat) foreach (var r in rs) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float k = Mathf.Min(1.04f / b.size.x, 1.45f / b.size.y, 1.04f / b.size.z) * Products.Fill(model.name);
        model.transform.localScale *= k;
        // scaling is around the pivot (pos): move the scaled bounds' bottom-center onto pos
        var c = pos + (b.center - pos) * k;
        model.transform.position += pos - new Vector3(c.x, c.y - b.extents.y * k, c.z);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    // Ice block around a frozen product (on its feet pivot): a translucent cube textured with frost, glints and
    // cracks (Fx.IceTexture), capped with snow, over the product's icy tint.
    Material iceBlockMat;

    void IceFrame(Transform item)
    {
        const float w = 1.16f, h = 1.6f, d = 1.1f;
        if (!iceBlockMat)
        {
            var asset = Resources.Load<Material>("Ice"); // transparent URP Lit, created by Setup (keeps its variant in builds)
            if (!asset) return;
            iceBlockMat = new Material(asset) { mainTexture = Fx.IceTexture(), color = Color.white };
        }
        var cube = Box(item.position + new Vector3(0, h / 2, 0), new Vector3(w, h, d), Color.white);
        var r = cube.GetComponent<Renderer>();
        r.sharedMaterial = iceBlockMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cube.transform.SetParent(item, true);

        // snow: a slab overhanging the top, plus a smaller lump on it
        var snow = new Color(.97f, .99f, 1f);
        foreach (var (pos, size) in new[]
                 {
                     (new Vector3(0, h + .05f, 0), new Vector3(w + .08f, .12f, d + .08f)),
                     (new Vector3(-.12f, h + .15f, -.05f), new Vector3(w * .55f, .1f, d * .6f)),
                 })
        {
            var s = Box(item.position + pos, size, snow);
            s.GetComponent<Renderer>().material.SetFloat("_Smoothness", .1f);
            s.transform.SetParent(item, true);
        }
    }

    (int, int) NearestItem(Vector3 p)
    {
        (int, int) best = (-1, -1);
        float bestD = .7f;
        for (int s = 0; s < shelves.Count; s++)
            for (int i = 0; i < 3; i++)
            {
                if (shelves[s][0][i] < 0 || locked.Contains(s)) continue;
                float d = Vector2.Distance(p, SlotAim(s, i));
                if (d < bestD) { bestD = d; best = (s, i); }
            }
        return best;
    }

    // ---------- input ----------

    void Update()
    {
        if (recordDir != null) ScreenCapture.CaptureScreenshot($"{recordDir}/{recordFrame++:D5}.png", 2); // at frame end
        if (shotsDir != null && Input.GetKeyDown(KeyCode.F12))
            ScreenCapture.CaptureScreenshot($"{shotsDir}/{System.DateTime.Now:HHmmss_fff}.png", 3);
        AnimateHome();
        UpdateMap();
        UpdateWorld();
        TickStars();
        AnimateStage();
        TrophyIcons();
        if (started && !over && !Paused) UpdateEvents();
        if (started && !busy && !over && !Paused)
        {
            time = Mathf.Max(0, time - Time.deltaTime);
            if (time <= 0) Lose("Temps écoulé !");
        }

        // Touch is delivered as mouse (Input.simulateMouseWithTouches is on by default)
        Vector3 world = OnBoard(Input.mousePosition);

        // Picking stays possible while trios pop elsewhere: only the animating shelves are locked
        if (Input.GetMouseButtonDown(0) && !over && !Paused)
        {
            var (s, i) = NearestItem(world);
            int x = s >= 0 ? shelves[s][0][i] : -1;
            if (s >= 0 && !LevelGen.Movable(x))
            {
                Fx.Play("back");
                StartCoroutine(Fx.Squash(views[(s, i)].transform));
                Toast(x == LevelGen.Crate ? "Fais un trio à côté !" : "Glacé !");
            }
            else if (s >= 0 && !HeldBack(s, i)) // a pigeon, a siesta or a sealed bento may hold it back
            {
                if (LevelGen.IsBoxed(x)) // the carton opens in the hand: swap its view for the product inside
                {
                    // ponytail: not in the undo history, so an undo puts the product back in its carton
                    shelves[s][0][i] = LevelGen.Id(x);
                    Destroy(views[(s, i)]);
                    views[(s, i)] = Item(shelves[s][0][i], SlotPos(s, i), 1f, false, $"{s},{i}");
                    Fx.Burst(SlotAim(s, i) + new Vector3(0, 0, -1), 12);
                    Fx.Play("match", 1.4f);
                }
                dragS = s;
                dragI = i;
                started = true; // the clock starts with the first product picked up
                dragGo = views[(s, i)];
                dragGo.transform.localScale = Vector3.one * 1.2f;
                Fx.Play("pick");
            }
        }

        // follow the finger with a little lag, held above it so it stays visible
        if (dragGo)
            dragGo.transform.position = Vector3.Lerp(dragGo.transform.position, world + new Vector3(0, 0, -1.5f), 25 * Time.deltaTime);

        if (Input.GetMouseButtonUp(0) && dragGo)
        {
            var (ts, ti) = NearestEmpty(world);
            var go = dragGo.transform;
            int s = dragS, i = dragI;
            dragGo = null;
            dragS = -1;
            if (ts >= 0 && !over)
            {
                Fx.Play("drop", Random.Range(.95f, 1.05f));
                Move(s, i, ts, ti, go);
            }
            else
            {
                Fx.Play("back");
                StartCoroutine(Fx.Move(go, go.position, SlotPos(s, i), go.localScale, Vector3.one, .15f));
            }
        }
    }

    // Resolve() rebuilds every view; a product held by the finger is handed over to its rebuilt view
    void KeepDrag()
    {
        if (dragS < 0 || !views.TryGetValue((dragS, dragI), out var v)) return;
        var pos = dragGo ? dragGo.transform.position : v.transform.position;
        dragGo = v;
        dragGo.transform.position = pos;
        dragGo.transform.localScale = Vector3.one * 1.2f;
    }

    (int, int) NearestEmpty(Vector3 p)
    {
        (int, int) best = (-1, -1);
        float bestD = .9f; // drop tolerance, in world units
        for (int s = 0; s < shelves.Count; s++)
            for (int i = 0; i < 3; i++)
            {
                if (shelves[s][0][i] >= 0 || locked.Contains(s) || s == shutS) continue; // nothing goes on a shelf shut for the siesta
                float d = Vector2.Distance(p, SlotAim(s, i));
                if (d < bestD) { bestD = d; best = (s, i); }
            }
        return best;
    }

    // ---------- rules ----------

    static List<List<int[]>> Clone(List<List<int[]>> b) => b.Select(sh => sh.Select(r => (int[])r.Clone()).ToList()).ToList();

    void Move(int a, int i, int b, int j, Transform view)
    {
        history.Push(Clone(shelves));
        shelves[b][0][j] = shelves[a][0][i];
        shelves[a][0][i] = -1;
        moves++;
        TickSeals();
        // the dropped product settles into its slot right away, even while another shelf is still animating
        views.Remove((a, i));
        views[(b, j)] = view.gameObject;
        view.position = SlotPos(b, j);
        view.localScale = Vector3.one;
        StartCoroutine(Fx.Squash(view));
        if (!busy) StartCoroutine(Resolve()); // a running Resolve re-reads the board on its next pass
    }

    // Promotes emptied rows, pops trios, repeats until stable. Only the shelves being animated are locked, so the
    // player can keep moving products elsewhere; their moves are picked up on the next pass of the loop.
    IEnumerator Resolve()
    {
        busy = true;
        while (true)
        {
            var promoted = new HashSet<int>();
            for (int s = 0; s < shelves.Count; s++)
                while (shelves[s].Count > 1 && shelves[s][0].All(x => x < 0))
                {
                    shelves[s].RemoveAt(0);
                    promoted.Add(s);
                }

            if (promoted.Count > 0)
            {
                Render();
                KeepDrag();
                // the waiting row slides forward from the riser
                locked.UnionWith(promoted);
                foreach (int s in promoted)
                    for (int i = 0; i < 3; i++)
                        if (views.TryGetValue((s, i), out var v))
                            StartCoroutine(Fx.Move(v.transform, SlotPos(s, i) + BackOff, SlotPos(s, i), Vector3.one * BackScale, Vector3.one, .2f));
                yield return new WaitForSeconds(.2f);
                locked.Clear();
            }

            // read the board only now: moves made during the slide count. No yield from here to break/end.
            var hits = Enumerable.Range(0, shelves.Count).Where(s => LevelGen.Triple(shelves[s][0])).ToList();
            if (hits.Count == 0) break;
            locked.UnionWith(hits);

            combo = Time.time - lastMatch < 4 ? combo + 1 : 1;
            lastMatch = Time.time;
            Fx.Play("match", 1 + .12f * (combo - 1)); // each combo step sings a bit higher
            if (combo > 1) // praise growing with the chain, and the 2 seconds won flying to the clock
            {
                time = Mathf.Min(total, time + 2);
                Toast(combo == 2 ? "SUPER ! x2" : combo == 3 ? "GÉNIAL ! x3" : Tf("INCROYABLE ! x{0}", combo));
                StartCoroutine(Fx.Shake(cam.transform, CamHome, .08f + .04f * combo));
                Fly(ShelfPos(hits[0]) + new Vector3(0, .7f, 0), clockTarget, "+2 s", .15f);
            }
            // a golden trio pays: coins fly to the purse
            foreach (int s in hits.Where(h => Golden(shelves[h][0][0])))
            {
                Count("golden");
                coins += GoldPay;
                PlayerPrefs.SetInt("coins", coins);
                PlayerPrefs.Save();
                Fx.Play("match", 1.9f);
                Fx.Haptic(3);
                Toast(Tf("+{0} PIÈCES !", GoldPay));
                for (int c = 0; c < 6; c++) Fly(ShelfPos(s) + new Vector3((c - 2.5f) * .25f, .7f, 0), coinTarget, null, .1f + c * .06f);
            }

            Count("trios", hits.Count);
            Best("combo", combo);
            foreach (int s in hits) TrioEvents(s, LevelGen.Id(shelves[s][0][0])); // a sealed bento popped, the siesta beaten, the sale's
            yield return new WaitForSeconds(.08f); // let the trio register before it pops
            // the trio swells, jumps and spins, then shrinks away
            var popping = hits.SelectMany(s => Enumerable.Range(0, 3).Select(i => views[(s, i)].transform)).ToList();
            var start = popping.Select(v => v.position).ToList();
            for (float t = 0; t < .34f; t += Time.deltaTime)
            {
                float k = t < .12f ? Mathf.Lerp(1, 1.35f, t / .12f) : Mathf.Lerp(1.35f, 0, (t - .12f) / .22f);
                for (int n = 0; n < popping.Count; n++)
                {
                    popping[n].localScale = Vector3.one * k;
                    popping[n].position = start[n] + Vector3.up * .5f * Mathf.Sin(Mathf.PI * t / .34f);
                    popping[n].localRotation = Quaternion.Euler(0, 720 * t, 0);
                }
                yield return null;
            }
            foreach (int s in hits)
            {
                Fx.Burst(ShelfPos(s) + new Vector3(0, .7f, -1));
                shelves[s][0] = new[] { -1, -1, -1 };
                for (int i = 0; i < 3; i++)
                {
                    Destroy(views[(s, i)]);
                    views.Remove((s, i));
                }
            }
            // crates on the neighboring shelves burst open
            foreach (var (s, i) in LevelGen.BreakCrates(shelves, hits))
            {
                Fx.Burst(SlotAim(s, i) + new Vector3(0, 0, -1), 25);
                StartCoroutine(Fx.Shake(cam.transform, CamHome, .1f));
                Destroy(views[(s, i)]);
                views.Remove((s, i));
            }
            locked.Clear();
        }
        busy = false;

        if (shelves.All(sh => sh.All(r => r.All(x => x < 0)))) Win();
        else if (shelves.All(sh => sh[0].All(x => x >= 0))) Lose("Plus de place !");
    }

    void DoShuffle()
    {
        // frozen products and crates stay put
        var cells = Enumerable.Range(0, shelves.Count).SelectMany(s => Enumerable.Range(0, 3).Select(i => (s, i)))
            .Where(c => (shelves[c.s][0][c.i] < 0 || LevelGen.Movable(shelves[c.s][0][c.i])) && !seals.ContainsKey(c)).ToList(); // sealed bentos too
        pigeon = (-1, -1);
        var vals = LevelGen.Shuffle(cells.Select(c => shelves[c.s][0][c.i]).ToList(), rng);
        for (int k = 0; k < cells.Count; k++) shelves[cells[k].s][0][cells[k].i] = vals[k];
    }

    void Win()
    {
        over = won = true;
        overAt = Time.time;
        ScoreLevel();
        TrackLevel(GameAnalyticsSDK.GAProgressionStatus.Complete, starsWon);
        Fx.Play("win");
        Fx.Burst(new Vector3(-2, 1, -2), 40);
        Fx.Burst(new Vector3(2, 1, -2), 40);
    }

    void Lose(string msg)
    {
        if (over) return;
        over = true;
        overAt = Time.time;
        TrackLevel(GameAnalyticsSDK.GAProgressionStatus.Fail);
        title = msg;
        subtitle = "Tu y étais presque !";
        Fx.Play("lose");
    }

    void ContinueAfterAd() => ShowRewarded(() =>
    {
        adUsed = true;
        over = false;
        if (time <= 0) { time = 15; total = Mathf.Max(total, 15); }
        else for (int k = 0; k < 2 && history.Count > 0; k++) shelves = history.Pop();
        Render();
    });

    void Toast(string t) { toast = t; toastUntil = Time.time + 1f; }

    // ---------- HUD (IMGUI, laid out on a 1080-wide virtual canvas) ----------
    // ponytail: styled IMGUI is enough to ship a first version; move to UI Toolkit if menus/shop grow

    static readonly Color Ink = new(.14f, .19f, .29f), Gold = new(1f, .76f, .24f), Red = new(.91f, .3f, .27f),
        Cream = new(1f, .98f, .93f), Mist = new(.84f, .9f, .95f);
    Texture2D round, circle;
    Texture2D[] icons, hudIcons; // bonus bar: undo, shuffle, time; top bar: restart, pause
    GUIStyle pill, text;

    void InitGui()
    {
        round = Fx.Rounded(64, 30);
        circle = Fx.Rounded(64, 32);
        // same background in every state: no hover/focus looks, which mean nothing on a touch screen
        pill = new GUIStyle
        {
            normal = { background = round }, hover = { background = round }, active = { background = round },
            focused = { background = round }, onNormal = { background = round }, onHover = { background = round },
            border = new RectOffset(30, 30, 30, 30),
        };
        var font = Resources.Load<Font>("UI/Font"); // optional display font; falls back to the default bold
        text = new GUIStyle(GUI.skin.label)
        {
            font = font ? font : GUI.skin.font, fontStyle = font ? FontStyle.Normal : FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = true, clipping = TextClipping.Overflow,
        };
    }

    static Color Darker(Color c) => new(c.r * .72f, c.g * .72f, c.b * .72f, 1);

    void Panel(Rect r, Color c)
    {
        GUI.backgroundColor = c;
        GUI.Box(r, GUIContent.none, pill);
        GUI.backgroundColor = Color.white;
    }

    void Label(Rect r, string s, int size, Color c, bool shadow = false, TextAnchor align = TextAnchor.MiddleCenter)
    {
        s = T(s); // every text on screen goes through the translation (Lang.cs)
        text.fontSize = size;
        text.alignment = align;
        if (shadow)
        {
            text.normal.textColor = text.hover.textColor = text.active.textColor = new Color(0, 0, 0, .3f);
            GUI.Label(new Rect(r.x, r.y + 6, r.width, r.height), s, text);
        }
        text.normal.textColor = text.hover.textColor = text.active.textColor = c;
        GUI.Label(r, s, text);
    }

    // Chunky mobile button: darker lip underneath, face sinks onto it while pressed
    // With an icon: icon on top, label underneath as a caption.
    bool Button(Rect r, string label, Color bg, int size = 46, Texture2D icon = null)
    {
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        bool down = GUI.enabled && Input.GetMouseButton(0) && r.Contains(Event.current.mousePosition);
        Panel(new Rect(r.x, r.y + 14, r.width, r.height), Darker(bg));
        var face = down ? new Rect(r.x, r.y + 10, r.width, r.height) : r;
        Panel(face, bg);
        if (icon)
        {
            float s = face.height * .55f;
            GUI.color = Ink;
            GUI.DrawTexture(new Rect(face.center.x - s / 2, face.y + face.height * .07f, s, s), icon);
            GUI.color = Color.white;
            face = new Rect(face.x, face.y + face.height * .6f, face.width, face.height * .36f);
        }
        Label(face, label, size, Ink);
        return clicked;
    }

    void Badge(float x, float y, string s)
    {
        Panel(new Rect(x, y, 64, 64), Red);
        Label(new Rect(x, y, 64, 64), s, 36, Color.white);
    }

    void OnGUI()
    {
        if (!round) InitGui();
        float k = Screen.width / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
        float W = 1080, H = Screen.height / k;
        var safe = Screen.safeArea;
        float top = (Screen.height - safe.yMax) / k + 30, bottom = H - safe.y / k - 50;
        switch (menu)
        {
            case Menu.Home: HomeScreen(W, H, k, top - 30, bottom); TrophyBanner(W, top); return;
            case Menu.Map: MapScreen(W, H, top - 30, bottom); TrophyBanner(W, top); return;
            case Menu.Catalog: CatalogScreen(W, H, top - 30); TrophyBanner(W, top); return;
            case Menu.Trophies: TrophyScreen(W, H, top - 30); return;
            case Menu.World: WorldScreen(W, H, top - 30, bottom); return;
        }
        bool live = !busy && !over && menu == Menu.None;

        // top bar: level (red on HARD levels), coins, pause
        Panel(new Rect(40, top, 420, 110), hard ? Red : Cream);
        Label(new Rect(40, top, 420, 110), hard && !daily ? Tf("NIVEAU {0} · HARD", LevelName) : LevelTitle, hard || daily ? 42 : 52, hard ? Color.white : Ink);
        CoinsPill(500, top);
        coinTarget = new Vector2(555, top + 48); // the purse's coin
        GUI.enabled = live;
        // two round buttons: start the level over (so nobody reaches for "Annuler" to do it), and pause
        hudIcons ??= new[] { Fx.Icon(6), Fx.Icon(7) };
        for (int b = 0; b < 2; b++)
        {
            var r = new Rect(828 + b * 116, top, 96, 96);
            if (Button(r, "", Mist))
            {
                if (b == 0) StartLevel();
                else OpenMenu(Menu.Pause);
            }
            GUI.color = Ink;
            GUI.DrawTexture(new Rect(r.x + 16, r.y + 14, 64, 64), hudIcons[b]);
            GUI.color = Color.white;
        }
        GUI.enabled = true;

        // timer: blinks red under 10 s
        var bar = new Rect(40, top + 145, W - 80, 64); // >= 60 tall, or the 30px rounded corners overlap
        Panel(bar, new Color(0, 0, 0, .2f));
        bool low = time < 10;
        var fill = low && Mathf.Repeat(Time.time, .5f) < .25f ? Color.white : low ? Red : Gold;
        Panel(new Rect(bar.x, bar.y, Mathf.Max(64, bar.width * time / total), bar.height), fill);
        Label(new Rect(bar.x + 30, bar.y, bar.width / 2, bar.height), $"{Mathf.CeilToInt(time)} s", 40, Color.white, true, TextAnchor.MiddleLeft);
        clockTarget = new Vector2(bar.x + 90, bar.center.y);
        // moves against the target for the moves star; turns red once over it
        Label(new Rect(bar.center.x, bar.y, bar.width / 2 - 30, bar.height), Tf("{0} / {1} coups", moves, par), 40,
            moves > par ? new Color(1f, .75f, .75f) : Color.white, true, TextAnchor.MiddleRight);

        var boardTop = cam.WorldToScreenPoint(new Vector3(0, BoardTopY + .3f, 0));
        StoreFront(bar.yMax + 30, (Screen.height - boardTop.y) / k - 20);

        // bonuses
        // bonuses: icon + what it does; spent or unusable ones turn grey, the badge says how many are left
        float bw = 300, bh = 150, by = bottom - bh;
        icons ??= new[] { Fx.Icon(0), Fx.Icon(1), Fx.Icon(2) };
        string[] names = { "Annuler", "Mélanger", "+15 s" };
        int[] left = { undoLeft, shuffleLeft, timeLeft };
        for (int b = 0; b < 3; b++)
        {
            float x = 45 + b * (bw + 45);
            // a spent bonus can be bought again with coins
            bool free = left[b] > 0, can = (free || coins >= BonusPrice[b]) && (b != 0 || history.Count > 0) && !(daily && DailyTwist == 2);
            GUI.enabled = live && can;
            if (Button(new Rect(x, by, bw, bh), names[b], can ? Gold : Mist, 38, icons[b]))
            {
                if (!free) BuyBonus(b);
                UseBonus(b);
            }
            GUI.enabled = true;
            if (free) Badge(x + bw - 44, by - 22, left[b].ToString());
            else // price tag
            {
                var tag = new Rect(x + bw - 120, by - 28, 150, 60);
                Panel(tag, Ink);
                GUI.color = Gold;
                GUI.DrawTexture(new Rect(tag.x + 14, tag.y + 12, 36, 36), circle);
                GUI.color = Color.white;
                Label(new Rect(tag.x + 50, tag.y, tag.width - 56, tag.height), BonusPrice[b].ToString(), 34, Color.white);
            }
        }
        GUI.enabled = true;


        if (!over) DrawEvents(k);
        DrawFlyers();
        if (Time.time < toastUntil)
        {
            float a = 1 - (toastUntil - Time.time); // 0→1 over the toast's life
            Label(new Rect(0, H * .2f - a * 60, W, 140), toast, 84, Gold, true);
        }

        if (over) EndCard(W, H, k);
        if (menu == Menu.Pause) PauseCard(W, H, k);
        else if (intros.Count > 0 && !over) IntroCard(W, H, k);
        TrophyBanner(W, top);
    }

    // Shop sign + striped awning, centered in the gap between the timer and the cabinets.
    // ponytail: skipped when the board is too tall (3+ rows); shrink it instead if those levels look bare
    void StoreFront(float from, float to)
    {
        const float h = 270, awningH = 60;
        if (to - from < h - 10) return; // FitCamera reserves exactly h, allow rounding
        float y = from + (to - from - h) / 2;

        var sign = new Rect(140, y, 800, 140);
        Panel(new Rect(sign.x, sign.y + 14, sign.width, sign.height), Darker(Cream));
        Panel(sign, Cream);
        Label(new Rect(sign.x, sign.y + 4, sign.width, 90), "SHELF SORT", 80, Red, true);
        Label(new Rect(sign.x, sign.y + 88, sign.width, 40), Tf("{0} · OUVERT 24/24", Name(world).ToUpper()), 32, Ink);

        // full-width awning hung from a wooden rail
        float ay = sign.yMax + 25;
        Awning((1080 - 12 * 98) / 2f, ay, 12, 98, awningH);
        GUI.color = new Color(.42f, .28f, .18f);
        GUI.DrawTexture(new Rect(0, ay - 12, 1080, 20), Texture2D.whiteTexture);
        GUI.color = new Color(.6f, .42f, .28f);
        GUI.DrawTexture(new Rect(0, ay - 12, 1080, 6), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // n stripes of width sw alternating awning color / white, each ending in a half-circle scallop
    // with some relief: a soft shadow on the wall, a darker rim under each scallop, a shaded fold on each stripe
    void Awning(float x0, float y, int n, float sw, float h)
    {
        GUI.color = new Color(0, 0, 0, .12f);
        for (int i = 0; i < n; i++) GUI.DrawTexture(new Rect(x0 + i * sw + 4, y + h - sw / 2 + 16, sw - 8, sw), circle);
        for (int i = 0; i < n; i++)
        {
            var c = i % 2 == 0 ? AwningC : Color.white;
            GUI.color = Darker(c);
            GUI.DrawTexture(new Rect(x0 + i * sw, y + h - sw / 2 + 7, sw, sw), circle);
            GUI.color = c;
            GUI.DrawTexture(new Rect(x0 + i * sw, y, sw, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x0 + i * sw, y + h - sw / 2, sw, sw), circle);
            GUI.color = new Color(0, 0, 0, .07f);
            GUI.DrawTexture(new Rect(x0 + i * sw + sw * .65f, y, sw * .35f, h), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    // coins pill: the money that buys more bonuses once a level's free ones are spent
    void CoinsPill(float x, float y)
    {
        Panel(new Rect(x, y, 250, 96), Cream);
        Panel(new Rect(x + 25, y + 18, 60, 60), Gold);
        Label(new Rect(x + 85, y, 150, 96), coins.ToString(), 50, Ink);
    }

    static readonly int[] BonusPrice = { 20, 30, 30 }; // undo, shuffle, +15 s

    void BuyBonus(int b)
    {
        coins -= BonusPrice[b];
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.Save();
        if (b == 0) undoLeft++;
        else if (b == 1) shuffleLeft++;
        else timeLeft++;
        Fx.Play("pick");
    }

    void UseBonus(int b)
    {
        switch (b)
        {
            case 0: undoLeft--; usedBonus = true; shelves = history.Pop(); Render(); break;
            case 1: shuffleLeft--; usedBonus = true; history.Push(Clone(shelves)); DoShuffle(); Render(); StartCoroutine(Resolve()); break;
            case 2: timeLeft--; usedBonus = true; time += 15; total = Mathf.Max(total, time); Toast("+15 s"); break;
        }
    }

    // card pop-in scale over .25 s since `since`, with a small overshoot (ease-out-back). Never 0: a zero
    // scale makes GUI.matrix non-invertible, which Unity rejects with an error.
    static float PopIn(float since)
    {
        float a = Mathf.Clamp01((Time.time - since) / .25f) - 1;
        return Mathf.Max(.01f, 1 + 2.7f * a * a * a + 1.7f * a * a);
    }

    void EndCard(float W, float H, float k)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .55f));

        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(overAt), new Vector2(W / 2 * k, H / 2 * k));

        if (won)
        {
            SummaryCard(W, H, k);
            GUI.matrix = keep;
            return;
        }

        var card = new Rect(90, H / 2 - 480, W - 180, 960);
        Panel(new Rect(card.x, card.y + 18, card.width, card.height), Darker(Cream));
        Panel(card, Cream);
        Label(new Rect(card.x, card.y + 50, card.width, 140), title, 80, Red, true);
        Label(new Rect(card.x + 40, card.y + 200, card.width - 80, 170), subtitle, 50, Ink);

        // ad (once), then retry and back to the level grid
        var rows = new[] { new Rect(card.x + 70, card.y + 420, card.width - 140, 150), new Rect(card.x + 70, card.y + 610, card.width - 140, 130), new Rect(card.x + 70, card.y + 780, card.width - 140, 130) };
        int r = 0;
        if (!adUsed && Button(rows[r++], time <= 0 ? "Pub : +15 secondes" : "Pub : annuler 2 coups", Gold, 46)) ContinueAfterAd();
        if (Button(rows[r++], "Recommencer", adUsed ? Gold : Mist, 46)) StartLevel();
        if (Button(rows[r], daily ? "Accueil" : "Carte", Mist, 46)) LeaveLevel();
        GUI.matrix = keep;
    }
}
