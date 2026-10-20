using UnityEngine;

// World map: a 3D globe in a starry sky, far from the board (GlobeO), turned to face the current country where the
// plane is parked. Each country has a flag pin at its real place; the countries are listed as cards under the globe,
// and tapping one flies there: the plane follows the great circle while the globe turns under it, then that
// country's map opens with the truck at its first unfinished level.
public partial class Game
{
    static readonly Vector3 GlobeO = new(-2000, 0, 0);
    const float GlobeR = 2.6f, FlightTime = 2.4f;
    // each country's pin: latitude, longitude
    static readonly Vector2[] Places = { new(46.5f, 2.5f), new(42.5f, 12.5f), new(36f, 138f), new(39f, -98f) };
    // where each pole leans, in degrees of latitude/longitude (not the current country's): France and Italy are
    // neighbours, France's flag leans north and Italy's east so neither hides the other, seen from anywhere
    static readonly Vector2[] Lean = { new(25, 0), new(0, 35), default, default };

    Transform globe, plane3D;
    Transform[] route; // the flight's dotted trail on the globe
    readonly RenderTexture[] landmarkIcons = new RenderTexture[Places.Length]; // each country card's monument
    readonly Transform[] pins = new Transform[Places.Length];
    float flyAt = -1;
    int flyFrom, flyTo;
    Quaternion flyStartRot;
    CameraClearFlags camFlags;
    Color camBg;

