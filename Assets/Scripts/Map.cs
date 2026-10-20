using System.Collections.Generic;
using UnityEngine;

// Country map: a 3D diorama of one country (mapWorld), built far from the board (MapO) and filmed from above by the
// main camera. Its road sets off from the airport where the player landed, passes the country's gate, runs past its
// 20 shops (one per level) through the country's own ground and decor, and ends on a round plaza around its monument,
// wrapped like a present until all 20 levels are done (then unveiled once, with a reward). The next country is
// reached from the globe (WorldMap.cs).
// A delivery truck waits at the current level, drives on to the next one after a win, and drives to any open level
// the player taps before starting it. Level numbers, stars and signs' text are GUI drawn over the 3D scene.
// t = global level index (world * 20 + level), fractional between levels; MapPos(t) = the point of the road at t.
public partial class Game
{
    const float NodeGap = 3; // road length between two levels, in world units
    const float MapTilt = 42; // the map camera looks down this steeply
    static readonly Vector3 MapO = new(2000, 0, 0); // well away from the board

    int mapWorld; // the country shown
    float scrollZ, mapVel, truckT, truckGoal, truckYaw, wheelSpin, playAt, pressMoved, tripFrom, tripStart, tripDur = 1;
    int nextLevel, truckWorld = -1;
    bool driveThenPlay, pressing;
    Vector2 lastPress;
    Transform mapRoot, truck3D;
    readonly List<Transform> pads = new();
    readonly List<(Transform t, int l)> chests3D = new(); // chests still to open, beside their shops (Chest.cs)
    Transform landmark3D; // the monument on the plaza, wrapped until the country is finished
    Vector3 landmarkScale;
    float revealAt = -10; // when the monument was just unveiled: it pops up, a banner shows the reward
    const int RevealCoins = 200;

    // star totals read every level's prefs: computed when a map opens, not on every GUI event
    readonly bool[] openWorlds = new bool[Worlds.Length];
    int mapStars;

    static int TotalLevels => Worlds.Length * LevelsPerWorld;

    static Vector3 MapPos(float t) => MapO + new Vector3(1.1f * Mathf.Sin(t * .6f), 0, t * NodeGap);

    // this country's stretch of road: gate a little before level 1, the monument's plaza a little after level 20
    float First => mapWorld * LevelsPerWorld;
    float Last => First + LevelsPerWorld - 1;
    float GateT => First - 1.1f;
    float StartT => First - 3.2f; // the airport where the player lands, before the gate
    float PlazaT => Last + 2.2f;
    const float PlazaR = 2.3f;
    bool LastCountry => mapWorld == Worlds.Length - 1;

    static float Hash01(int a, int b) => Mathf.Abs(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f) % 1;

    // every country and every level is open while testing in the editor (see also openWorlds)
    bool Reachable(int g) => openWorlds[g / LevelsPerWorld] && (LevelOpen(g / LevelsPerWorld, g % LevelsPerWorld) || Application.isEditor);

    void RefreshStars()
    {
        mapStars = TotalStars;
        for (int w = 0; w < Worlds.Length; w++) openWorlds[w] = mapStars >= Worlds[w].need || Application.isEditor;
    }

    Vector3 TruckPos(float t) => MapPos(t);

    // a level's shop stands beside the road, alternating sides: the truck parks right next to it
    static Vector3 PadPos(int g)
    {
        var d = MapPos(g + .05f) - MapPos(g - .05f);
        var right = new Vector3(d.z, 0, -d.x).normalized;
        return MapPos(g) + right * (g % 2 == 0 ? 1.95f : -1.95f);
    }

    float CenterZ(float t) => MapPos(t).z + 1.2f; // scroll that keeps t a little below the middle of the screen

    // first level of country w not finished yet (its last level when all are done)
    int FirstToPlay(int w)
    {
        for (int l = 0; l < LevelsPerWorld; l++)
            if (!Done(w, l)) return l;
        return LevelsPerWorld - 1;
    }

    static bool Completed(int w)
    {
        for (int l = 0; l < LevelsPerWorld; l++)
            if (!Done(w, l)) return false;
        return true;
    }

