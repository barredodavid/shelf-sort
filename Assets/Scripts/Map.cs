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

    static Vector3 MapPos(float t) => MapO + new Vector3(1.1f * Mathf.Sin(t * .6f), 0, MapY(t) * MapScale);

    static float GateT(int w) => w == 0 ? -1.1f : w * LevelsPerWorld - .5f;

    static float Hash01(int a, int b) => Mathf.Abs(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f) % 1;

    // every country and every level is open while testing in the editor (see also openWorlds)
    bool Reachable(int g) => openWorlds[g / LevelsPerWorld] && (LevelOpen(g / LevelsPerWorld, g % LevelsPerWorld) || Application.isEditor);

    // the truck parks just short of its level's pad, so the pad and its number stay in view
    Vector3 TruckPos(float t) => MapPos(t);

    // a level's pad stands beside the road: the truck parks right next to it
    static Vector3 PadPos(int g)
    {
        var d = MapPos(g + .05f) - MapPos(g - .05f);
        var right = new Vector3(d.z, 0, -d.x).normalized;
        return MapPos(g) + right * (g % 2 == 0 ? 1.95f : -1.95f); // alternating sides: neighbors never crowd
    }

    float CenterZ(float t) => MapPos(t).z + 1.2f; // scroll that keeps t a little below the middle of the screen

    void EnterMap()
    {
        mapStars = TotalStars;
        for (int w = 0; w < Worlds.Length; w++) openWorlds[w] = mapStars >= Worlds[w].need || Application.isEditor;
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
        // sidewalks along the road, in each country's paving: Paris stone, terracotta, gravel, concrete
        string[] paving = { "#e6dccb", "#e8c9a4", "#d3cec2", "#d9d9d6" };
        for (int w = 0; w < Worlds.Length; w++)
        {
            float t0 = w == 0 ? -3 : GateT(w), t1 = w == Worlds.Length - 1 ? TotalLevels + .5f : GateT(w + 1);
            var side = new List<Vector3>();
            for (float t = t0; t <= t1 + .01f; t += .05f) side.Add(MapPos(t));
            Products.Place(mapRoot, Products.Ribbon(side, 2.7f), C2(paving[w]), Up(.005f), smooth: .1f);
        }
        Products.Place(mapRoot, Products.Ribbon(pts, 1.35f), C2("#fbf8f0"), Up(.01f));
        Products.Place(mapRoot, Products.Ribbon(pts, 1.1f), C2("#5a5f6c"), Up(.02f), smooth: .15f);
        Products.Place(mapRoot, Products.Ribbon(pts, .09f, i => i % 6 < 3), Color.white, Up(.03f));

        // level pads
        for (int g = 0; g < TotalLevels; g++)
        {
            int w = g / LevelsPerWorld, l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0; // every 5th level is HARD (LevelGen.Generate)
            var awning = !open ? new Color(.62f, .66f, .72f) : tough ? (w == 2 ? Ink : Red) : Worlds[w].color; // Japan is already red
            var pad = Products.Shop(w, awning, open).transform;
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

        // each country's monument, just past its gate (before the start for the first), on the side away from the road
        var landmarks = new List<Vector3>();
        for (int w = 0; w < Worlds.Length; w++)
        {
            float t = w == 0 ? -2.3f : GateT(w) + .1f; // beside the gate: the first shop stands ~4.6 further on
            var p = MapPos(t);
            var at = new Vector3(MapO.x + (p.x > MapO.x ? -1 : 1) * (w == 0 ? 2.5f : 3f), 0, p.z);
            var lm = Products.Landmark(w).transform;
            lm.SetParent(mapRoot);
            lm.position = at;
            if (w == 2) lm.localScale = Vector3.one * .65f; // Fuji is wide
            landmarks.Add(at);
        }

        // end of the road: works in progress until the next country ships
        var end = Products.Barrier().transform;
        end.SetParent(mapRoot);
        end.position = MapPos(TotalLevels - .35f);
        for (int k = 0; k < 3; k++)
        {
            var cone = Products.Cone().transform;
            cone.SetParent(mapRoot);
            cone.position = MapPos(TotalLevels - .1f + k * .2f) + new Vector3((k - 1) * .4f, 0, 0);
        }

        // decor on both sides, kept clear of the road
        for (float t = -4; t < TotalLevels + 6; t += .45f)
            for (int side = 0; side < 2; side++)
            {
                int k = Mathf.RoundToInt(t * 10);
                float r = Hash01(k, side);
                if (r < .35f) continue;
                var p = MapPos(t);
                float x = MapO.x + (side == 0 ? -1 : 1) * (2.4f + 1.6f * Hash01(k, side + 7));
                if (Mathf.Abs(x - p.x) < 2.4f || Mathf.Abs(x - MapPos(t + .4f).x) < 2.4f || Mathf.Abs(x - MapPos(t - .4f).x) < 2.4f) continue;
                int near = Mathf.Clamp(Mathf.RoundToInt(t), 0, TotalLevels - 1);
                if (Vector2.Distance(new Vector2(x, p.z), new Vector2(PadPos(near).x, PadPos(near).z)) < 1.9f) continue; // clear of the pads
                if (landmarks.Exists(m => Vector2.Distance(new Vector2(x, p.z), new Vector2(m.x, m.z)) < 2.8f)) continue; // and of the monuments
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
            pads[g].localScale = Vector3.one * s;
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
            if (Vector2.Distance(p, ToGui(PadPos(g) + new Vector3(0, .6f, 0))) > 110) continue;
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
            Label(new Rect(board.x - 200, board.y - 62, 400, 50), $"PAYS {w + 1}", 34, Color.white, true);
            Label(new Rect(board.x - 200, board.y - 18, 400, 70), Worlds[w].name.ToUpper(), 52, Color.white, true);
            if (openWorlds[w]) continue;
            var at = ToGui(new Vector3(MapPos(GateT(w)).x, 0, MapPos(GateT(w)).z - 1.4f));
            var lockTag = new Rect(at.x - 210, at.y - 40, 420, 80);
            Panel(lockTag, Ink);
            Star(new Rect(lockTag.x + 22, lockTag.y + 12, 56, 56), true);
            Label(new Rect(lockTag.x + 85, lockTag.y, lockTag.width - 100, 80), $"{Worlds[w].need} pour ouvrir", 38, Color.white);
        }

        // the works at the end of the road
        var soon = ToGui(MapPos(TotalLevels + .15f) + new Vector3(0, 1, 0));
        if (soon.y > -150 && soon.y < H + 150)
        {
            var sign = new Rect(soon.x - 280, soon.y - 70, 560, 140);
            Panel(new Rect(sign.x, sign.y + 12, sign.width, sign.height), Darker(Gold));
            Panel(sign, Gold);
            Label(new Rect(sign.x, sign.y + 10, sign.width, 60), "PROCHAIN PAYS", 48, Ink);
            Label(new Rect(sign.x, sign.y + 70, sign.width, 50), "BIENTÔT !", 40, Red);
        }

        // numbers on the shops' signs, "DUR" above hard ones, stars under finished ones
        for (int g = 0; g < TotalLevels; g++)
        {
            var c = ToGui(PadPos(g) + new Vector3(0, Products.SignY(g / LevelsPerWorld), -.48f)); // the shop's sign
            if (c.y < -150 || c.y > H + 150) continue;
            int w = g / LevelsPerWorld, l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0;
            Label(new Rect(c.x - 80, c.y - 34, 160, 70), (l + 1).ToString(), 48,
                !open ? new Color(.55f, .6f, .66f) : tough ? Red : Ink);
            if (tough)
            {
                var tag = new Rect(c.x - 50, c.y - 82, 100, 44);
                Panel(tag, Ink);
                Label(tag, "DUR", 28, Color.white);
            }
            if (!Done(w, l)) continue;
            var front = ToGui(PadPos(g) + new Vector3(0, 0, -.8f)); // the pavement's front edge
            var pill = new Rect(front.x - 96, front.y + 4, 192, 64); // stars on a dark pill, so gold reads on any ground
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
            var c = ToGui(PadPos(nextLevel) + new Vector3(0, Products.SignY(nextLevel / LevelsPerWorld), -.48f));
            float hop = 14 * Mathf.Abs(Mathf.Sin(Time.time * 4));
            var tag = new Rect(c.x - 85, c.y - ((nextLevel % LevelsPerWorld + 1) % 5 == 0 ? 150 : 110) - hop, 170, 70);
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