    // a point of the globe (unit length) from latitude/longitude; (0, 0) faces the camera
    static Vector3 Dir(float lat, float lon)
    {
        float a = lat * Mathf.Deg2Rad, o = lon * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * Mathf.Sin(o), Mathf.Sin(a), -Mathf.Cos(a) * Mathf.Cos(o));
    }

    // globe rotation that brings (lat, lon) to the front, north up
    static Quaternion Facing(float lat, float lon) => Quaternion.Euler(-lat, 0, 0) * Quaternion.Euler(0, lon, 0);

    // how the globe is shown around (lat, lon): that point a little above the middle, so its pin and the plane are
    // seen three-quarters, standing up, rather than head-on
    static Quaternion View(float lat, float lon) => Facing(lat - 25, lon);

    // where the plane parks in country w, just south of its flag (a point of the unit globe), and how it sits there in
    // the globe's frame: belly down, nose north (the model's nose is +Y, its top -Z)
    static Vector3 Parking(int w) => Dir(Places[w].x - 6, Places[w].y);
    static Quaternion ParkRot(int w)
    {
        var d = Parking(w);
        return Quaternion.LookRotation(-d, Vector3.Cross(d, Vector3.Cross(Vector3.up, d).normalized));
    }

    void OpenWorldMap(int flyToWorld)
    {
        RefreshStars();
        flyFrom = mapWorld;
        flyAt = -1;
        if (flyToWorld >= 0 && flyToWorld != mapWorld)
        {
            flyTo = flyToWorld;
            flyAt = Time.time + .6f; // a beat to see where we are first
            flyStartRot = View(Places[mapWorld].x, Places[mapWorld].y);
        }
        OpenMenu(Menu.World);
    }

    void Land(int w)
    {
        world = w;
        PlayerPrefs.SetInt("world", w); // the game reopens where the plane last landed
        lvl = FirstToPlay(w);
        flyAt = -1;
        OpenMenu(Menu.Map);
    }

    // ---------- 3D: the globe, the pins, the plane, the stars; rebuilt by Render() ----------

    void BuildGlobe()
    {
        var space = new GameObject("World").transform;
        space.SetParent(root);
        globe = Products.Globe(GlobeR).transform;
        globe.SetParent(space);
        globe.position = GlobeO;
        for (int w = 0; w < Worlds.Length; w++)
        {
            var pin = Products.FlagPin(w).transform;
            pin.localScale = Vector3.one * (w == mapWorld ? 2.4f : 1.6f); // big enough to see; ours stands out
            pin.SetParent(globe, false);
            var d = Dir(Places[w].x, Places[w].y);
            pin.localPosition = d * GlobeR;
            pins[w] = pin;
        }
        plane3D = Products.Plane(Worlds[mapWorld].color).transform;
        plane3D.SetParent(space);
        // stars far behind the globe
        var starMat = Color.white;
        for (int i = 0; i < 70; i++)
            Products.Place(space, Products.Sphere, starMat, GlobeO + new Vector3((Hash01(i, 1) - .5f) * 16, (Hash01(i, 2) - .5f) * 28, 12),
                default, Vector3.one * (.03f + .05f * Hash01(i, 3)), .9f);
        UpdateWorld();
    }

    void UpdateWorld()
    {
        if (menu != Menu.World || !globe) return;
        for (int w = 0; w < Worlds.Length; w++) // the cards' monument pictures, taken once
            if (!landmarkIcons[w])
            {
                var model = Products.Landmark(w);
                model.transform.SetPositionAndRotation(StageO + new Vector3(40 * w, 80, 0), Quaternion.Euler(0, w == 1 ? 0 : 20, 0));
                landmarkIcons[w] = Snapshot(model);
            }
        if (camFlags == 0) (camFlags, camBg) = (cam.clearFlags, cam.backgroundColor); // restored when the globe closes
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.05f, .07f, .16f);
        cam.transform.SetPositionAndRotation(GlobeO + new Vector3(0, -.5f, -20), Quaternion.identity);

        float size = 4.7f; // close on our country, the globe overflowing the width a little
        if (flyAt >= 0 && Time.time >= flyAt)
        {
            // flight: from parking spot to parking spot along the great circle, the globe turning under the plane;
            // it climbs and grows mid-flight, belly to the globe and nose along the route, a dotted trail behind it
            float t = Mathf.Clamp01((Time.time - flyAt) / FlightTime), e = Mathf.SmoothStep(0, 1, t);
            Vector3 a = Parking(flyFrom), b = Parking(flyTo);
            var here = Vector3.Slerp(a, b, e);
            var follow = View(Mathf.Asin(here.y) * Mathf.Rad2Deg + 6, Mathf.Atan2(here.x, -here.z) * Mathf.Rad2Deg);
            globe.rotation = Quaternion.Slerp(flyStartRot, follow, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .3f)));
            size += 2f * Mathf.Sin(Mathf.PI * t); // pull back mid-flight to see the whole globe
            plane3D.localScale = Vector3.one * (.55f + .3f * Mathf.Sin(Mathf.PI * t));
            float alt = .12f + .6f * Mathf.Sin(Mathf.PI * t);
            var along = Vector3.Cross(Vector3.Cross(a, b).normalized, here); // the way the great circle goes, here
            var cruise = Quaternion.LookRotation(-here, along);
            float turn = Mathf.Max(0, 1 - t / .15f) + Mathf.Max(0, (t - .85f) / .15f); // turning onto the route from the parked heading, and back at the end
            var local = Quaternion.Slerp(cruise, ParkRot(t < .5f ? flyFrom : flyTo), Mathf.SmoothStep(0, 1, turn));
            float bank = 25 * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .15f)) - 25 * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .85f) / .15f)); // leaning into those turns
            plane3D.SetPositionAndRotation(globe.TransformPoint(here * (GlobeR + alt)), globe.rotation * local * Quaternion.Euler(0, bank, 0));
            if (route == null || !route[0]) // the trail: white dots on the ground, showing up behind the plane
            {
                route = new Transform[Mathf.Max(2, Mathf.RoundToInt(Vector3.Angle(a, b) / 3))]; // a dot every 3 degrees, short trips too
                for (int i = 0; i < route.Length; i++)
                {
                    route[i] = Products.Place(globe, Products.Sphere, Color.white, Vector3.Slerp(a, b, (i + .5f) / route.Length) * (GlobeR + .02f), default, Vector3.one * .07f, .2f);
                    route[i].gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < route.Length; i++) route[i].gameObject.SetActive((i + .5f) / route.Length < e - .03f);
            if (t >= 1)
            {
                Fx.Play("win");
                foreach (var dot in route) Destroy(dot.gameObject);
                route = null;
                Land(flyTo);
                return;
            }
        }
        else
        {
            // idle: the current country in view, the plane parked just south of its flag, nose north, breathing a little
            var p = Places[mapWorld];
            globe.rotation = View(p.x, p.y);
            float alt = .12f + .03f * Mathf.Sin(Time.time * 2);
            plane3D.localScale = Vector3.one * .55f;
            plane3D.SetPositionAndRotation(globe.TransformPoint(Parking(mapWorld) * (GlobeR + alt)), globe.rotation * ParkRot(mapWorld));
            if (flyAt >= 0) flyStartRot = globe.rotation;
        }
        cam.orthographicSize = size;
        // each pin stands straight out of the globe and turns on its pole so the flag always faces the player
        for (int w = 0; w < pins.Length; w++)
        {
            var pin = pins[w];
            var p = Places[w] + (w == mapWorld ? default : Lean[w]); // ours stands straight
            var up = globe.rotation * Dir(p.x, p.y);
            pin.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, up), up);
        }
    }

    void WorldScreen(float W, float H, float safeTop, float bottom)
    {
        if (!globe) return;

        // the countries, two by two under the globe; hidden during the flight
        if (flyAt < 0)
        {
            // "you are here": the country's name over its flag
            var top = ToGui(globe.TransformPoint(Dir(Places[mapWorld].x, Places[mapWorld].y) * (GlobeR + 1.1f)));
            var tag = new Rect(top.x - 150, top.y - 120, 300, 70); // clear of the flag below it
            Panel(new Rect(tag.x, tag.y + 8, tag.width, tag.height), Darker(Worlds[mapWorld].color));
            Panel(tag, Worlds[mapWorld].color);
            Label(tag, Worlds[mapWorld].name.ToUpper(), 40, Color.white, true);

            Label(new Rect(0, bottom - 470, W, 60), "Choisis ta destination", 38, new Color(1, 1, 1, .8f));
            for (int w = 0; w < Worlds.Length; w++)
            {
                var r = new Rect(50 + w % 2 * 505, bottom - 390 + w / 2 * 200, 475, 160);
                bool open = openWorlds[w];
                if (Button(r, "", open ? Worlds[w].color : new Color(.45f, .5f, .58f)))
                {
                    if (!open) Fx.Play("back");
                    else if (w == mapWorld) Land(w);
                    else
                    {
                        flyFrom = mapWorld;
                        flyTo = w;
                        flyAt = Time.time;
                        flyStartRot = globe.rotation;
                        Fx.Play("pick");
                    }
                }
                // its flag, its name and stars, its monument standing out of the card's top right corner
                var flag = new Rect(r.x + 26, r.y + 30, 96, 64);
                Fill(new Rect(flag.x - 5, flag.y - 5, flag.width + 10, flag.height + 10), Color.white);
                Flag(flag, w);
                Label(new Rect(r.x + 140, r.y + 14, r.width - 290, 70), Worlds[w].name.ToUpper(), Worlds[w].name.Length > 7 ? 32 : 40, Color.white, true, TextAnchor.MiddleLeft);
                Star(new Rect(r.x + 140, r.y + 92, 40, 40), true);
                Label(new Rect(r.x + 186, r.y + 84, r.width - 330, 56),
                    open ? $"{WorldStars(w)} / {LevelsPerWorld * 3}" : $"{Worlds[w].need} pour ouvrir", 28, Color.white, false, TextAnchor.MiddleLeft);
                if (landmarkIcons[w])
                {
                    GUI.color = open ? Color.white : new Color(.6f, .62f, .68f, .8f);
                    GUI.DrawTexture(new Rect(r.xMax - 180, r.y - 55, 190, 190), landmarkIcons[w]);
                    GUI.color = Color.white;
                }
                if (w == mapWorld)
                {
                    var here = new Rect(r.x + 10, r.y - 30, 130, 56); // top left: the monument has the right
                    Panel(here, Cream);
                    Label(here, "ICI", 30, Ink);
                }
            }
        }

        else // in flight: where to, in that country's colour
        {
            var tag = new Rect(W / 2 - 300, bottom - 300, 600, 110);
            Panel(new Rect(tag.x, tag.y + 10, tag.width, tag.height), Darker(Worlds[flyTo].color));
            Panel(tag, Worlds[flyTo].color);
            Label(tag, $"Direction : {Worlds[flyTo].name}", 48, Color.white, true);
        }

        // header
        float hy = safeTop + 40;
        if (flyAt < 0 && Button(new Rect(40, hy, 250, 96), "Retour", Mist, 40)) Land(mapWorld);
        Label(new Rect(0, hy + 110, W, 110), "LE MONDE", 90, Color.white, true);
        StarCount(W - 290, hy, mapStars);    }

    // the main camera goes back to its own background when the globe closes
    void RestoreCamera()
    {
        if (camFlags == 0) return;
        cam.clearFlags = camFlags;
        cam.backgroundColor = camBg;
    }

    // country w's flag, flat in r: the French and Italian tricolours, the Japanese red sun, the Stars and Stripes
    void Flag(Rect r, int w)
    {
        static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        if (w <= 1)
        {
            string[] bands = w == 0 ? new[] { "#0055a4", "#ffffff", "#ef4135" } : new[] { "#009246", "#ffffff", "#ce2b37" };
            for (int k = 0; k < 3; k++) Fill(new Rect(r.x + k * r.width / 3, r.y, r.width / 3 + 1, r.height), C(bands[k]));
        }
        else if (w == 2)
        {
            Fill(r, Color.white);
            GUI.color = C("#bc002d");
            float d = r.height * .6f;
            GUI.DrawTexture(new Rect(r.center.x - d / 2, r.center.y - d / 2, d, d), circle);
            GUI.color = Color.white;
        }
        else
        {
            for (int k = 0; k < 7; k++) Fill(new Rect(r.x, r.y + k * r.height / 7, r.width, r.height / 7 + 1), k % 2 == 0 ? C("#b22234") : Color.white);
            var canton = new Rect(r.x, r.y, r.width * .42f, r.height * 4 / 7);
            Fill(canton, C("#3c3b6e"));
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3; j++) Fill(new Rect(canton.x + 5 + i * 9, canton.y + 5 + j * 11, 4, 4), Color.white);
        }
    }
}
