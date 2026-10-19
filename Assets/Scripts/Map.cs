using System.Collections.Generic;
using UnityEngine;

// Level map: a 3D diorama, built far from the board (MapO) and filmed from above by the main camera. One winding
// road runs through every world, level 1 at the near end. A delivery truck waits at the current level, drives on
// to the next one after a win ("Suivant"), and drives to any open level the player taps before starting it.
// Each world has its own ground and decor and a gate at its start; a locked world's gate is barred.
// The level numbers, stars and signs' text are GUI drawn over the 3D pads (MapScreen).
// t = global level index, fractional between levels; MapPos(t) = the point of the road at t.
public partial class Game
{
    const float NodeGap = 200, WorldGap = 420; // spacing along the road, in map units: a world gate adds a gap
    const float MapScale = 3f / NodeGap; // world units per map unit
    const float MapTilt = 42; // the map camera looks down this steeply
    static readonly Vector3 MapO = new(2000, 0, 0); // well away from the board

    float scrollZ, mapVel, truckT, truckYaw, wheelSpin, playAt, pressMoved;
    int truckGoal, nextLevel, truckWorld = -1;
    bool driveThenPlay, pressing;
    Vector2 lastPress;
    Transform mapRoot, truck3D;
    readonly List<Transform> pads = new();

    // star totals read every level's prefs: computed once when the map opens, not on every GUI event
    readonly bool[] openWorlds = new bool[Worlds.Length];
    int mapStars;

    static int TotalLevels => Worlds.Length * LevelsPerWorld;

    static float MapY(float t)
    {
        float y = t * NodeGap;
        for (int w = 1; w < Worlds.Length; w++) y += WorldGap * Mathf.Clamp01(t - (w * LevelsPerWorld - 1)); // room for the gate
        return y;
    }

    static Vector3 MapPos(float t) => MapO + new Vector3(1.5f * Mathf.Sin(t * .6f), 0, MapY(t) * MapScale);

    static float GateT(int w) => w == 0 ? -1.1f : w * LevelsPerWorld - .5f;

    static float Hash01(int a, int b) => Mathf.Abs(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f) % 1;

    // every world is reachable while testing in the editor
    bool Reachable(int g) => (openWorlds[g / LevelsPerWorld] || Application.isEditor) && LevelOpen(g / LevelsPerWorld, g % LevelsPerWorld);

    // the truck parks just short of its level's pad, so the pad and its number stay in view
    Vector3 TruckPos(float t) => MapPos(t);

    // a level's pad stands beside the road: the truck parks right next to it
    static Vector3 PadPos(int g)
    {
        var d = MapPos(g + .05f) - MapPos(g - .05f);
        var right = new Vector3(d.z, 0, -d.x).normalized;
        return MapPos(g) + right * (g % 2 == 0 ? 1.3f : -1.3f); // alternating sides: neighbors never crowd
    }

    float CenterZ(float t) => MapPos(t).z + 1.2f; // scroll that keeps t a little below the middle of the screen

    void EnterMap()
    {
        mapStars = TotalStars;
        for (int w = 0; w < Worlds.Length; w++) openWorlds[w] = mapStars >= Worlds[w].need;
        int cur = world * LevelsPerWorld + lvl;
        truckT = truckGoal = cur;
        driveThenPlay = false;
        playAt = 0;
        // after a win the truck moves on by itself
        if (Done(world, lvl) && cur + 1 < TotalLevels && Reachable(cur + 1)) truckGoal = cur + 1;
        nextLevel = -1; // first open level not finished yet: its pad gently pulses
        for (int g = 0; g < TotalLevels && nextLevel < 0; g++)
            if (Reachable(g) && !Done(g / LevelsPerWorld, g % LevelsPerWorld)) nextLevel = g;
        mapVel = 0;
        scrollZ = CenterZ(truckT);
        var ahead = TruckPos(truckT + .05f) - TruckPos(truckT);
        truckYaw = Mathf.Atan2(-ahead.z, ahead.x) * Mathf.Rad2Deg;
    }