    void EnterMap()
    {
        RefreshStars();
        mapWorld = world;
        int cur = world * LevelsPerWorld + lvl;
        truckT = truckGoal = cur;
        driveThenPlay = false;
        playAt = 0;
        // after a win the truck moves on by itself to the next shop
        if (Done(world, lvl) && lvl < LevelsPerWorld - 1 && Reachable(cur + 1)) DriveTo(cur + 1, false);
        nextLevel = -1; // first open level of this country not finished yet: its shop gently pulses
        for (int g = (int)First; g <= Last && nextLevel < 0; g++)
            if (Reachable(g) && !Done(g / LevelsPerWorld, g % LevelsPerWorld)) nextLevel = g;
        mapVel = 0;
        scrollZ = CenterZ(truckT);
        var ahead = TruckPos(truckT + .05f) - TruckPos(truckT);
        truckYaw = Mathf.Atan2(-ahead.z, ahead.x) * Mathf.Rad2Deg;
        OfferChest(); // a chest level just won: the chest card goes up
        // the country just finished: unveil its monument once, with a reward, the camera on the plaza
        if (Completed(world) && PlayerPrefs.GetInt($"revealed{world}", 0) == 0)
        {
            PlayerPrefs.SetInt($"revealed{world}", 1);
            coins += RevealCoins;
            PlayerPrefs.SetInt("coins", coins);
            PlayerPrefs.Save();
            revealAt = Time.time;
            scrollZ = CenterZ(PlazaT);
            Fx.Play("win");
        }
    }

    // ---------- 3D scene, rebuilt by Render() when the map opens ----------

