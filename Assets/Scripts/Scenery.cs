using System.Collections.Generic;
using UnityEngine;

// 3D pieces of the level map (Map.cs), built like the products from primitives and plain colors:
// road ribbons, level pads, world gates, barriers and per-world decor.
public static partial class Products
{
    // each world's ground and its decor
    public static Color Ground(int w) => C(new[] { "#9ad66f", "#cfd88a", "#a9dc8f", "#ecc98f" }[w]); // a method: C() needs hexCache, which may not be initialized yet in a static field

    // the land of a country map, width x length world units: France a park lawn with mowing stripes, Italy a patchwork
    // of wheat, olive and meadow fields, Japan moss with flooded rice paddies, the USA desert sand with dry patches
    public static Material GroundMat(int w, float width, float length) => Paint((u, v) =>
    {
        float x = u * width, z = v * length;
        float cell = Mathf.Abs(Mathf.Sin(Mathf.Floor(x / 4) * 12.9898f + Mathf.Floor(z / 5) * 78.233f) * 43758.5453f) % 1;
        float n = Mathf.PerlinNoise(x * .12f, z * .12f);
        switch (w)
        {
            case 0: return Color.Lerp((int)(z / 1.5f) % 2 == 0 ? C("#92d066") : C("#9ed973"), C("#86c45e"), n * .4f);
            case 1:
                var field = C(new[] { "#e2cb6c", "#b8c97a", "#cfd88a", "#a9bf68", "#d9d27a" }[(int)(cell * 5)]);
                return field * (Mathf.Repeat(z * 1.2f, 1) < .2f ? .94f : 1f); // ploughed rows
            case 2:
                if (cell < .35f) return Mathf.Repeat(x * 1.5f, 1) < .25f ? C("#8fcf7a") : C("#a8d9b8"); // paddy: water between the rows
                return Color.Lerp(C("#a9dc8f"), C("#94cc7a"), n);
            default:
                return Color.Lerp(C("#efcd94"), C("#d9a46a"), n * n * 1.6f) * (Mathf.PerlinNoise(x * .8f, z * .8f) > .72f ? .9f : 1f);
        }
    }, .1f, 512);

    public static Transform Place(Transform parent, Mesh mesh, Color c, Vector3 pos, Vector3 euler = default, Vector3? scale = null, float smooth = .3f)
    {
        var t = Part(parent.gameObject, mesh, Plain(c, smooth), pos, euler, scale);
        return t;
    }

    public static Mesh Cube => Prim(PrimitiveType.Cube);
    public static Mesh Cylinder => Prim(PrimitiveType.Cylinder);
    public static Mesh Sphere => Prim(PrimitiveType.Sphere);