    // ---------- 3D scene, rebuilt by Render() when the map opens ----------

    void BuildMap()
    {
        mapRoot = new GameObject("Map").transform;
        mapRoot.SetParent(root);
        pads.Clear();
        truck3D = null;
        truckWorld = -1;

        // ground: one slab per world, from its gate to the next
        for (int w = 0; w < Worlds.Length; w++)
        {
            float z0 = w == 0 ? MapPos(-6).z : MapPos(GateT(w)).z, z1 = w == Worlds.Length - 1 ? MapPos(TotalLevels + 8).z : MapPos(GateT(w + 1)).z;
            Products.Place(mapRoot, Products.Cube, Products.Ground(w), new Vector3(MapO.x, -.1f, (z0 + z1) / 2), default, new Vector3(30, .2f, z1 - z0), .1f);
        }

        // road: white kerbs, asphalt, dashed middle line
        var pts = new List<Vector3>();
        for (float t = -3; t <= TotalLevels + .5f; t += .05f) pts.Add(MapPos(t));
        Vector3 Up(float y) => new(0, y, 0);
        Products.Place(mapRoot, Products.Ribbon(pts, 1.35f), C2("#fbf8f0"), Up(.01f));
        Products.Place(mapRoot, Products.Ribbon(pts, 1.1f), C2("#5a5f6c"), Up(.02f), smooth: .15f);
        Products.Place(mapRoot, Products.Ribbon(pts, .09f, i => i % 6 < 3), Color.white, Up(.03f));

        // level pads
        for (int g = 0; g < TotalLevels; g++)
        {
            int w = g / LevelsPerWorld, l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0; // every 5th level is HARD (LevelGen.Generate)
            var c = !open ? new Color(.72f, .76f, .82f) : tough ? Red : Done(w, l) ? Gold : Cream;
            var pad = Products.Pad(c).transform;
            pad.SetParent(mapRoot);
            pad.position = PadPos(g);
            Products.Place(mapRoot, Products.Ribbon(new[] { MapPos(g), PadPos(g) }, .55f), C2("#fbf8f0"), new Vector3(0, .015f, 0)); // driveway
            pads.Add(pad);
        }

        // world gates, barred while the world is locked
        for (int w = 0; w < Worlds.Length; w++)
        {
            var gate = Products.Gate(Worlds[w].color, MapTilt).transform;
            gate.SetParent(mapRoot);
            gate.position = new Vector3(MapPos(GateT(w)).x, 0, MapPos(GateT(w)).z);
            if (openWorlds[w]) continue;
            var bar = Products.Barrier().transform;
            bar.SetParent(mapRoot);
            bar.position = gate.position + new Vector3(0, 0, -.5f);
        }

        // decor on both sides, kept clear of the road
        for (float t = -4; t < TotalLevels + 6; t += .3f)
            for (int side = 0; side < 2; side++)
            {
                int k = Mathf.RoundToInt(t * 10);
                float r = Hash01(k, side);
                if (r < .35f) continue;
                var p = MapPos(t);
                float x = MapO.x + (side == 0 ? -1 : 1) * (2.4f + 1.6f * Hash01(k, side + 7));
                if (Mathf.Abs(x - p.x) < 2.4f || Mathf.Abs(x - MapPos(t + .4f).x) < 2.4f || Mathf.Abs(x - MapPos(t - .4f).x) < 2.4f) continue;
                int near = Mathf.Clamp(Mathf.RoundToInt(t), 0, TotalLevels - 1);
                if (Vector2.Distance(new Vector2(x, p.z), new Vector2(PadPos(near).x, PadPos(near).z)) < 1.5f) continue; // clear of the pads
                int w = Mathf.Clamp(Mathf.FloorToInt((t + .5f) / LevelsPerWorld), 0, Worlds.Length - 1);
                var d = Products.Decor(w, Hash01(k, side + 3)).transform;
                d.SetParent(mapRoot);
                d.position = new Vector3(x, 0, p.z + (Hash01(k, side + 5) - .5f) * .6f);
                d.rotation = Quaternion.Euler(0, Hash01(k, side + 9) * 360, 0);
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
            PlayLevel(truckGoal / LevelsPerWorld, truckGoal % LevelsPerWorld);
            return;
        }

        bool moving = truckT != truckGoal, busyTruck = moving || playAt > 0;
        if (moving)
        {
            float d = truckGoal - truckT;
            float speed = Mathf.Max(1.6f, Mathf.Abs(d) * 2.5f); // levels per second: long trips go faster
            truckT = Mathf.MoveTowards(truckT, truckGoal, speed * dt);
            wheelSpin += speed * 2.2f / (2 * Mathf.PI * .2f) * 360 * dt; // roll without sliding
            var ahead = (TruckPos(truckT + .05f * Mathf.Sign(d)) - TruckPos(truckT)).normalized;
            truckYaw = Mathf.LerpAngle(truckYaw, Mathf.Atan2(-ahead.z, ahead.x) * Mathf.Rad2Deg, 12 * dt);
            scrollZ = Mathf.Lerp(scrollZ, CenterZ(truckT), 6 * dt);
            if (truckT == truckGoal)
            {
                Fx.Play("drop");
                if (driveThenPlay) playAt = Time.time + .35f;
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
            if (menu != Menu.Map) return; // the tap started a level: leave the camera to the board
        }
        else if (!busyTruck)
        {
            scrollZ += mapVel * dt;
            mapVel *= Mathf.Pow(.04f, dt);
        }
        scrollZ = Mathf.Clamp(scrollZ, CenterZ(0), CenterZ(TotalLevels - 1));

        // camera
        var rot = Quaternion.Euler(MapTilt, 0, 0);
        cam.transform.SetPositionAndRotation(new Vector3(MapO.x, 0, scrollZ) - rot * Vector3.forward * 15, rot);
        cam.orthographicSize = 3.8f / cam.aspect;

        // the next level to play breathes
        for (int g = 0; g < pads.Count; g++)
        {
            float s = g == nextLevel ? 1 + .07f * Mathf.Sin(Time.time * 5) : 1;
            pads[g].localScale = new Vector3(s, 1, s);
        }

        // truck: rebuilt with the color of the world it drives through
        int tw = Mathf.Clamp((int)(truckT + .5f) / LevelsPerWorld, 0, Worlds.Length - 1);
        if (!truck3D || truckWorld != tw)
        {
            if (truck3D) Destroy(truck3D.gameObject);
            truck3D = Products.Truck(Worlds[tw].color).transform;
            truck3D.SetParent(mapRoot);
            truck3D.localScale = Vector3.one * .6f;
            truckWorld = tw;
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
        if (p.y < SafeTopPx() + 150 || p.y > Screen.height / GuiScale - SafeBottomPx() - 230) return; // header and the JOUER button
        for (int g = 0; g < TotalLevels; g++)
        {
            if (Vector2.Distance(p, ToGui(PadPos(g) + new Vector3(0, .32f, 0))) > 90) continue;
            if (!Reachable(g))
            {
                Fx.Play("back");
                return;
            }
            Fx.Play("pick");
            if (g == (int)truckT) PlayLevel(g / LevelsPerWorld, g % LevelsPerWorld);
            else
            {
                truckGoal = g;
                driveThenPlay = true;
                mapVel = 0;
            }
            return;
        }
    }

    // ---------- GUI over the 3D map ----------

    void MapScreen(float W, float H, float safeTop, float bottom)
    {
        if (!mapRoot) return;

        // world names on the gates' boards, lock condition in front of a barred gate
        for (int w = 0; w < Worlds.Length; w++)
        {
            var board = ToGui(new Vector3(MapPos(GateT(w)).x, 2.05f, MapPos(GateT(w)).z));
            if (board.y < -200 || board.y > H + 200) continue;
            Label(new Rect(board.x - 200, board.y - 62, 400, 50), $"MONDE {w + 1}", 34, Color.white, true);
            Label(new Rect(board.x - 200, board.y - 18, 400, 70), Worlds[w].name.ToUpper(), 52, Color.white, true);
            if (openWorlds[w]) continue;
            var at = ToGui(new Vector3(MapPos(GateT(w)).x, 0, MapPos(GateT(w)).z - 1.4f));
            var lockTag = new Rect(at.x - 210, at.y - 40, 420, 80);
            Panel(lockTag, Ink);
            Star(new Rect(lockTag.x + 22, lockTag.y + 12, 56, 56), true);
            Label(new Rect(lockTag.x + 85, lockTag.y, lockTag.width - 100, 80), $"{Worlds[w].need} pour ouvrir", 38, Color.white);
        }

        // numbers on the pads, "DUR" above hard ones, stars under finished ones
        for (int g = 0; g < TotalLevels; g++)
        {
            var c = ToGui(PadPos(g) + new Vector3(0, .32f, 0));
            if (c.y < -150 || c.y > H + 150) continue;
            int w = g / LevelsPerWorld, l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0;
            Label(new Rect(c.x - 80, c.y - 45, 160, 90), (l + 1).ToString(), 62,
                !open ? new Color(.5f, .55f, .62f) : tough ? Color.white : Ink, open && tough);
            if (tough)
            {
                var tag = new Rect(c.x - 50, c.y - 112, 100, 44);
                Panel(tag, Ink);
                Label(tag, "DUR", 28, Color.white);
            }
            if (!Done(w, l)) continue;
            var pill = new Rect(c.x - 96, c.y + 62, 192, 64); // stars on a dark pill, so gold reads on any ground
            Panel(pill, new Color(Ink.r, Ink.g, Ink.b, .85f));
            for (int k = 0; k < 3; k++)
            {
                var sr = new Rect(pill.x + 10 + k * 58, pill.y + 4, 56, 56);
                if (k < Stars(w, l)) Star(sr, true);
                else
                {
                    GUI.color = new Color(1, 1, 1, .22f);
                    GUI.DrawTexture(sr, star ??= Fx.Star(128));
                    GUI.color = Color.white;
                }
            }
        }

        // "you are here": a bouncing tag over the next level to play, and a big button that drives there and starts it
        if (nextLevel >= 0)
        {
            var c = ToGui(PadPos(nextLevel) + new Vector3(0, .32f, 0));
            float hop = 14 * Mathf.Abs(Mathf.Sin(Time.time * 4));
            var tag = new Rect(c.x - 85, c.y - ((nextLevel % LevelsPerWorld + 1) % 5 == 0 ? 210 : 160) - hop, 170, 70);
            Panel(new Rect(tag.x, tag.y + 8, tag.width, tag.height), Darker(Gold));
            Panel(tag, Gold);
            Label(tag, "À TOI !", 36, Ink);

            var play = new Rect(140, bottom - 160, W - 280, 150);
            bool busyTruck = truckT != truckGoal || playAt > 0;
            GUI.enabled = !busyTruck;
            if (Button(play, $"JOUER  {nextLevel / LevelsPerWorld + 1}-{nextLevel % LevelsPerWorld + 1}", Gold, 60))
            {
                Fx.Play("pick");
                if (nextLevel == (int)truckT && truckT == truckGoal) PlayLevel(nextLevel / LevelsPerWorld, nextLevel % LevelsPerWorld);
                else
                {
                    truckGoal = nextLevel;
                    driveThenPlay = true;
                    mapVel = 0;
                }
            }
            GUI.enabled = true;
        }

        // header: back and star count, no awning: the map has its own scenery
        float hy = safeTop + 40;
        if (Button(new Rect(40, hy, 250, 96), "Retour", Mist, 40)) OpenMenu(Menu.Home);
        StarCount(W - 290, hy, mapStars);
    }
}