    void BuildMap()
    {
        mapRoot = new GameObject("Map").transform;
        mapRoot.SetParent(root);
        pads.Clear();
        chests3D.Clear();
        truck3D = null;
        truckWorld = -1;
        int w = mapWorld;
        Vector3 Up(float y) => new(0, y, 0);
        var plaza = MapPos(PlazaT);

        // ground, painted with the country's own land (Products.GroundTexture)
        float z0 = MapPos(StartT - 5).z, z1 = MapPos(PlazaT + 7).z;
        var ground = Products.Place(mapRoot, Products.Cube, Color.white, new Vector3(MapO.x, -.1f, (z0 + z1) / 2), default, new Vector3(30, .2f, z1 - z0), .1f);
        ground.GetComponent<Renderer>().material = Products.GroundMat(w, 30, z1 - z0);

        // France: the Seine running along the left between stone quays, at a steady distance from the road
        if (w == 0)
        {
            var river = new List<Vector3>();
            for (float z = z0; z <= z1; z += .5f) river.Add(new Vector3(MapPos(z / NodeGap).x - 4.4f, 0, z)); // bending with the road, clear of the shops
            Products.Place(mapRoot, Products.Ribbon(river, 2.1f), C2("#d6ccb8"), Up(.004f), smooth: .1f);
            Products.Place(mapRoot, Products.Ribbon(river, 1.7f), C2("#5fa3d6"), Up(.008f), smooth: .8f);
        }

        // road: sidewalks in the country's paving, white kerbs, asphalt, dashed middle line; it runs into the plaza
        var pts = new List<Vector3>();
        for (float t = StartT; t <= PlazaT - .4f; t += .05f) pts.Add(MapPos(t));
        string[] paving = { "#e6dccb", "#e8c9a4", "#d3cec2", "#d9d9d6" };
        Products.Place(mapRoot, Products.Ribbon(pts, 2.7f), C2(paving[w]), Up(.005f), smooth: .1f);
        Products.Place(mapRoot, Products.Ribbon(pts, 1.35f), C2("#fbf8f0"), Up(.01f));
        Products.Place(mapRoot, Products.Ribbon(pts, 1.1f), C2("#5a5f6c"), Up(.02f), smooth: .15f);
        Products.Place(mapRoot, Products.Ribbon(pts, .09f, i => i % 6 < 3), Color.white, Up(.03f));

        // one shop per level; the next one to play gets a glowing ring round its pavement (it pulses with the shop)
        for (int g = (int)First; g <= Last; g++)
        {
            int l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0; // every 5th level is HARD (LevelGen.Generate)
            var awning = !open ? new Color(.62f, .66f, .72f) : tough ? (w == 2 ? Ink : Red) : Worlds[w].color; // Japan is already red
            var pad = Products.Shop(w, awning, open).transform;
            pad.SetParent(mapRoot);
            pad.position = PadPos(g);
            Products.Place(mapRoot, Products.Ribbon(new[] { MapPos(g), PadPos(g) }, .55f), C2("#fbf8f0"), Up(.015f)); // driveway
            if (g == nextLevel)
            {
                var ring = Products.Place(pad, Products.Cylinder, Gold, new Vector3(0, .02f, -.1f), default, new Vector3(2f, .02f, 1.75f));
                var glow = ring.GetComponent<Renderer>().material;
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", Gold * .8f);
            }
            if (HasChest(l) && !ChestOpened(w, l)) // a chest on the pavement beside the shop: grey until the level is won
            {
                var chest = Products.Chest(Done(w, l)).transform;
                chest.SetParent(mapRoot);
                var toRoad = (MapPos(g) - PadPos(g)).normalized; // on the pavement between the road and the shop, by its driveway
                chest.SetPositionAndRotation(PadPos(g) + toRoad * .95f + new Vector3(0, .02f, .55f), Quaternion.Euler(0, toRoad.x > 0 ? -20 : 20, 0));
                chest.localScale = Vector3.one * .5f;
                chests3D.Add((chest, l));
            }
            pads.Add(pad);
        }

        // the country's gate at the start of the road
        var gate = Products.Gate(Worlds[w].color, MapTilt).transform;
        gate.SetParent(mapRoot);
        gate.position = new Vector3(MapPos(GateT).x, 0, MapPos(GateT).z);

        // the airport where the player landed, before the gate: the road sets off from its apron
        var port = Products.Airport(Worlds[w].color).transform;
        port.SetParent(mapRoot);
        port.SetPositionAndRotation(new Vector3(MapO.x, 0, MapPos(StartT).z), Quaternion.Euler(0, 180, 0));

        // the end of the road: a round plaza with white kerbs, the country's monument in the middle, bushes round it
        Products.Place(mapRoot, Products.Cylinder, C2("#fbf8f0"), plaza + Up(.004f), default, new Vector3(2 * PlazaR + .2f, .01f, 2 * PlazaR + .2f));
        Products.Place(mapRoot, Products.Cylinder, C2(paving[w]), plaza + Up(.012f), default, new Vector3(2 * PlazaR, .01f, 2 * PlazaR), .1f);
        var lm = Products.Landmark(w).transform;
        lm.SetParent(mapRoot);
        lm.position = plaza + new Vector3(0, 0, .4f);
        if (w == 2) lm.localScale = Vector3.one * .7f; // Fuji is wide
        landmark3D = lm;
        landmarkScale = lm.localScale;
        if (!Completed(w)) // still to win: wrapped up under a tarp with a red ribbon and a bow
        {
            var rs = lm.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var wrap = Products.Wrapped(b.size).transform;
            wrap.SetParent(mapRoot);
            wrap.position = new Vector3(b.center.x, 0, b.center.z);
            lm.gameObject.SetActive(false);
        }
        for (int k = 0; k < 6; k++)
        {
            float a = (k * 60 + 30) * Mathf.Deg2Rad;
            if (Mathf.Sin(a) < -.5f) continue; // the road comes in at the front
            Products.Place(mapRoot, Products.Sphere, C2("#5aa84a"), plaza + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (PlazaR - .25f) + Up(.2f), default,
                new Vector3(.5f, .4f, .5f), .25f);
        }

        // Decor: each piece is measured once built (the circle round its footprint) and kept only if that circle stays
        // clear of the road and its pavements, the shops, the Seine, the plaza, the gate, the airport and the pieces
        // already placed; otherwise it is thrown away.
        var placed = new List<(Vector2 c, float r)>();
        float airZ = MapPos(StartT).z;
        float RoadX(float z) => MapPos((z - MapO.z) / NodeGap).x;
        bool Clear(Vector2 c, float r, float h)
        {
            if (c.y - r < airZ + .7f && c.y + r > airZ - 3.9f) return false; // the airport's band
            if (c.y + r > airZ && c.y - r < plaza.z)
                for (float dz = -r; dz <= r; dz += r)
                    if (Mathf.Abs(c.x - RoadX(c.y + dz)) < 1.4f + r) return false;
            foreach (var pad in pads)
            {
                var s = new Vector2(pad.position.x, pad.position.z);
                if (Vector2.Distance(c, s) < 1.1f + r) return false;
                // the camera looks up the map at MapTilt: something tall in front of a shop hides it (its top lands on
                // the screen where the ground h / tan(MapTilt) further back does), with its sign and stars
                if (Mathf.Abs(c.x - s.x) < 1f + r && s.y > c.y && s.y - c.y < h / Mathf.Tan(MapTilt * Mathf.Deg2Rad) + 1f) return false;
            }
            if (w == 0 && c.x - r < RoadX(c.y) - 4.4f + 1.1f) return false; // the Seine and its quay
            if (Vector2.Distance(c, new Vector2(plaza.x, plaza.z)) < PlazaR + .15f + r) return false;
            if (Vector2.Distance(c, new Vector2(MapPos(GateT).x, MapPos(GateT).z)) < 1.6f + r) return false;
            foreach (var (pc, pr) in placed)
                if (Vector2.Distance(c, pc) < r + pr - .05f) return false;
            return true;
        }
        void TryDecor(float variant, float x, float z, float yaw)
        {
            var d = Products.Decor(w, variant).transform;
            d.SetParent(mapRoot);
            d.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
            var rs = d.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var rr in rs) b.Encapsulate(rr.bounds);
            var c = new Vector2(b.center.x, b.center.z);
            float r = Mathf.Max(b.extents.x, b.extents.z);
            if (Clear(c, r, b.max.y)) placed.Add((c, r));
            else Destroy(d.gameObject);
        }

