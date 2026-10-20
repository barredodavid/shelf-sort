using UnityEngine;

// World map: a 3D globe turning slowly in a starry sky, far from the board (GlobeO). Each country has a flag pin at
// its real place; its name and stars (GUI) follow the pin while it faces the player, and tapping it flies there.
// The plane circles over the current country; a flight carries it along the great circle to another country while
// the globe turns under it, then that country's map opens with the truck at its first unfinished level.
public partial class Game
{
    static readonly Vector3 GlobeO = new(-2000, 0, 0);
    const float GlobeR = 2.6f, FlightTime = 2.4f;
    // each country's pin: latitude, longitude
    static readonly Vector2[] Places = { new(46.5f, 2.5f), new(42.5f, 12.5f), new(36f, 138f), new(39f, -98f) };

    Transform globe, plane3D;
    float globeYaw, flyAt = -1;
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

    void OpenWorldMap(int flyToWorld)
    {
        RefreshStars();
        flyFrom = mapWorld;
        flyAt = -1;
        globeYaw = Places[mapWorld].y;
        if (flyToWorld >= 0 && flyToWorld != mapWorld)
        {
            flyTo = flyToWorld;
            flyAt = Time.time + .6f; // a beat to see where we are first
            flyStartRot = Facing(35, globeYaw);
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
            pin.localScale = Vector3.one * 1.6f; // big enough to see on the globe
            pin.SetParent(globe, false);
            var d = Dir(Places[w].x, Places[w].y);
            pin.localPosition = d * GlobeR;
            pin.localRotation = Quaternion.FromToRotation(Vector3.up, d);
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
        cam.transform.SetPositionAndRotation(GlobeO + new Vector3(0, -.6f, -20), Quaternion.identity);

        float size = 5.4f; // the globe fills most of the width, with sky around
        if (flyAt >= 0 && Time.time >= flyAt)
        {
            // flight: the plane stays in front, the globe turns under it along the great circle
            float t = Mathf.Clamp01((Time.time - flyAt) / FlightTime), e = Mathf.SmoothStep(0, 1, t);
            Vector3 a = Dir(Places[flyFrom].x, Places[flyFrom].y), b = Dir(Places[flyTo].x, Places[flyTo].y);
            var here = Vector3.Slerp(a, b, e);
            var follow = Facing(Mathf.Asin(here.y) * Mathf.Rad2Deg, Mathf.Atan2(here.x, -here.z) * Mathf.Rad2Deg);
            globe.rotation = Quaternion.Slerp(flyStartRot, follow, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .3f)));
            size += 1.2f * Mathf.Sin(Mathf.PI * t); // pull back mid-flight
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
            // idle: the earth turns slowly, the plane circles over the current country
            globeYaw += 8 * Time.deltaTime;
            globe.rotation = Facing(35, globeYaw);
            var d = Dir(Places[mapWorld].x, Places[mapWorld].y);
            var east = Vector3.Cross(Vector3.up, d).normalized;
            var north = Vector3.Cross(d, east);
            float a = Time.time * 1.2f;
            var local = (d * (GlobeR + .45f) + (east * Mathf.Cos(a) + north * Mathf.Sin(a)) * .35f);
            var tangent = -east * Mathf.Sin(a) + north * Mathf.Cos(a);
            plane3D.SetPositionAndRotation(globe.TransformPoint(local), globe.rotation * Quaternion.LookRotation(-d, tangent));
            if (flyAt >= 0) flyStartRot = globe.rotation;
        }
        cam.orthographicSize = size;
    }

    void WorldScreen(float W, float H, float safeTop, float bottom)
    {
        if (!globe) return;

        // each country's name and stars, following its pin while it faces the player
        for (int w = 0; w < Worlds.Length; w++)
        {
            var d = globe.rotation * Dir(Places[w].x, Places[w].y);
            if (d.z > -.2f) continue; // on the far side
            var p = ToGui(globe.position + d * (GlobeR + .5f)) + new Vector2(w == 0 ? -110 : w == 1 ? 110 : 0, 0); // France and Italy are neighbours: labels apart
            p.x = Mathf.Clamp(p.x, 160, W - 160); // keep the label on screen
            bool open = openWorlds[w];
            var name = new Rect(p.x - 140, p.y - 110, 280, 64);
            Panel(name, open ? Worlds[w].color : new Color(.45f, .5f, .58f));
            Label(name, Worlds[w].name.ToUpper(), 38, Color.white, true);
            var info = new Rect(p.x - 110, p.y - 48, 220, 54);
            Panel(info, Ink);
            Star(new Rect(info.x + 12, info.y + 7, 40, 40), true);
            Label(new Rect(info.x + 50, info.y, info.width - 58, info.height),
                open ? $"{WorldStars(w)} / {LevelsPerWorld * 3}" : $"{Worlds[w].need} pour ouvrir", 28, Color.white);
            if (flyAt < 0 && GUI.Button(new Rect(p.x - 140, p.y - 110, 280, 130), GUIContent.none, GUIStyle.none))
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
        }

        // header
        float hy = safeTop + 40;
        if (flyAt < 0 && Button(new Rect(40, hy, 250, 96), "Retour", Mist, 40)) Land(mapWorld);
        Label(new Rect(0, hy + 110, W, 110), "LE MONDE", 90, Color.white, true);
        StarCount(W - 290, hy, mapStars);
        if (flyAt < 0) Label(new Rect(0, bottom - 90, W, 60), "Touche un pays pour t'y envoler", 38, new Color(1, 1, 1, .8f));
    }

    // the main camera goes back to its own background when the globe closes
    void RestoreCamera()
    {
        if (camFlags == 0) return;
        cam.clearFlags = camFlags;
        cam.backgroundColor = camBg;
    }
}
