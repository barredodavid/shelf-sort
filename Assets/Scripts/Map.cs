using System.Collections.Generic;
using UnityEngine;

// Country map: a 3D diorama of one country (mapWorld), built far from the board (MapO) and filmed from above by the
// main camera. Its road starts at the country's gate, by its monument, runs past its 20 shops (one per level) and
// ends at the airport, where the plane leaves for the next country (WorldMap.cs); the last country ends on works.
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

    // star totals read every level's prefs: computed when a map opens, not on every GUI event
    readonly bool[] openWorlds = new bool[Worlds.Length];
    int mapStars;

    static int TotalLevels => Worlds.Length * LevelsPerWorld;

    static Vector3 MapPos(float t) => MapO + new Vector3(1.1f * Mathf.Sin(t * .6f), 0, t * NodeGap);

    // this country's stretch of road: gate a little before level 1, airport a little after level 20
    float First => mapWorld * LevelsPerWorld;
    float Last => First + LevelsPerWorld - 1;
    float GateT => First - 1.1f;
    float AirportT => Last + 1.6f;
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

    void EnterMap()
    {
        RefreshStars();
        mapWorld = world;
        int cur = world * LevelsPerWorld + lvl;
        truckT = truckGoal = cur;
        driveThenPlay = false;
        playAt = 0;
        // after a win the truck moves on by itself: to the next shop, or to the airport after the country's last one
        if (Done(world, lvl))
        {
            if (lvl < LevelsPerWorld - 1 && Reachable(cur + 1)) DriveTo(cur + 1, false);
            else if (lvl == LevelsPerWorld - 1 && !LastCountry) DriveTo(AirportT - .6f, false);
        }
        nextLevel = -1; // first open level of this country not finished yet: its shop gently pulses
        for (int g = (int)First; g <= Last && nextLevel < 0; g++)
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
        int w = mapWorld;
        Vector3 Up(float y) => new(0, y, 0);

        // ground
        float z0 = MapPos(First - 7).z, z1 = MapPos(AirportT + 8).z;
        Products.Place(mapRoot, Products.Cube, Products.Ground(w), new Vector3(MapO.x, -.1f, (z0 + z1) / 2), default, new Vector3(30, .2f, z1 - z0), .1f);

        // road: sidewalks in the country's paving, white kerbs, asphalt, dashed middle line
        var pts = new List<Vector3>();
        for (float t = First - 3; t <= AirportT; t += .05f) pts.Add(MapPos(t));
        string[] paving = { "#e6dccb", "#e8c9a4", "#d3cec2", "#d9d9d6" };
        Products.Place(mapRoot, Products.Ribbon(pts, 2.7f), C2(paving[w]), Up(.005f), smooth: .1f);
        Products.Place(mapRoot, Products.Ribbon(pts, 1.35f), C2("#fbf8f0"), Up(.01f));
        Products.Place(mapRoot, Products.Ribbon(pts, 1.1f), C2("#5a5f6c"), Up(.02f), smooth: .15f);
        Products.Place(mapRoot, Products.Ribbon(pts, .09f, i => i % 6 < 3), Color.white, Up(.03f));

        // one shop per level
        for (int g = (int)First; g <= Last; g++)
        {
            int l = g % LevelsPerWorld;
            bool open = Reachable(g), tough = (l + 1) % 5 == 0; // every 5th level is HARD (LevelGen.Generate)
            var awning = !open ? new Color(.62f, .66f, .72f) : tough ? (w == 2 ? Ink : Red) : Worlds[w].color; // Japan is already red
            var pad = Products.Shop(w, awning, open).transform;
            pad.SetParent(mapRoot);
            pad.position = PadPos(g);
            Products.Place(mapRoot, Products.Ribbon(new[] { MapPos(g), PadPos(g) }, .55f), C2("#fbf8f0"), Up(.015f)); // driveway
            pads.Add(pad);
        }

        // the country's gate, and its monument beside the start of the road
        var gate = Products.Gate(Worlds[w].color, MapTilt).transform;
        gate.SetParent(mapRoot);
        gate.position = new Vector3(MapPos(GateT).x, 0, MapPos(GateT).z);
        var lp = MapPos(First - 2.4f);
        var landmark = new Vector3(MapO.x + (lp.x > MapO.x ? -1 : 1) * 2.6f, 0, lp.z);
        var lm = Products.Landmark(w).transform;
        lm.SetParent(mapRoot);
        lm.position = landmark;
        if (w == 2) lm.localScale = Vector3.one * .65f; // Fuji is wide

        // end of the road: the airport to the next country, or works until the next country ships
        if (!LastCountry)
        {
            var port = Products.Airport(Worlds[w + 1].color).transform;
            port.SetParent(mapRoot);
            port.position = new Vector3(MapO.x, 0, MapPos(AirportT).z);
        }
        else
        {
            var end = Products.Barrier().transform;
            end.SetParent(mapRoot);
            end.position = MapPos(Last + .65f);
            for (int k = 0; k < 3; k++)
            {
                var cone = Products.Cone().transform;
                cone.SetParent(mapRoot);
                cone.position = MapPos(Last + .9f + k * .2f) + new Vector3((k - 1) * .4f, 0, 0);
            }
        }

        // decor on both sides, kept clear of the road, the shops, the monument and the airport
        for (float t = First - 6; t < AirportT + 6; t += .45f)
            for (int side = 0; side < 2; side++)
            {
                int k = Mathf.RoundToInt(t * 10);
                if (Hash01(k, side) < .35f) continue;
                var p = MapPos(t);
                float x = MapO.x + (side == 0 ? -1 : 1) * (2.4f + 1.6f * Hash01(k, side + 7));
                if (Mathf.Abs(x - p.x) < 2.4f || Mathf.Abs(x - MapPos(t + .4f).x) < 2.4f || Mathf.Abs(x - MapPos(t - .4f).x) < 2.4f) continue;
                int near = (int)Mathf.Clamp(Mathf.Round(t), First, Last);
                if (Vector2.Distance(new Vector2(x, p.z), new Vector2(PadPos(near).x, PadPos(near).z)) < 1.9f) continue;
                if (Vector2.Distance(new Vector2(x, p.z), new Vector2(landmark.x, landmark.z)) < 2.8f) continue;
                if (!LastCountry && Mathf.Abs(p.z - MapPos(AirportT).z) < 3.5f) continue;
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
            scrollZ = Mathf.Lerp(scrollZ, CenterZ(truckT), 6 * dt);
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
        scrollZ = Mathf.Clamp(scrollZ, CenterZ(First - 1.5f), CenterZ(LastCountry ? Last + 1 : AirportT - .5f));

        // camera
        var rot = Quaternion.Euler(MapTilt, 0, 0);
        cam.transform.SetPositionAndRotation(new Vector3(MapO.x, 0, scrollZ) - rot * Vector3.forward * 15, rot);
        cam.orthographicSize = 3.8f / cam.aspect;

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
        if (p.y < SafeTopPx() + 150 || p.y > Screen.height / GuiScale - SafeBottomPx() - 230) return; // header and the bottom button
        if (!LastCountry && Vector2.Distance(p, ToGui(new Vector3(MapO.x, .8f, MapPos(AirportT).z))) < 180)
        {
            TakeOff();
            return;
        }
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

        // country name on the gate's board
        var board = ToGui(new Vector3(MapPos(GateT).x, 2.05f, MapPos(GateT).z));
        Label(new Rect(board.x - 200, board.y - 62, 400, 50), $"PAYS {mapWorld + 1}", 34, Color.white, true);
        Label(new Rect(board.x - 200, board.y - 18, 400, 70), Worlds[mapWorld].name.ToUpper(), 52, Color.white, true);

        // airport sign, or the works at the end of the last country
        var endSign = ToGui(MapPos(LastCountry ? Last + 1.15f : AirportT) + new Vector3(0, LastCountry ? 1 : 2.2f, 0));
        if (endSign.y > -150 && endSign.y < H + 150)
        {
            var sign = new Rect(endSign.x - 280, endSign.y - 70, 560, 140);
            Panel(new Rect(sign.x, sign.y + 12, sign.width, sign.height), Darker(Gold));
            Panel(sign, Gold);
            if (LastCountry)
            {
                Label(new Rect(sign.x, sign.y + 10, sign.width, 60), "PROCHAIN PAYS", 48, Ink);
                Label(new Rect(sign.x, sign.y + 70, sign.width, 50), "BIENTÔT !", 40, Red);
            }
            else
            {
                Label(new Rect(sign.x, sign.y + 10, sign.width, 60), "AÉROPORT", 48, Ink);
                Label(new Rect(sign.x, sign.y + 70, sign.width, 50), openWorlds[mapWorld + 1]
                    ? $"Vols pour : {Worlds[mapWorld + 1].name}" : $"{Worlds[mapWorld + 1].name} : {Worlds[mapWorld + 1].need} étoiles", 36, Ink);
            }
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