        // along the road, both sides, packed
        for (float t = First - 6; t < PlazaT; t += .38f) // beyond the plaza: the park below
            for (int side = 0; side < 2; side++)
            {
                int k = Mathf.RoundToInt(t * 10);
                if (Hash01(k, side) < .2f) continue;
                float x = MapO.x + (side == 0 ? -1 : 1) * (2.3f + 1.8f * Hash01(k, side + 7));
                TryDecor(Hash01(k, side + 3), x, MapPos(t).z + (Hash01(k, side + 5) - .5f) * .4f, Hash01(k, side + 9) * 360);
            }

        // behind the plaza, after a stretch of open lawn, a calm park of the country's trees across the whole width
        float trees = new[] { .66f, .6f, .42f, .55f }[w]; // where Decor's tree variants start: plane trees, cypresses, cherries, palms
        for (float z = plaza.z + PlazaR + 2; z < z1 - 1; z += 1.6f)
            for (float x = -3.4f; x <= 3.4f; x += 1.7f)
            {
                int k = Mathf.RoundToInt(z * 7 + x * 3);
                if (Hash01(k, 11) < .45f) continue;
                TryDecor(Mathf.Lerp(trees, .99f, Hash01(k, 12)), MapO.x + x + (Hash01(k, 13) - .5f) * .5f, z + (Hash01(k, 14) - .5f) * .5f, Hash01(k, 15) * 360);
            }
    }

    static Color C2(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    // ---------- update: scrolling, taps, the truck ----------

    void UpdateMap()
    {
        if (menu != Menu.Map || !mapRoot) return;
        float dt = Time.deltaTime;

        // parked at the level it drove to: a short beat, then the level starts
        if (playAt > 0 && Time.time >= playAt)
        {
            playAt = 0;
            driveThenPlay = false;
            int g = Mathf.RoundToInt(truckGoal);
            PlayLevel(g / LevelsPerWorld, g % LevelsPerWorld);
            return;
        }

        bool moving = truckT != truckGoal, busyTruck = moving || playAt > 0;
        if (moving)
        {
            // eased trip: smooth start and stop, duration set by DriveTo
            float before = truckT, dir = Mathf.Sign(truckGoal - tripFrom);
            float p = Mathf.Clamp01((Time.time - tripStart) / tripDur);
            truckT = p >= 1 ? truckGoal : Mathf.Lerp(tripFrom, truckGoal, Mathf.SmoothStep(0, 1, p));
            wheelSpin += Mathf.Abs(truckT - before) * NodeGap / (2 * Mathf.PI * .2f) * 360; // roll without sliding
            var ahead = (TruckPos(truckT + .05f * dir) - TruckPos(truckT)).normalized;
            truckYaw = Mathf.LerpAngle(truckYaw, Mathf.Atan2(-ahead.z, ahead.x) * Mathf.Rad2Deg, 10 * dt);
            if (chestAt <= 0) scrollZ = Mathf.Lerp(scrollZ, CenterZ(truckT), 6 * dt); // the camera stays on a chest being opened
            if (truckT == truckGoal)
            {
                Fx.Play("drop");
                if (driveThenPlay) playAt = Time.time + .6f; // a beat at the door before the level starts
            }
        }

        var pos = (Vector2)Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            pressing = true;
            pressMoved = 0;
            mapVel = 0;
            lastPress = pos;
        }
        else if (pressing && Input.GetMouseButton(0))
        {
            pressMoved += (pos - lastPress).magnitude / GuiScale;
            if (!busyTruck)
            {
                float dz = OnGround(lastPress).z - OnGround(pos).z; // keeps the ground under the finger
                scrollZ += dz;
                mapVel = Mathf.Lerp(mapVel, dz / Mathf.Max(dt, 1e-4f), .5f);
            }
            lastPress = pos;
        }
        else if (pressing)
        {
            pressing = false;
            if (pressMoved < 25 && !busyTruck) TapMap(pos);
            if (menu != Menu.Map) return; // the tap left the map: leave the camera alone
        }
        else if (!busyTruck)
        {
            scrollZ += mapVel * dt;
            mapVel *= Mathf.Pow(.04f, dt);
        }
        scrollZ = Mathf.Clamp(scrollZ, CenterZ(StartT + .5f), CenterZ(PlazaT));

        // camera
        var rot = Quaternion.Euler(MapTilt, 0, 0);
        cam.transform.SetPositionAndRotation(new Vector3(MapO.x, 0, scrollZ) - rot * Vector3.forward * 15, rot);
        cam.orthographicSize = 3.8f / cam.aspect;

        // the monument, just unveiled, pops up
        if (landmark3D && landmark3D.gameObject.activeSelf)
            landmark3D.localScale = landmarkScale * (Time.time - revealAt < 2 ? PopIn(revealAt + .4f) : 1);

        // chests ready to open hop on the spot
        foreach (var (chest, l) in chests3D)
            if (Done(mapWorld, l) && !(chestAt > 0 && l == chestLvl)) chest.position = new Vector3(chest.position.x, .02f + .12f * Mathf.Abs(Mathf.Sin(Time.time * 4 + l)), chest.position.z);

        if (chestAt > 0) UpdateChest();

        // the next level to play breathes
        for (int i = 0; i < pads.Count; i++)
        {
            float s = First + i == nextLevel ? 1 + .07f * Mathf.Sin(Time.time * 5) : 1;
            pads[i].localScale = Vector3.one * s;
        }

        // truck, striped in the country's colour
        if (!truck3D || truckWorld != mapWorld)
        {
            if (truck3D) Destroy(truck3D.gameObject);
            truck3D = Products.Truck(Worlds[mapWorld].color).transform;
            truck3D.SetParent(mapRoot);
            truck3D.localScale = Vector3.one * .6f;
            truckWorld = mapWorld;
        }
        float bob = moving ? .03f * Mathf.Abs(Mathf.Sin(Time.time * 18)) : 0;
        truck3D.SetPositionAndRotation(TruckPos(truckT) + new Vector3(0, bob, 0), Quaternion.Euler(0, truckYaw, 0));
        foreach (Transform part in truck3D)
            if (part.name == "wheel") part.localRotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, -wheelSpin, 0);
    }

    // where a screen point lands on the ground (y = 0)
    Vector3 OnGround(Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        return ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
    }

    // world point -> GUI canvas point
    Vector2 ToGui(Vector3 p)
    {
        var s = cam.WorldToScreenPoint(p);
        return new Vector2(s.x / GuiScale, (Screen.height - s.y) / GuiScale);
    }

    void TapMap(Vector2 screen)
    {
        var p = new Vector2(screen.x / GuiScale, (Screen.height - screen.y) / GuiScale);
        if (chestAt > 0) return; // the chest card is up
        foreach (var (chest, l) in chests3D) // a tap on a chest is the chest's, not its shop's
        {
            if (Vector2.Distance(p, ToGui(chest.position + new Vector3(0, .25f, 0))) > 90) continue;
            if (!Done(mapWorld, l))
            {
                Fx.Play("back");
                return;
            }
            ShowChest(l);
            Fx.Play("pick");
            return;
        }
        if (p.y < SafeTopPx() + 150 || p.y > Screen.height / GuiScale - SafeBottomPx() - 230) return; // header and the bottom button
        for (int g = (int)First; g <= Last; g++)
        {
            if (Vector2.Distance(p, ToGui(PadPos(g) + new Vector3(0, .6f, 0))) > 110) continue;
            if (!Reachable(g))
            {
                Fx.Play("back");
                return;
            }
            Fx.Play("pick");
            DriveTo(g, true);
            return;
        }
    }

    // to the world map, the plane leaving for the next country if it is open
    void TakeOff()
    {
        Fx.Play("pick");
        OpenWorldMap(openWorlds[mapWorld + 1] ? mapWorld + 1 : -1);
    }

    // Sends the truck to t (a level, or a point of the road): an eased trip of about a second nearby, 2.5 s at most
    // from far away. play: start the level once parked (after a short beat, also when the truck is already there).
    void DriveTo(float t, bool play)
    {
        driveThenPlay = play;
        mapVel = 0;
        if (truckT == t)
        {
            if (play) playAt = Time.time + .6f;
            return;
        }
        tripFrom = truckT;
        truckGoal = t;
        tripStart = Time.time;
        tripDur = Mathf.Clamp(.7f + .15f * Mathf.Abs(t - truckT), .9f, 2.5f);
    }

    // ---------- GUI over the 3D map ----------

    void MapScreen(float W, float H, float safeTop, float bottom)
    {
        if (!mapRoot) return;
        if (chestAt > 0)
        {
            ChestCard(W, H, GuiScale);
            return;
        }

        // country name on the gate's board
        var board = ToGui(new Vector3(MapPos(GateT).x, 2.05f, MapPos(GateT).z));
        Label(new Rect(board.x - 200, board.y - 62, 400, 50), $"PAYS {mapWorld + 1}", 34, Color.white, true);
        Label(new Rect(board.x - 200, board.y - 18, 400, 70), Worlds[mapWorld].name.ToUpper(), 52, Color.white, true);

        // a plaque at the plaza's front: the monument's name once the country is finished, until then what unveils it
        var plaque = ToGui(MapPos(PlazaT) + new Vector3(0, 0, -PlazaR + .3f));
        if (plaque.y > -150 && plaque.y < H + 150)
        {
            bool won = Completed(mapWorld);
            var sign = new Rect(plaque.x - 250, plaque.y - 55, 500, won ? 100 : 120);
            Panel(new Rect(sign.x, sign.y + 10, sign.width, sign.height), Darker(won ? Gold : Ink));
            Panel(sign, won ? Gold : Ink);
            if (won) Label(sign, new[] { "TOUR EIFFEL", "COLISÉE", "MONT FUJI", "NEW YORK" }[mapWorld], 46, Ink);
            else
            {
                Label(new Rect(sign.x, sign.y + 8, sign.width, 60), "À DÉBLOQUER", 44, Gold);
                Label(new Rect(sign.x, sign.y + 64, sign.width, 44), $"Finis les {LevelsPerWorld} niveaux", 30, Color.white);
            }
        }

        // just unveiled: the reward, over the top of the screen for a few seconds
        if (Time.time - revealAt < 3.5f)
        {
            var banner = new Rect(W / 2 - 380, safeTop + 170, 760, 190);
            var keep = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(revealAt + .4f), banner.center * GuiScale);
            Panel(new Rect(banner.x, banner.y + 14, banner.width, banner.height), Darker(Gold));
            Panel(banner, Gold);
            Label(new Rect(banner.x, banner.y + 18, banner.width, 90), "PAYS TERMINÉ !", 70, Red, true);
            Label(new Rect(banner.x, banner.y + 112, banner.width, 60), $"+{RevealCoins} pièces", 46, Ink);
            GUI.matrix = keep;
        }

        // numbers on the shops' signs, "DUR" above hard ones, stars under finished ones
        for (int g = (int)First; g <= Last; g++)
        {
            var c = ToGui(PadPos(g) + new Vector3(0, Products.SignY(mapWorld), -.48f)); // the shop's sign
            if (c.y < -150 || c.y > H + 150) continue;
            int l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0;
            Label(new Rect(c.x - 80, c.y - 34, 160, 70), (l + 1).ToString(), 48,
                !open ? new Color(.55f, .6f, .66f) : tough ? Red : Ink);
            if (tough)
            {
                var tag = new Rect(c.x - 50, c.y - 82, 100, 44);
                Panel(tag, Ink);
                Label(tag, "DUR", 28, Color.white);
            }
            if (!Done(mapWorld, l)) continue;
            var front = ToGui(PadPos(g) + new Vector3(0, 0, -.8f)); // the pavement's front edge
            var pill = new Rect(front.x - 96, front.y + 4, 192, 64); // stars on a dark pill, so gold reads on any ground
            Panel(pill, new Color(Ink.r, Ink.g, Ink.b, .85f));
            for (int k = 0; k < 3; k++)
            {
                var sr = new Rect(pill.x + 10 + k * 58, pill.y + 4, 56, 56);
                if (k < Stars(mapWorld, l)) Star(sr, true);
                else
                {
                    GUI.color = new Color(1, 1, 1, .22f);
                    GUI.DrawTexture(sr, star ??= Fx.Star(128));
                    GUI.color = Color.white;
                }
            }
        }

        // "you are here": a bouncing tag over the next level, and the big button: play it, or fly on
        bool busyTruck = truckT != truckGoal || playAt > 0;
        var play = new Rect(140, bottom - 160, W - 280, 150);
        GUI.enabled = !busyTruck;
        if (nextLevel >= 0)
        {
            var c = ToGui(PadPos(nextLevel) + new Vector3(0, Products.SignY(mapWorld), -.48f));
            float hop = 14 * Mathf.Abs(Mathf.Sin(Time.time * 4));
            var tag = new Rect(c.x - 85, c.y - ((nextLevel % LevelsPerWorld + 1) % 5 == 0 ? 150 : 110) - hop, 170, 70);
            Panel(new Rect(tag.x, tag.y + 8, tag.width, tag.height), Darker(Gold));
            Panel(tag, Gold);
            Label(tag, "À TOI !", 36, Ink);
            if (Button(play, $"JOUER  {mapWorld + 1}-{nextLevel % LevelsPerWorld + 1}", Gold, 60))
            {
                Fx.Play("pick");
                DriveTo(nextLevel, true);
            }
        }
        else if (!LastCountry && Button(play, $"VERS {Worlds[mapWorld + 1].name.ToUpper()}", Gold, 56)) TakeOff();
        GUI.enabled = true;

        // header: back, the world map, star count
        float hy = safeTop + 40;
        if (Button(new Rect(40, hy, 250, 96), "Retour", Mist, 40)) OpenMenu(Menu.Home);
        if (Button(new Rect(W / 2 - 125, hy, 250, 96), "Monde", Mist, 40)) OpenWorldMap(-1);
        StarCount(W - 290, hy, mapStars);
    }
}
