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
    bool hard, busy, over, adUsed, won;
    string title, subtitle, toast;
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

        coins = PlayerPrefs.GetInt("coins", 0);
        world = PlayerPrefs.GetInt("world", 0); // the title screen shows the last played world's products
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
            wr.material = new Material(GraphicsSettings.currentRenderPipeline.defaultParticleMaterial) { color = Color.white };
        }
        if (wallTex) Destroy(wallTex); // only our own previous bake: the material starts with a built-in texture asset
        wallTex = tex;
        wall.GetComponent<Renderer>().material.mainTexture = tex;
    }

    void StartLevel()
    {
        StopAllCoroutines();
        cam.transform.position = CamHome;
        var lv = LevelGen.Make(world * LevelsPerWorld + lvl + 1, rng);
        shelves = lv.Shelves;
        time = total = baseTime = lv.Time;
        hard = lv.Hard;
        par = lv.Par;
        moves = 0;
        locked.Clear();
        dragGo = null;
        dragS = -1;
        busy = over = adUsed = won = usedBonus = false;
        history.Clear();
        undoLeft = shuffleLeft = timeLeft = 1;
        combo = 0;
        lastMatch = -10;
        Render();
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
        foreach (Transform c in root) Destroy(c.gameObject);
        views.Clear();
        heroes.Clear();
        FitCamera();
        if (menu == Menu.Home)
        {
            RenderHome();
            return;
        }
        if (menu is Menu.Worlds or Menu.Levels) return; // map screens: just the wall behind the GUI

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
        go.GetComponent<Renderer>().material.color = c;
        return go;
    }

    // Wraps the model in a pivot at its feet, so every product stands on its slot and fits a 1.04 wide x 1.45 tall box, whatever its native size.
    GameObject Item(int id, Vector3 pos, float scale, bool ghost, string name)
    {
        var model = Instantiate(Model(LevelGen.Id(id)));
        var mat = ghost ? worldGhosts[world] : LevelGen.IsFrozen(id) ? worldIce[world] : worldMats[world];
        return Wrap(model, pos, scale, name, mat);
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
        float k = Mathf.Min(1.04f / b.size.x, 1.45f / b.size.y, 1.04f / b.size.z);
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
        AnimateHome();
        TickStars();
        if (!busy && !over && !Paused)
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
            if (s >= 0 && LevelGen.IsFrozen(shelves[s][0][i]))
            {
                Fx.Play("back");
                StartCoroutine(Fx.Squash(views[(s, i)].transform));
                Toast("Glacé !");
            }
            else if (s >= 0)
            {
                dragS = s;
                dragI = i;
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
                if (shelves[s][0][i] >= 0 || locked.Contains(s)) continue;
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
            if (combo > 1)
            {
                time = Mathf.Min(total, time + 2);
                Toast($"COMBO x{combo}  +2 s");
                StartCoroutine(Fx.Shake(cam.transform, CamHome, .08f + .04f * combo));
            }

            yield return new WaitForSeconds(.08f); // let the trio register before it pops
            var popping = hits.SelectMany(s => Enumerable.Range(0, 3).Select(i => views[(s, i)])).ToList();
            for (float t = 0; t < .3f; t += Time.deltaTime)
            {
                float k = t < .12f ? Mathf.Lerp(1, 1.35f, t / .12f) : Mathf.Lerp(1.35f, 0, (t - .12f) / .18f);
                foreach (var go in popping) go.transform.localScale = Vector3.one * k;
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
            locked.Clear();
        }
        busy = false;

        if (shelves.All(sh => sh.All(r => r.All(x => x < 0)))) Win();
        else if (shelves.All(sh => sh[0].All(x => x >= 0))) Lose("Plus de place !");
    }

    void DoShuffle()
    {
        // frozen products stay put
        var cells = Enumerable.Range(0, shelves.Count).SelectMany(s => Enumerable.Range(0, 3).Select(i => (s, i)))
            .Where(c => !LevelGen.IsFrozen(shelves[c.s][0][c.i])).ToList();
        var vals = LevelGen.Shuffle(cells.Select(c => shelves[c.s][0][c.i]).ToList(), rng);
        for (int k = 0; k < cells.Count; k++) shelves[cells[k].s][0][cells[k].i] = vals[k];
    }

    void Win()
    {
        over = won = true;
        overAt = Time.time;
        ScoreLevel();
        Fx.Play("win");
        Fx.Burst(new Vector3(-2, 1, -2), 40);
        Fx.Burst(new Vector3(2, 1, -2), 40);
    }

    void Lose(string msg)
    {
        if (over) return;
        over = true;
        overAt = Time.time;
        title = msg;
        subtitle = "Tu y étais presque !";
        Fx.Play("lose");
    }

    void ContinueAfterAd()
    {
        // ponytail: fake rewarded ad; plug the ad SDK (LevelPlay/AppLovin) here
        adUsed = true;
        over = false;
        if (time <= 0) { time = 15; total = Mathf.Max(total, 15); }
        else for (int k = 0; k < 2 && history.Count > 0; k++) shelves = history.Pop();
        Render();
    }

    void Toast(string t) { toast = t; toastUntil = Time.time + 1f; }

    // ---------- HUD (IMGUI, laid out on a 1080-wide virtual canvas) ----------
    // ponytail: styled IMGUI is enough to ship a first version; move to UI Toolkit if menus/shop grow

    static readonly Color Ink = new(.14f, .19f, .29f), Gold = new(1f, .76f, .24f), Red = new(.91f, .3f, .27f),
        Cream = new(1f, .98f, .93f), Mist = new(.84f, .9f, .95f);
    Texture2D round, circle;
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
    bool Button(Rect r, string label, Color bg, int size = 46)
    {
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        bool down = GUI.enabled && Input.GetMouseButton(0) && r.Contains(Event.current.mousePosition);
        Panel(new Rect(r.x, r.y + 14, r.width, r.height), Darker(bg));
        var face = down ? new Rect(r.x, r.y + 10, r.width, r.height) : r;
        Panel(face, bg);
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
        if (shopOpen)
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            ShopCard(1080, Screen.height / k, k);
            return; // the shop is modal: nothing underneath may take clicks
        }
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
        float W = 1080, H = Screen.height / k;
        var safe = Screen.safeArea;
        float top = (Screen.height - safe.yMax) / k + 30, bottom = H - safe.y / k - 50;
        switch (menu)
        {
            case Menu.Home: HomeScreen(W, H, k, top - 30, bottom); return;
            case Menu.Worlds: WorldsScreen(W, H, top - 30, bottom); return;
            case Menu.Levels: LevelsScreen(W, H, top - 30, bottom); return;
        }
        bool live = !busy && !over && menu == Menu.None;

        // top bar: level (red on HARD levels), coins, pause
        Panel(new Rect(40, top, 420, 110), hard ? Red : Cream);
        Label(new Rect(40, top, 420, 110), hard ? $"NIVEAU {LevelName} · HARD" : $"NIVEAU {LevelName}", hard ? 42 : 52, hard ? Color.white : Ink);
        CoinsButton(500, top, live);
        GUI.enabled = live;
        if (Button(new Rect(790, top, 250, 96), "Pause", Mist, 42)) OpenMenu(Menu.Pause);
        GUI.enabled = true;

        // timer: blinks red under 10 s
        var bar = new Rect(40, top + 145, W - 80, 64); // >= 60 tall, or the 30px rounded corners overlap
        Panel(bar, new Color(0, 0, 0, .2f));
        bool low = time < 10;
        var fill = low && Mathf.Repeat(Time.time, .5f) < .25f ? Color.white : low ? Red : Gold;
        Panel(new Rect(bar.x, bar.y, Mathf.Max(64, bar.width * time / total), bar.height), fill);
        Label(new Rect(bar.x + 30, bar.y, bar.width / 2, bar.height), $"{Mathf.CeilToInt(time)} s", 40, Color.white, true, TextAnchor.MiddleLeft);
        // moves against the target for the moves star; turns red once over it
        Label(new Rect(bar.center.x, bar.y, bar.width / 2 - 30, bar.height), $"{moves} / {par} coups", 40,
            moves > par ? new Color(1f, .75f, .75f) : Color.white, true, TextAnchor.MiddleRight);

        var boardTop = cam.WorldToScreenPoint(new Vector3(0, BoardTopY + .3f, 0));
        StoreFront(bar.yMax + 30, (Screen.height - boardTop.y) / k - 20);

        // bonuses
        float bw = 300, bh = 150, by = bottom - bh;
        string[] names = { "Annuler", "Mélanger", "+15 s" };
        int[] left = { undoLeft, shuffleLeft, timeLeft };
        for (int b = 0; b < 3; b++)
        {
            float x = 45 + b * (bw + 45);
            GUI.enabled = live && left[b] > 0 && (b != 0 || history.Count > 0);
            if (Button(new Rect(x, by, bw, bh), names[b], Gold)) UseBonus(b);
            Badge(x + bw - 44, by - 22, left[b].ToString());
        }
        GUI.enabled = true;

        if (Time.time < toastUntil)
        {
            float a = 1 - (toastUntil - Time.time); // 0→1 over the toast's life
            Label(new Rect(0, H * .2f - a * 60, W, 140), toast, 84, Gold, true);
        }

        if (over) EndCard(W, H, k);
        if (menu == Menu.Pause) PauseCard(W, H, k);
    }

    // Shop sign + striped awning, centered in the gap between the timer and the cabinets.
    // ponytail: skipped when the board is too tall (3+ rows); shrink it instead if those levels look bare
    void StoreFront(float from, float to)
    {
        const float h = 270, sw = 94, awningH = 60;
        if (to - from < h - 10) return; // FitCamera reserves exactly h, allow rounding
        float y = from + (to - from - h) / 2;

        var sign = new Rect(140, y, 800, 140);
        Panel(new Rect(sign.x, sign.y + 14, sign.width, sign.height), Darker(Cream));
        Panel(sign, Cream);
        Label(new Rect(sign.x, sign.y + 4, sign.width, 90), "SHELF SORT", 80, Red, true);
        Label(new Rect(sign.x, sign.y + 88, sign.width, 40), "SUPÉRETTE · OUVERT 24/24", 32, Ink);

        Awning((1080 - 10 * sw) / 2, sign.yMax + 25, 10, sw, awningH);
    }

    // n stripes of width sw alternating awning color / white, each ending in a half-circle scallop
    void Awning(float x0, float y, int n, float sw, float h)
    {
        for (int i = 0; i < n; i++)
        {
            GUI.color = i % 2 == 0 ? AwningC : Color.white;
            GUI.DrawTexture(new Rect(x0 + i * sw, y, sw, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x0 + i * sw, y + h - sw / 2, sw, sw), circle);
        }
        GUI.color = Color.white;
    }

    // coins pill; opens the renovation shop, shows "!" when something is affordable
    void CoinsButton(float x, float y, bool enabled)
    {
        GUI.enabled = enabled;
        if (Button(new Rect(x, y, 250, 96), "", Cream)) OpenShop();
        GUI.enabled = true;
        Panel(new Rect(x + 25, y + 18, 60, 60), Gold);
        Label(new Rect(x + 85, y, 150, 96), coins.ToString(), 50, Ink);
        if (CanBuyAny) Badge(x + 250 - 44, y - 22, "!");
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
        if (Button(rows[r], "Niveaux", Mist, 46)) OpenMenu(Menu.Levels);
        GUI.matrix = keep;
    }
}
