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
        if (camFlags == 0) (camFlags, camBg) = (cam.clearFlags, cam.backgroundColor); // restored when the globe closes
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.05f, .07f, .16f);
        cam.transform.SetPositionAndRotation(GlobeO + new Vector3(0, -.5f, -20), Quaternion.identity);

        float size = 4.7f; // close on our country, the globe overflowing the width a little
        if (flyAt >= 0 && Time.time >= flyAt)
        {
            // flight: the plane stays in front, the globe turns under it along the great circle
            float t = Mathf.Clamp01((Time.time - flyAt) / FlightTime), e = Mathf.SmoothStep(0, 1, t);
            Vector3 a = Dir(Places[flyFrom].x, Places[flyFrom].y), b = Dir(Places[flyTo].x, Places[flyTo].y);
            var here = Vector3.Slerp(a, b, e);
            var follow = View(Mathf.Asin(here.y) * Mathf.Rad2Deg, Mathf.Atan2(here.x, -here.z) * Mathf.Rad2Deg);
            globe.rotation = Quaternion.Slerp(flyStartRot, follow, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .3f)));
            size += 2f * Mathf.Sin(Mathf.PI * t); // pull back mid-flight to see the whole globe
            plane3D.localScale = Vector3.one * (.55f + .3f * Mathf.Sin(Mathf.PI * t));
            // heading on screen: where the route goes next, seen through the globe's current rotation
            var next = globe.rotation * Vector3.Slerp(a, b, Mathf.Min(e + .02f, 1)) - globe.rotation * here;
            float heading = Mathf.Atan2(next.x, next.y) * Mathf.Rad2Deg;
            float alt = .3f + .5f * Mathf.Sin(Mathf.PI * t);
            plane3D.SetPositionAndRotation(globe.position + globe.rotation * here * (GlobeR + alt), Quaternion.Euler(0, 0, -heading));
            if (t >= 1)
            {
                Fx.Play("win");
                Land(flyTo);
                return;
            }
        }
        else
        {
            // idle: the current country in view, the plane parked just south of its flag, nose north, breathing a little
            var p = Places[mapWorld];
            globe.rotation = View(p.x, p.y);
            var d = Dir(p.x - 6, p.y);
            var north = Vector3.Cross(d, Vector3.Cross(Vector3.up, d).normalized);
            float alt = .12f + .03f * Mathf.Sin(Time.time * 2);
            plane3D.localScale = Vector3.one * .55f;
            plane3D.SetPositionAndRotation(globe.TransformPoint(d * (GlobeR + alt)), globe.rotation * Quaternion.LookRotation(-d, north));
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
                Label(new Rect(r.x, r.y + 10, r.width, 75), Worlds[w].name.ToUpper(), 42, Color.white, true);
                Star(new Rect(r.x + 120, r.y + 92, 44, 44), true);
                Label(new Rect(r.x + 170, r.y + 84, r.width - 190, 60),
                    open ? $"{WorldStars(w)} / {LevelsPerWorld * 3}" : $"{Worlds[w].need} pour ouvrir", 30, Color.white, false, TextAnchor.MiddleLeft);
                if (w == mapWorld)
                {
                    var here = new Rect(r.xMax - 120, r.y - 26, 130, 56);
                    Panel(here, Cream);
                    Label(here, "ICI", 30, Ink);
                }
            }
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
}