    // Flat strip of `width` following pts (on the ground, facing up). keep(i) = false skips the segment from
    // pts[i] to pts[i + 1] (dashes).
    public static Mesh Ribbon(IList<Vector3> pts, float width, System.Func<int, bool> keep = null)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            if (keep != null && !keep(i)) continue;
            Vector3 Side(int k)
            {
                var d = pts[Mathf.Min(k + 1, pts.Count - 1)] - pts[Mathf.Max(k - 1, 0)];
                return new Vector3(d.z, 0, -d.x).normalized * width / 2; // to the right of the direction of travel
            }
            int v = verts.Count;
            verts.AddRange(new[] { pts[i] - Side(i), pts[i] + Side(i), pts[i + 1] - Side(i + 1), pts[i + 1] + Side(i + 1) });
            uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
            tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 }); // wound to face up
        }
        return Finish(verts, uvs, tris);
    }

    // A level on the map: a little shop facing the camera (-Z), striped awning over its window and door, and an
    // empty sign board on the roof (SignY(w), z = -.47) where the map writes the level number. open = false: shutters down.
    public static float SignY(int w) => w == 0 ? 1.75f : 1.4f; // above the Paris mansard

    // Each country's shop: Paris shop with a zinc roof, Italian one in ochre under terracotta tiles, Japanese one in
    // dark wood with a paper window, a noren over the door and a red lantern, American diner with a chrome stripe.
    // awning: the stripes' color (the country's, red on hard levels). Locked: all grey.
    public static GameObject Shop(int w, Color awning, bool open)
    {
        var root = new GameObject("Shop");
        Color Pick(string fr, string it, string jp, string us) => !open ? C("#c9ced6") : C(new[] { fr, it, jp, us }[Mathf.Clamp(w, 0, 3)]);
        var wall = Pick("#fbf3e4", "#f2c98a", "#6b4a2e", "#cdeee9");
        var roofC = Pick("#4f5b6e", "#c8643c", "#30343d", "#f4f4f4");
        Part(root, Cube, Plain(C("#d9d4cb"), .2f), new(0, .04f, -.1f), scale: new(1.6f, .08f, 1.35f)); // pavement
        Part(root, Cube, Plain(wall, .3f), new(0, .53f, 0), scale: new(1.3f, .9f, 1f));
        var roof = Plain(roofC, .3f);
        if (w == 2) // Japanese gable: two slopes overhanging the walls
        {
            Part(root, Cube, roof, new(0, 1.1f, -.28f), new(-24, 0, 0), new(1.56f, .08f, .7f));
            Part(root, Cube, roof, new(0, 1.1f, .28f), new(24, 0, 0), new(1.56f, .08f, .7f));
        }
        else if (w == 0) // Paris: slate mansard roof with two dormers
        {
            Part(root, Prism(new(-.7f, .98f, -.56f), new(-.7f, .98f, .56f), new(-.7f, 1.48f, 0), new(1.4f, 0, 0)), roof, Vector3.zero);
            foreach (float x in new[] { -.32f, .32f })
            {
                Part(root, Cube, Plain(C("#fbf3e4"), .3f), new(x, 1.16f, -.3f), scale: new(.22f, .24f, .2f));
                Part(root, Cube, Plain(C("#4a5a78"), .8f), new(x, 1.15f, -.405f), scale: new(.12f, .14f, .01f));
            }
        }
        else Part(root, Cube, roof, new(0, 1.04f, 0), scale: new(1.44f, .14f, 1.12f));
        if (w == 3 && open) Part(root, Cube, Plain(C("#e8423c"), .7f), new(0, .3f, 0), scale: new(1.32f, .1f, 1.02f)); // diner stripe

        // sign board on posts
        Part(root, Cube, roof, new(-.4f, SignY(w) - .24f, -.42f), scale: new(.06f, .2f, .06f));
        Part(root, Cube, roof, new(.4f, SignY(w) - .24f, -.42f), scale: new(.06f, .2f, .06f));
        Part(root, Cube, Plain(C("#fbf8f0"), .4f), new(0, SignY(w), -.45f), scale: new(1.1f, .5f, .06f));

        // window and door, or rolled-down shutters while the level is locked
        var glass = w == 2 ? C("#f7f1e1") : C("#9fd8ff"); // paper screen in Japan
        Part(root, Cube, Plain(open ? glass : C("#9aa1ad"), open ? .9f : .3f), new(-.22f, .5f, -.505f), scale: new(.6f, .42f, .02f));
        Part(root, Cube, Plain(open ? C("#8a5a35") : C("#9aa1ad"), .3f), new(.37f, .38f, -.505f), scale: new(.3f, .6f, .02f));
        if (w == 0 && open) // window box of red geraniums
        {
            Part(root, Cube, Plain(C("#8a5a35"), .2f), new(-.22f, .26f, -.55f), scale: new(.62f, .08f, .1f));
            for (int k = 0; k < 5; k++)
                Part(root, Sphere, Plain(k % 2 == 0 ? C("#e3263a") : C("#3e9b3a"), .3f), new(-.46f + k * .12f, .32f, -.56f), scale: Vector3.one * .1f);
        }
        if (w == 2 && open)
        {
            Part(root, Cube, Plain(awning, .3f), new(.37f, .6f, -.52f), scale: new(.34f, .18f, .02f)); // noren curtain
            Part(root, Sphere, Plain(C("#e8423c"), .6f), new(.62f, .72f, -.55f), scale: new(.16f, .22f, .16f)); // lantern
        }
        else // striped awning sloping down over the front
            for (int i = 0; i < 6; i++)
                Part(root, Cube, Plain(i % 2 == 0 ? awning : Color.white, .4f), new(-.6f + i * .24f, .86f, -.66f), new(28, 0, 0), new(.24f, .08f, .42f));
        return root;
    }

    // a big monument per country, seen at the start of its stretch of road
    public static GameObject Landmark(int w)
    {
        var root = new GameObject("Landmark");
        switch (w)
        {
            case 0: // Eiffel tower: four splayed legs up to the first floor, a narrower stage, a slim top and a spire
            {
                var iron = Paint((u, v) => Mathf.Repeat(u * 4 + v * 8, 1) < .18f || Mathf.Repeat(u * 4 - v * 8, 1) < .18f ? C("#5a4330") : C("#9a7650"), .4f);
                void Beam(Vector3 a, Vector3 b, float th) =>
                    Part(root, Cube, iron, (a + b) / 2, Quaternion.FromToRotation(Vector3.up, b - a).eulerAngles, new(th, (b - a).magnitude, th));
                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    Beam(new(sx * 1.05f, 0, sz * 1.05f), new(sx * .45f, 1.05f, sz * .45f), .3f);
                    Beam(new(sx * .45f, 1.15f, sz * .45f), new(sx * .2f, 2.15f, sz * .2f), .2f);
                    Beam(new(sx * .2f, 2.2f, sz * .2f), new(sx * .05f, 3.5f, sz * .05f), .12f);
                }
                foreach (int s in new[] { -1, 1 }) // arched girders between the legs, front/back and sides
                {
                    Beam(new(-.75f, .62f, s * .78f), new(.75f, .62f, s * .78f), .1f);
                    Beam(new(s * .78f, .62f, -.75f), new(s * .78f, .62f, .75f), .1f);
                }
                var deck = Plain(C("#6e5236"), .3f);
                Part(root, Cube, deck, new(0, 1.1f, 0), scale: new(1.25f, .14f, 1.25f));
                Part(root, Cube, deck, new(0, 2.18f, 0), scale: new(.62f, .1f, .62f));
                Part(root, Cube, deck, new(0, 3.55f, 0), scale: new(.18f, .14f, .18f));
                Part(root, Cylinder, deck, new(0, 3.85f, 0), scale: new(.04f, .2f, .04f));
                break;
            }
            case 1: // Colosseum: ring wall with three tiers of arches
            {
                var mat = Paint((u, v) =>
                    Mathf.Repeat(u * 28, 1) is > .25f and < .75f && Mathf.Repeat(v * 3, 1) is > .15f and < .75f ? C("#8a6a4a") : C("#e3c9a0"), .2f, 256);
                Spin(root, P(1.5f, 0, 1.5f, 1.3f, 1.15f, 1.3f, 1.15f, 0), mat);
                break;
            }
            case 2: // Mount Fuji with snow, a red torii in front
            {
                Spin(root, P(0, 0, 2.1f, 0, 1.2f, .9f, .45f, 2f, .3f, 2.1f, 0, 2.12f),
                    Paint((u, v) => v > .72f + .05f * Mathf.Sin(u * 50) ? Color.white : C("#7f8fb5"), .3f), new(0, 0, .8f));
                var red = Plain(C("#d7282f"), .4f);
                foreach (float x in new[] { -.55f, .55f }) Part(root, Cylinder, red, new(x, .6f, -1.6f), scale: new(.12f, .6f, .12f));
                Part(root, Cube, Plain(C("#1f1f24"), .4f), new(0, 1.24f, -1.6f), scale: new(1.6f, .12f, .16f));
                Part(root, Cube, red, new(0, 1.0f, -1.6f), scale: new(1.3f, .09f, .12f));
                break;
            }
            default: // skyscrapers, the tallest with set-backs and a spire
            {
                var glass = Paint((u, v) => Mathf.Repeat(v * 14, 1) < .35f ? C("#2f4a6e") : C("#a9c7e8"), .8f);
                Part(root, Cube, glass, new(0, 1.5f, 0), scale: new(.9f, 3f, .9f));
                Part(root, Cube, glass, new(0, 3.3f, 0), scale: new(.6f, .6f, .6f));
                Part(root, Cylinder, Plain(C("#cfd4dc"), .8f), new(0, 4f, 0), scale: new(.06f, .5f, .06f));
                Part(root, Cube, glass, new(-1, 1, .3f), scale: new(.8f, 2f, .8f));
                Part(root, Cube, glass, new(1, .8f, .4f), scale: new(.8f, 1.6f, .8f));
                break;
            }
        }
        return root;
    }

    // ---------- the world map's globe ----------

    // the earth: Resources/Earth.png, NASA's Blue Marble (public domain) repainted in flat colours, equirectangular. The lathe's v
    // runs with height, so each vertex gets the photo's v from its latitude instead; its u = 0 sits at longitude 90°,
    // which is .75 across the photo.
    public static GameObject Globe(float r)
    {
        var mesh = Lathe(Ball(r, 0, 1, 48), 96);
        var verts = mesh.vertices;
        var uv = mesh.uv;
        for (int i = 0; i < uv.Length; i++) uv[i].y = Mathf.Asin(Mathf.Clamp(verts[i].y / r, -1, 1)) / Mathf.PI + .5f;
        mesh.uv = uv;
        var mat = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
        {
            mainTexture = Resources.Load<Texture2D>("Earth"), mainTextureOffset = new Vector2(.75f, 0), color = Color.white,
        };
        mat.SetFloat("_Smoothness", .25f);
        var go = new GameObject("Globe");
        Part(go, mesh, mat, Vector3.zero);
        return go;
    }

    // a little pin with the country's flag, standing on the globe (local +Y = outwards). The cloth waves a little
    // and reads the right way round from -Z (the side to turn to the camera); its back shows it unmirrored too.
    public static GameObject FlagPin(int w)
    {
        var root = new GameObject("Pin");
        Part(root, Cylinder, Plain(C("#e9edf2"), .5f), new(0, .26f, 0), scale: new(.03f, .26f, .03f));
        var flag = Paint((u, v) => w switch
        {
            0 => u < .33f ? C("#2f5fd0") : u < .66f ? Color.white : C("#e3263a"),
            1 => u < .33f ? C("#1f9a4a") : u < .66f ? Color.white : C("#e3263a"),
            2 => new Vector2((u - .5f) * 1.5f, v - .5f).magnitude < .3f ? C("#d7282f") : Color.white,
            _ => u < .42f && v > .46f ? C("#27407a") : (int)(v * 7) % 2 == 0 ? C("#c8282e") : Color.white,
        }, .2f);
        // lit from within a little: the cloth faces the camera, side-on to the light, and would look dull
        flag.EnableKeyword("_EMISSION");
        flag.SetTexture("_EmissionMap", flag.mainTexture);
        flag.SetColor("_EmissionColor", Color.white * .45f);
        const int N = 10;
        float fw = .4f, fh = .27f, top = .5f;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int side = 0; side < 2; side++)
            for (int i = 0; i <= N; i++)
            {
                float u = (float)i / N, z = .03f * u * Mathf.Sin(u * 7.5f); // still at the pole, rippling outwards
                foreach (float v in new[] { 0f, 1f })
                {
                    verts.Add(new Vector3(.015f + u * fw, top - fh + v * fh - .03f * u * u, z)); // the free end droops
                    uvs.Add(new Vector2(side == 0 ? u : 1 - u, v));
                }
            }
        for (int i = 0; i < N; i++)
        {
            int a = i * 2, b = (N + 1) * 2 + i * 2; // front strip, back strip
            tris.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
            tris.AddRange(new[] { b, b + 3, b + 1, b, b + 2, b + 3 });
        }
        Part(root, Finish(verts, uvs, tris), flag, Vector3.zero);
        Part(root, Sphere, Plain(C("#e8b84a"), .7f), new(0, .53f, 0), scale: Vector3.one * .065f);
        return root;
    }

    // a small airliner for the world map: nose along +Y, its top facing -Z, swept wings with two engines, a row of
    // windows and a cheatline down each side, tail in the country's colour
    public static GameObject Plane(Color stripe)
    {
        var root = new GameObject("Plane");
        var white = Plain(Color.white, .6f);
        var grey = Plain(C("#c9d2dc"), .6f);
        var tail = Plain(stripe, .5f);
        var glass = C("#24314a");
        // fuselage: the lathe's u goes round from +X (0) through +Z (.25, belly) to -Z (.75, top), v from tail to nose
        var skin = Paint((u, v) =>
        {
            float a = u * 360;
            bool side(float at, float half) => Mathf.Abs(Mathf.DeltaAngle(a, at)) < half;
            if (v > .86f && v < .92f && side(270, 45)) return glass; // cockpit windscreen
            if ((side(340, 6) || side(200, 6)) && v > .28f && v < .82f && Mathf.Repeat(v * 30, 1) < .5f) return glass; // windows
            if ((side(0, 9) || side(180, 9)) && v > .12f && v < .9f) return stripe; // cheatline under the windows
            return v < .1f ? C("#dfe5ec") : Color.white;
        }, .6f, 256);
        Spin(root, P(0, -.52f, .03f, -.5f, .06f, -.42f, .085f, -.3f, .095f, -.12f, .095f, .3f, .085f, .41f, .062f, .47f, .032f, .505f, 0, .52f),
            skin, segments: 40);
        // swept wings and tailplane, each half turned so its tip trails back
        foreach (int s in new[] { -1, 1 })
        {
            Part(root, Cube, white, new(s * .25f, -.04f, .02f), new(0, 0, s * -24), new(.5f, .13f, .022f));
            Spin(root, Capsule(.17f, .038f), grey, new(s * .2f, -.09f, .065f)); // engine under the wing
            Part(root, Cube, tail, new(s * .47f, -.17f, .01f), scale: new(.03f, .05f, .02f)); // wingtip in colour
            Part(root, Cube, white, new(s * .1f, -.45f, -.02f), new(0, 0, s * -28), new(.2f, .07f, .018f));
        }
        Part(root, Cube, tail, new(0, -.42f, -.12f), new(-32, 0, 0), new(.022f, .13f, .19f)); // fin
        return root;
    }

    // airport where the player lands in a country: dark tarmac with yellow guide lines and a white edge, a runway along
    // the back, a glass-fronted terminal with a jet bridge out to the airliner they came in on (tail in that country's
    // colour), the control tower, a baggage tug with its carts. Built with the road's end at the origin and the
    // buildings towards +Z; the map turns it round to sit before the road's start.
    public static GameObject Airport(Color tail)
    {
        var root = new GameObject("Airport");
        Part(root, Cube, Paint((u, v) =>
        {
            if (u < .015f || u > .985f || v < .02f || v > .98f) return C("#f4f4f2"); // edge
            if (Mathf.Abs(u - .55f) < .006f && v < .5f || Mathf.Abs(v - .5f) < .009f && u > .3f && u < .55f) return C("#f2c230"); // guide lines
            return C("#6b707a") * (.95f + .08f * Mathf.PerlinNoise(u * 30, v * 20));
        }, .2f, 256), new(0, .01f, 1.6f), scale: new(7.2f, .02f, 4.4f));
        Part(root, Cube, Paint((u, v) => Mathf.Abs(v - .5f) < .06f && Mathf.Repeat(u * 10, 1) < .5f ? Color.white : C("#4a4e57"), .2f),
            new(0, .022f, 3.45f), scale: new(7.2f, .01f, .7f)); // runway with its centre dashes
        var white = Plain(C("#f4f4f2"), .4f);
        // terminal: a glass front over a white base, a pale roof overhanging it
        Part(root, Cube, Paint((u, v) => v > .25f && v < .85f ? (Mathf.Repeat(u * 12, 1) < .08f ? C("#d9dde3") : C("#5a8fc2")) : C("#f4f4f2"), .85f),
            new(-2.3f, .42f, 2.3f), scale: new(2.4f, .84f, 1.1f));
        Part(root, Cube, Plain(C("#c8cdd6"), .4f), new(-2.3f, .88f, 2.3f), scale: new(2.7f, .08f, 1.35f));
        Part(root, Cube, white, new(-.85f, .45f, 1.8f), new(0, 25, 0), new(.9f, .22f, .22f)); // jet bridge to the plane's door
        // the airliner, nose towards the road, wheels down on the apron
        var plane = Plane(tail).transform;
        plane.SetParent(root.transform, false);
        plane.localPosition = new(.3f, .32f, 1.75f);
        plane.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.back); // its nose (+Y) to -Z, its top (-Z) up
        plane.localScale = Vector3.one * 2.3f;
        // control tower on the right, with its antenna
        Part(root, Cylinder, white, new(2.5f, .9f, 2.6f), scale: new(.32f, .9f, .32f));
        Part(root, Cylinder, Plain(C("#5a8fc2"), .9f), new(2.5f, 1.95f, 2.6f), scale: new(.62f, .15f, .62f));
        Part(root, Cylinder, white, new(2.5f, 2.15f, 2.6f), scale: new(.7f, .05f, .7f));
        Part(root, Cylinder, Plain(C("#d7192a"), .5f), new(2.5f, 2.4f, 2.6f), scale: new(.03f, .2f, .03f));
        // baggage tug pulling three carts
        Part(root, Cube, Plain(C("#f2c230"), .4f), new(1.5f, .12f, .6f), scale: new(.3f, .2f, .22f));
        for (int k = 0; k < 3; k++)
            Part(root, Cube, Plain(C(k == 1 ? "#2f7ae5" : "#e8742a"), .4f), new(1.95f + k * .36f, .1f, .6f), scale: new(.3f, .16f, .22f));
        return root;
    }

    // a monument still to win, wrapped like a present: a draped tarp of `size` (standing on y = 0, centred), a red
    // ribbon crossing over it both ways and a big bow on top
    public static GameObject Wrapped(Vector3 size)
    {
        var root = new GameObject("Wrapped");
        Part(root, Cube, Paint((u, v) => C("#8fa0bd") * (.88f + .14f * Mathf.Abs(Mathf.Sin(u * 30 + v * 2))), .3f), new(0, size.y / 2, 0), scale: size); // folds
        var ribbon = Plain(C("#d7192a"), .6f);
        Part(root, Cube, ribbon, new(0, size.y / 2, 0), scale: new(.18f, size.y + .03f, size.z + .03f));
        Part(root, Cube, ribbon, new(0, size.y / 2, 0), scale: new(size.x + .03f, size.y + .03f, .18f));
        foreach (int s in new[] { -1, 1 })
            Part(root, Sphere, ribbon, new(s * .22f, size.y + .14f, 0), new(0, 0, s * -30), new(.42f, .2f, .26f)); // the bow's loops
        Part(root, Sphere, ribbon, new(0, size.y + .1f, 0), scale: Vector3.one * .16f); // its knot
        return root;
    }

    // archway over the road with a board tilted to face the map camera (which looks down at `tilt` degrees)
    public static GameObject Gate(Color c, float tilt)
    {
        var root = new GameObject("Gate");
        var post = Plain(C("#fbf8f0"), .4f);
        foreach (float x in new[] { -1.3f, 1.3f })
        {
            Part(root, Cylinder, post, new(x, .9f, 0), scale: new(.16f, .9f, .16f));
            Part(root, Sphere, Plain(c, .5f), new(x, 1.85f, 0), scale: Vector3.one * .26f);
        }
        Part(root, Cube, Plain(c * .75f + new Color(0, 0, 0, .25f), .3f), new(0, 2.05f, .04f), new(tilt, 0, 0), new(2.9f, 1.12f, .1f)); // frame
        Part(root, Cube, Plain(c, .4f), new(0, 2.05f, 0), new(tilt, 0, 0), new(2.7f, .98f, .12f));
        return root;
    }

    // one piece of scenery for country w; r (0..1) picks the variant and its size.
    // France: Haussmann buildings, plane trees, lamp posts, Morris columns, café tables, flower beds; Italy: villas, rows of vines,
    // olive bushes, cypresses; Japan: pagodas, koi ponds, bamboo, stone lanterns, cherry trees; USA: mesas, Route 66 signs,
    // water towers, cacti, palm trees. The painted façades are shared by every copy.
    static Material haussmann, villa;
    public static GameObject Decor(int w, float r)
    {
        var root = new GameObject("Decor");
        float s = .8f + .5f * r;
        var trunk = Plain(C("#8a5a35"), .2f);
        void RoundTree(Color leaf)
        {
            Part(root, Cylinder, trunk, new(0, .3f * s, 0), scale: new(.14f, .3f * s, .14f));
            Part(root, Sphere, Plain(leaf, .25f), new(0, .85f * s, 0), scale: new Vector3(.95f, .85f, .95f) * s);
            Part(root, Sphere, Plain(leaf * 1.15f, .25f), new(-.12f * s, 1.05f * s, -.12f * s), scale: new Vector3(.55f, .45f, .55f) * s);
        }
        switch (w)
        {
            case 0 when r < .2f: // Paris lamp post
                Part(root, Cylinder, Plain(C("#2f4a3a"), .5f), new(0, .7f, 0), scale: new(.07f, .7f, .07f));
                Part(root, Cube, Plain(C("#2f4a3a"), .5f), new(0, 1.42f, 0), scale: new(.22f, .06f, .22f));
                Part(root, Sphere, Plain(C("#fff2b0"), .9f), new(0, 1.55f, 0), scale: Vector3.one * .22f);
                break;
            case 0 when r < .27f: // Morris column, dark green with a little dome
            {
                var green = Plain(C("#2f4a3a"), .5f);
                Part(root, Cylinder, Paint((u, v) => Mathf.Repeat(u * 5, 1) < .5f ? C("#f2e6c8") : C("#e8b25a"), .3f), new(0, .6f, 0), scale: new(.34f, .45f, .34f));
                Part(root, Cylinder, green, new(0, .1f, 0), scale: new(.4f, .1f, .4f));
                Part(root, Cylinder, green, new(0, 1.08f, 0), scale: new(.42f, .04f, .42f));
                Spin(root, Ball(.2f, 1.1f, .8f), green);
                break;
            }
            case 0 when r < .36f: // café table under a red and white umbrella, two chairs
            {
                var metal = Plain(C("#2a2d35"), .5f);
                Part(root, Cylinder, metal, new(0, .3f, 0), scale: new(.04f, .3f, .04f));
                Part(root, Cylinder, Plain(C("#fbf8f0"), .4f), new(0, .6f, 0), scale: new(.5f, .02f, .5f));
                Part(root, Cylinder, metal, new(0, .95f, 0), scale: new(.03f, .35f, .03f));
                Spin(root, P(0, 1.2f, .55f, 1.2f, 0, 1.42f), Paint((u, v) => (int)(u * 8) % 2 == 0 ? C("#e8423c") : Color.white, .4f));
                foreach (float x in new[] { -.4f, .4f })
                    Part(root, Cube, Plain(C("#c79a5a"), .3f), new(x, .25f, 0), scale: new(.22f, .5f, .22f));
                break;
            }
            case 0 when r < .44f: // round flower bed
                Part(root, Cylinder, Plain(C("#cfc6b4"), .2f), new(0, .08f, 0), scale: new(.9f, .08f, .9f));
                Part(root, Sphere, Paint((u, v) => Dot(u, v, 14, 8, .5f, .35f, 2, out float k) ? (k < .25f ? C("#e3263a") : C("#f7b6cf")) : C("#4fa83d"), .3f),
                    new(0, .2f, 0), scale: new(.8f, .3f, .8f));
                break;
            case 0 when r < .66f: // Haussmann building: cream stone, rows of tall windows, a black balcony, slate roof and chimneys
            {
                haussmann ??= Paint((u, v) => Mathf.Repeat(u * 4, 1) is > .3f and < .7f && Mathf.Repeat(v * 4, 1) is > .25f and < .75f
                    ? C("#5a6e8a") : C("#efe2c6"), .3f);
                Part(root, Cube, haussmann, new(0, .65f, 0), scale: new(1.2f, 1.3f, .9f));
                Part(root, Cube, Plain(C("#2a2d35"), .4f), new(0, .78f, -.47f), scale: new(1.22f, .05f, .08f)); // balcony
                Part(root, Cube, Plain(C("#4f5b6e"), .4f), new(0, 1.42f, 0), scale: new(1.14f, .26f, .84f)); // roof
                foreach (float x in new[] { -.35f, .35f }) Part(root, Cube, Plain(C("#c9785a"), .3f), new(x, 1.65f, .1f), scale: new(.12f, .22f, .12f));
                break;
            }
            case 0: // plane tree: pale mottled bark
                Part(root, Cylinder, Paint((u, v) => Mathf.PerlinNoise(u * 8, v * 8) > .55f ? C("#d9cfb8") : C("#9a8a6a"), .2f), new(0, .3f * s, 0), scale: new(.15f, .3f * s, .15f));
                Part(root, Sphere, Plain(C("#6cbb52"), .25f), new(0, .85f * s, 0), scale: new Vector3(1f, .85f, 1f) * s);
                Part(root, Sphere, Plain(C("#82c964"), .25f), new(-.12f * s, 1.05f * s, -.12f * s), scale: new Vector3(.55f, .45f, .55f) * s);
                break;
            case 1 when r < .15f: // Tuscan villa: ochre walls with green shutters, a terracotta roof
            {
                villa ??= Paint((u, v) => Mathf.Repeat(u * 3, 1) is > .35f and < .65f && Mathf.Repeat(v * 2, 1) is > .3f and < .75f
                    ? C("#4f7a4a") : C("#efc485"), .25f);
                Part(root, Cube, villa, new(0, .4f, 0), scale: new(1.1f, .8f, .8f));
                var tiles = Plain(C("#c8643c"), .3f);
                Part(root, Cube, tiles, new(0, .9f, -.22f), new(-22, 0, 0), new(1.24f, .07f, .5f));
                Part(root, Cube, tiles, new(0, .9f, .22f), new(22, 0, 0), new(1.24f, .07f, .5f));
                break;
            }
            case 1 when r < .4f: // a row of vines on their stakes
            {
                var vine = Plain(C("#5f8f3a"), .3f);
                var stake = Plain(C("#8a6a45"), .2f);
                for (int k = 0; k < 5; k++)
                {
                    Part(root, Cylinder, stake, new(-.6f + k * .3f, .22f, 0), scale: new(.03f, .22f, .03f));
                    Part(root, Sphere, vine, new(-.6f + k * .3f, .38f, 0), scale: new(.3f, .3f, .22f));
                }
                break;
            }
            case 1 when r < .6f: // olive bush
                Part(root, Sphere, Plain(C("#8fa36a"), .2f), new(0, .3f, 0), scale: new Vector3(.8f, .55f, .8f) * s);
                break;
            case 1: // cypress
                Part(root, Cylinder, trunk, new(0, .15f, 0), scale: new(.1f, .15f, .1f));
                Part(root, Sphere, Plain(C("#2f6b3a"), .25f), new(0, 1f * s, 0), scale: new Vector3(.45f, 1.8f, .45f) * s);
                break;
            case 2 when r < .12f: // pagoda: three red tiers under wide dark roofs with upturned eaves, a spire
            {
                var red = Plain(C("#c8322c"), .4f);
                var roof = Plain(C("#2f3440"), .4f);
                for (int k = 0; k < 3; k++)
                {
                    float y = k * .55f, sz = .8f - k * .15f;
                    Part(root, Cube, red, new(0, y + .22f, 0), scale: new(sz, .44f, sz));
                    Part(root, Cube, roof, new(0, y + .5f, 0), scale: new(sz + .5f, .07f, sz + .5f));
                    Part(root, Cube, roof, new(0, y + .55f, 0), scale: new(sz + .2f, .1f, sz + .2f));
                }
                Part(root, Cylinder, Plain(C("#d9b44a"), .6f), new(0, 1.9f, 0), scale: new(.04f, .25f, .04f));
                break;
            }
            case 2 when r < .22f: // koi pond: a ring of stones round blue water, three koi
            {
                Part(root, Cylinder, Plain(C("#9a9c96"), .2f), new(0, .02f, 0), scale: new(1.3f, .04f, 1f));
                Part(root, Cylinder, Plain(C("#5fa3d6"), .8f), new(0, .04f, 0), scale: new(1.1f, .02f, .82f));
                for (int k = 0; k < 3; k++)
                    Part(root, Sphere, Plain(C(k == 1 ? "#fbf8f0" : "#f07a2a"), .6f), new(-.25f + k * .25f, .06f, (k - 1) * .15f), new(0, k * 70, 0),
                        new(.16f, .03f, .07f));
                break;
            }
            case 2 when r < .3f: // a clump of bamboo
            {
                var cane = Plain(C("#7fb04a"), .4f);
                for (int k = 0; k < 5; k++)
                    Part(root, Cylinder, cane, new(Mathf.Cos(k * 1.3f) * .15f, .7f + k * .06f, Mathf.Sin(k * 1.3f) * .15f), new(k * 3 - 6, 0, k * 2 - 4),
                        new(.06f, .7f + k * .06f, .06f));
                break;
            }
            case 2 when r < .42f: // stone lantern
            {
                var stone = Plain(C("#a7a9a3"), .2f);
                Part(root, Cylinder, stone, new(0, .3f, 0), scale: new(.14f, .3f, .14f));
                Part(root, Cube, stone, new(0, .7f, 0), scale: new(.34f, .24f, .34f));
                Part(root, Cube, Plain(C("#fff2b0"), .6f), new(0, .7f, -.172f), scale: new(.16f, .12f, .01f));
                Part(root, Cube, stone, new(0, .88f, 0), new(0, 45, 0), new(.46f, .1f, .46f));
                break;
            }
            case 2: // cherry tree in bloom
                RoundTree(C(r < .65f ? "#f7b6cf" : "#f49ac0"));
                break;
            case 3 when r < .15f: // mesa: a flat-topped butte of layered red rock
                Spin(root, P(0, 0, .9f, 0, .7f, .3f, .62f, .6f, .58f, .85f, .64f, .9f, 0, .9f),
                    Paint((u, v) => Mathf.Repeat(v * 5, 1) < .3f ? C("#b8582e") : C("#d77a44"), .2f), ribs: 6, ribAmp: .08f);
                break;
            case 3 when r < .22f: // Route 66 sign: a white shield on a post
                Part(root, Cylinder, Plain(C("#8a8f99"), .5f), new(0, .5f, 0), scale: new(.05f, .5f, .05f));
                Part(root, Cube, Plain(Color.white, .4f), new(0, 1.05f, 0), scale: new(.5f, .45f, .04f));
                Part(root, Cube, Plain(C("#2a2d35"), .4f), new(0, 1.05f, -.025f), scale: new(.42f, .37f, .01f));
                Part(root, Cube, Plain(Color.white, .4f), new(0, 1.05f, -.032f), scale: new(.26f, .18f, .01f));
                break;
            case 3 when r < .28f: // water tower on stilts
            {
                var steel = Plain(C("#c9ced6"), .6f);
                foreach (var (x, z) in new[] { (-.25f, -.25f), (.25f, -.25f), (.25f, .25f), (-.25f, .25f) })
                    Part(root, Cylinder, steel, new(x, .55f, z), scale: new(.05f, .55f, .05f));
                Spin(root, P(0, 1.05f, .42f, 1.05f, .42f, 1.55f, .2f, 1.75f, 0, 1.8f), steel);
                break;
            }
            case 3 when r < .55f: // cactus
            {
                var green = Plain(C("#4f9a4a"), .3f);
                Spin(root, Capsule(1.1f * s, .16f), green);
                Spin(root, Capsule(.45f, .1f), green, new(.2f, .45f, 0), new(0, 0, -90));
                Spin(root, Capsule(.35f, .09f), green, new(.24f, .4f, 0));
                break;
            }
            default: // palm tree: leaning trunk, fronds fanned out at the top
            {
                var top = Tilted(root, new(0, r * 360, 10));
                Part(top, Cylinder, Plain(C("#a8794a"), .2f), new(0, .8f * s, 0), scale: new(.1f, .8f * s, .1f));
                var leaf = Plain(C("#3e9b3a"), .3f);
                for (int k = 0; k < 6; k++)
                    Part(top, Sphere, leaf, new(Mathf.Cos(k * 1.05f) * .35f, 1.6f * s, Mathf.Sin(k * 1.05f) * .35f),
                        new(0, -k * 60, -25), new(.8f, .06f, .2f));
                break;
            }
        }
        return root;
    }
}
