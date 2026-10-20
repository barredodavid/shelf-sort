using System.Collections.Generic;
using UnityEngine;

// 3D pieces of the level map (Map.cs), built like the products from primitives and plain colors:
// road ribbons, level pads, world gates, barriers and per-world decor.
public static partial class Products
{
    // each world's ground and its decor
    public static Color Ground(int w) => C(new[] { "#e6dfd1", "#efdfc2", "#ebe5d9", "#cfd2d7" }[w]); // a method: C() needs hexCache, which may not be initialized yet in a static field

    // the land of a country map, width x length world units: France pale Paris flagstones, Italy (Rome) warm travertine
    // slabs, Japan raked gravel strewn with cherry petals, the USA city concrete laid in slabs, with
    // no grass
    public static Material GroundMat(int w, float width, float length) => Paint((u, v) =>
    {
        float x = u * width, z = v * length;
        float n = Mathf.PerlinNoise(x * .12f, z * .12f);
        switch (w)
        {
            case 0: return (Mathf.Repeat(x / 1.2f, 1) < .05f || Mathf.Repeat(z / 1.2f + (int)(x / 1.2f) * .5f, 1) < .05f ? C("#c9bfae") : C("#e6dfd1")) * (.96f + .08f * n); // pale limestone flagstones
            case 1: return (Mathf.Repeat(x / 1.6f, 1) < .04f || Mathf.Repeat(z / 1f + (int)(x / 1.6f) * .5f, 1) < .05f ? C("#d6c19c") : C("#efdfc2")) * (.95f + .1f * n); // warm travertine slabs
            case 2: // pale raked gravel strewn with cherry petals
                if (Mathf.PerlinNoise(x * 3, z * 3) > .72f) return C("#f6c9d8");
                return (Mathf.Repeat(x * 2.5f + .3f * Mathf.Sin(z * .8f), 1) < .12f ? C("#d9d1c2") : C("#ebe5d9")) * (.97f + .06f * n);
            default:
                return (Mathf.Repeat(x / 1.5f, 1) < .05f || Mathf.Repeat(z / 1.5f, 1) < .05f ? C("#b4b8bf") : C("#cfd2d7")) * (.96f + .08f * n); // concrete slabs
        }
    }, .1f, 512);

    // Paris cobbles (Rome: black basalt sampietrini set in sand) for a tiled Ribbon (one texture square per road width): eight rows of grey-brown setts, every other
    // row offset by half a stone, dark joints, a damp sheen
    public static Material Cobbles(int w)
    {
        var m = Paint((u, v) =>
        {
            int row = (int)(v * 8);
            float cu = u * 8 + row % 2 * .5f;
            if (Mathf.Repeat(cu, 1) < .12f || Mathf.Repeat(v * 8, 1) < .14f) return C(w == 1 ? "#a8a094" : "#4f4a44"); // Rome: sand between black basalt
            return w == 1 ? Color.Lerp(C("#3a3937"), C("#5e5b57"), Hash((int)cu, row, 7)) : Color.Lerp(C("#7d776f"), C("#a39a8c"), Hash((int)cu, row, 7));
        }, .55f);
        m.mainTexture.wrapMode = TextureWrapMode.Repeat;
        return m;
    }

    public static Transform Place(Transform parent, Mesh mesh, Color c, Vector3 pos, Vector3 euler = default, Vector3? scale = null, float smooth = .3f)
    {
        var t = Part(parent.gameObject, mesh, Plain(c, smooth), pos, euler, scale);
        return t;
    }

    public static Mesh Cube => Prim(PrimitiveType.Cube);
    public static Mesh Cylinder => Prim(PrimitiveType.Cylinder);
    public static Mesh Sphere => Prim(PrimitiveType.Sphere);

    // Flat strip of `width` following pts (on the ground, facing up). keep(i) = false skips the segment from
    // pts[i] to pts[i + 1] (dashes). tile: v runs on with the distance (one unit of v per width) so a texture repeats
    // along it, square; otherwise each segment spans the whole texture.
    public static Mesh Ribbon(IList<Vector3> pts, float width, System.Func<int, bool> keep = null, bool tile = false)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float along = 0;
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
            float v0 = tile ? along / width : 0, v1 = tile ? (along += Vector3.Distance(pts[i], pts[i + 1])) / width : 1;
            uvs.AddRange(new[] { new Vector2(0, v0), new Vector2(1, v0), new Vector2(0, v1), new Vector2(1, v1) });
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
        var wall = Pick("#fbf3e4", "#f2c98a", "#6b4a2e", "#8fd0c8"); // the USA: a 50s diner teal
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
        if (w == 3 && open) // diner: a red stripe and a chrome band round the walls, a pink neon tube along the roof's front
        {
            Part(root, Cube, Plain(C("#e8423c"), .7f), new(0, .12f, 0), scale: new(1.36f, .12f, 1.06f));
            Part(root, Cube, Plain(C("#dfe4ea"), .95f), new(0, .76f, 0), scale: new(1.36f, .07f, 1.06f));
            var neon = Plain(C("#ff4fa3"), .81f); // its own smoothness: Plain shares materials, this one glows
            neon.EnableKeyword("_EMISSION");
            neon.SetColor("_EmissionColor", C("#ff4fa3") * 1.3f);
            Part(root, Cube, neon, new(0, 1.13f, -.57f), scale: new(1.4f, .05f, .05f));
        }

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

    // a Paris rock dove standing on y = 0, facing -Z: an egg-shaped blue-grey body leaning forward on a puffed chest,
    // purple-and-green sheen round the neck, pale wings folded on its back crossed by two black bars, a dark tail
    // ending in a black band, a small round head with an orange eye, a dark beak with its white cere, red feet with
    // toes. Its head is a child named "head" (to peck), its wings "wing" (to flap, hinged at the shoulder).
    public static GameObject Pigeon()
    {
        var root = new GameObject("Pigeon");
        var grey = Plain(C("#8e98a8"), .35f);
        var pale = Plain(C("#b4bcc8"), .35f);
        var black = Plain(C("#2a2d33"), .3f);
        var red = Plain(C("#d9576a"), .3f);
        Part(root, Sphere, grey, new(0, .3f, .04f), new(-18, 0, 0), new(.4f, .38f, .62f)); // body, leaning forward
        Part(root, Sphere, grey, new(0, .32f, -.14f), scale: new(.34f, .36f, .3f)); // the puffed chest
        Part(root, Sphere, Paint((u, v) => Color.Lerp(C("#5e9c80"), C("#8a5a9e"), .5f + .5f * Mathf.Sin(u * 18)), .85f), new(0, .46f, -.18f), new(-20, 0, 0), new(.25f, .26f, .24f)); // the shimmering neck
        foreach (int s in new[] { -1, 1 })
        {
            var wing = new GameObject("wing").transform; // hinged at the shoulder, the folded wing hanging back from it
            wing.SetParent(root.transform, false);
            wing.localPosition = new(s * .15f, .4f, -.06f);
            wing.localRotation = Quaternion.Euler(0, 0, s * 12);
            Part(wing.gameObject, Sphere, pale, new(s * .03f, -.06f, .17f), new(-12, 0, 0), new(.12f, .2f, .46f));
            foreach (float z in new[] { .14f, .26f }) // the two black bars
                Part(wing.gameObject, Sphere, black, new(s * .075f, -.07f, z), new(-12, 0, 0), new(.03f, .14f, .06f));
            Part(wing.gameObject, Sphere, black, new(s * .03f, -.04f, .38f), new(-12, 0, 0), new(.08f, .06f, .14f)); // dark wing tip
            Part(root, Cylinder, red, new(s * .07f, .05f, -.02f), scale: new(.035f, .05f, .035f)); // leg
            foreach (float a in new[] { -30f, 0, 30 }) // toes
                Part(root, Cube, red, new(s * .07f + .05f * Mathf.Sin(a * Mathf.Deg2Rad), .01f, -.02f - .05f * Mathf.Cos(a * Mathf.Deg2Rad)), new(0, a, 0), new(.025f, .02f, .1f));
        }
        Part(root, Cube, grey, new(0, .27f, .38f), new(-20, 0, 0), new(.2f, .04f, .22f)); // tail
        Part(root, Cube, black, new(0, .23f, .49f), new(-20, 0, 0), new(.205f, .045f, .06f)); // its black band
        var head = new GameObject("head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = new(0, .6f, -.24f);
        Part(head.gameObject, Sphere, grey, Vector3.zero, scale: new(.2f, .2f, .22f));
        foreach (int s in new[] { -1, 1 })
        {
            Part(head.gameObject, Sphere, Plain(C("#e8742a"), .7f), new(s * .085f, .025f, -.04f), scale: Vector3.one * .055f); // orange eye
            Part(head.gameObject, Sphere, black, new(s * .1f, .028f, -.045f), scale: Vector3.one * .028f); // pupil
        }
        Part(head.gameObject, Sphere, Plain(C("#eef0f2"), .4f), new(0, .025f, -.1f), scale: new(.05f, .035f, .05f)); // white cere
        Part(head.gameObject, Cube, Plain(C("#3a3a40"), .4f), new(0, -.005f, -.15f), new(12, 0, 0), new(.035f, .03f, .09f)); // beak
        return root;
    }

    // a treasure chest for the map: plank box, domed lid, gold bands round both and a gold lock at the front (-Z).
    // ready: its colours; otherwise faded to grey (the level still to win)
    public static GameObject Chest(bool ready)
    {
        var root = new GameObject("Chest");
        Color Tone(string hex) => ready ? C(hex) : Color.Lerp(C(hex), C("#9aa1ad"), .75f);
        var wood = Paint((u, v) => Tone(Mathf.Repeat(v * 3, 1) < .08f ? "#5a3418" : "#9a5a2a"), .3f);
        var gold = Plain(Tone("#f2c230"), .8f);
        Part(root, Cube, wood, new(0, .25f, 0), scale: new(.8f, .5f, .55f)); // the box
        Part(root, Sphere, gold, new(0, .48f, 0), scale: new(.66f, .14f, .44f)); // the coins heaped inside, seen once open
        var lid = new GameObject("lid"); // hinged at the back edge, so it swings open (Chest.cs)
        lid.transform.SetParent(root.transform, false);
        lid.transform.localPosition = new Vector3(0, .5f, .275f);
        Part(lid, Cylinder, wood, new(0, 0, -.275f), new(0, 0, 90), new(.55f, .4f, .55f)); // the domed lid: a log lying along X
        foreach (float x in new[] { -.26f, .26f })
        {
            Part(root, Cube, gold, new(x, .25f, 0), scale: new(.08f, .52f, .57f));
            Part(lid, Cylinder, gold, new(x, 0, -.275f), new(0, 0, 90), new(.58f, .04f, .58f));
        }
        Part(root, Cube, gold, new(0, .46f, -.29f), scale: new(.16f, .18f, .04f)); // the lock
        Part(root, Cube, Plain(C("#3a2410"), .3f), new(0, .44f, -.312f), scale: new(.04f, .07f, .01f)); // its keyhole
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
    // France (Paris): Haussmann buildings, lamp posts, Morris columns, café terraces, metro entrances, plane trees in grates;
    // Italy (Rome): palazzi in warm plaster, Vespas, Fiat 500s, fountains, ruined columns, obelisks, café terraces, stone
    // pines, cypresses; Japan: pagodas, machiya houses, torii, stone and paper lanterns, koi ponds, cherry trees in bloom;
    // USA (New York): brick blocks
    // with fire escapes and rooftop water tanks, glass towers, yellow cabs, hydrants and mailboxes, hot dog carts, flags,
    // billboards, street trees in planters. The painted façades are shared by every copy.
    static Material haussmann, brick, glassTower;
    static readonly Material[] palazzo = new Material[4]; // one façade per plaster colour
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
            case 0 when r < .45f: // Haussmann building: cream stone, a dark shop front on the ground floor, rows of tall
            {                     // windows, wrought-iron balconies on the 2nd and top floors, a slate roof with dormers, chimney pots
                float h = 1.2f + .7f * r / .45f;
                haussmann ??= Paint((u, v) => Mathf.Repeat(u * 4, 1) is > .3f and < .7f && Mathf.Repeat(v * 5, 1) is > .2f and < .75f
                    ? C("#5a6e8a") : C("#efe2c6"), .3f);
                var iron = Plain(C("#2a2d35"), .4f);
                var slate = Plain(C("#6f7b8c"), .45f);
                Part(root, Cube, haussmann, new(0, h / 2, 0), scale: new(1.3f, h, 1f));
                Part(root, Cube, Plain(C(r < .2f ? "#2f4a3a" : "#7a2430"), .4f), new(0, .16f, -.505f), scale: new(1.1f, .26f, .02f)); // shop front
                Part(root, Cube, Plain(C("#e3d4b4"), .3f), new(0, .34f, -.51f), scale: new(1.32f, .05f, .04f)); // stone ledge over it
                foreach (float y in new[] { .55f, h - .12f }) // balconies
                    Part(root, Cube, iron, new(0, y, -.53f), scale: new(1.3f, .07f, .07f));
                Part(root, Prism(new(-.65f, h, -.5f), new(-.65f, h, .5f), new(-.65f, h + .45f, 0), new(1.3f, 0, 0)), slate, Vector3.zero); // roof
                foreach (float x in new[] { -.36f, 0, .36f }) // dormers
                {
                    Part(root, Cube, Plain(C("#efe2c6"), .3f), new(x, h + .17f, -.3f), scale: new(.18f, .2f, .16f));
                    Part(root, Cube, Plain(C("#4a5a78"), .8f), new(x, h + .16f, -.385f), scale: new(.1f, .12f, .01f));
                }
                foreach (float x in new[] { -.5f, .5f })
                {
                    Part(root, Cube, Plain(C("#e3d4b4"), .3f), new(x, h + .38f, .15f), scale: new(.16f, .3f, .14f)); // chimney stack
                    for (int k = 0; k < 3; k++) Part(root, Cylinder, Plain(C("#c9785a"), .3f), new(x - .05f + k * .05f, h + .57f, .15f), scale: new(.035f, .05f, .035f));
                }
                break;
            }
            case 0 when r < .53f: // Paris street lamp: a black cast-iron post on a fluted base, a glass lantern lit warm under its
            {                     // little black roof
                var iron = Plain(C("#1f2226"), .6f);
                Part(root, Cylinder, iron, new(0, .15f, 0), scale: new(.16f, .15f, .16f));
                Part(root, Cylinder, iron, new(0, .85f, 0), scale: new(.06f, .7f, .06f));
                Part(root, Cylinder, iron, new(0, 1.55f, 0), scale: new(.1f, .02f, .1f));
                var glow = Plain(C("#ffe39a"), .82f); // its own smoothness: Plain shares materials, this one glows
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", C("#ffcf6a") * .8f);
                Spin(root, P(.07f, 1.57f, .12f, 1.78f), glow);
                Spin(root, P(.15f, 1.78f, .02f, 1.9f), iron);
                Part(root, Sphere, iron, new(0, 1.93f, 0), scale: Vector3.one * .05f);
                break;
            }
            case 0 when r < .58f: // Morris column, dark green with a little dome
            {
                var green = Plain(C("#2f4a3a"), .5f);
                Part(root, Cylinder, Paint((u, v) => Mathf.Repeat(u * 5, 1) < .5f ? C("#f2e6c8") : C("#e8b25a"), .3f), new(0, .6f, 0), scale: new(.34f, .45f, .34f));
                Part(root, Cylinder, green, new(0, .1f, 0), scale: new(.4f, .1f, .4f));
                Part(root, Cylinder, green, new(0, 1.08f, 0), scale: new(.42f, .04f, .42f));
                Spin(root, Ball(.2f, 1.1f, .8f), green);
                break;
            }
            case 0 when r < .67f: // café terrace: two tables under a red and white awning-striped umbrella, wicker chairs
            {
                var metal = Plain(C("#2a2d35"), .5f);
                foreach (float x in new[] { -.35f, .35f })
                {
                    Part(root, Cylinder, metal, new(x, .2f, 0), scale: new(.03f, .2f, .03f));
                    Part(root, Cylinder, Plain(C("#fbf8f0"), .4f), new(x, .4f, 0), scale: new(.3f, .02f, .3f));
                    foreach (float z in new[] { -.25f, .25f })
                        Part(root, Cube, Plain(C("#c79a5a"), .3f), new(x, .18f, z), scale: new(.16f, .36f, .14f));
                }
                Part(root, Cylinder, metal, new(0, .7f, 0), scale: new(.03f, .7f, .03f));
                Spin(root, P(0, 1.2f, .75f, 1.1f, 0, 1.36f), Paint((u, v) => (int)(u * 8) % 2 == 0 ? C("#e8423c") : Color.white, .4f));
                break;
            }
            case 0 when r < .73f: // Guimard metro entrance: green railings round the stairs, two curving posts with orange
            {                     // lamps, the METROPOLITAIN sign between them
                var green = Plain(C("#3e6b4f"), .5f);
                Part(root, Cube, Plain(C("#2a2d35"), .2f), new(0, .01f, 0), scale: new(.6f, .02f, .9f)); // the stairs going down
                foreach (float x in new[] { -.32f, .32f }) Part(root, Cube, green, new(x, .15f, .05f), scale: new(.04f, .3f, .8f));
                Part(root, Cube, green, new(0, .15f, .45f), scale: new(.68f, .3f, .04f));
                foreach (int k in new[] { -1, 1 })
                {
                    Part(root, Cylinder, green, new(k * .32f, .45f, -.4f), new(0, 0, k * -6), scale: new(.05f, .45f, .05f));
                    Part(root, Sphere, Plain(C("#f29a3a"), .8f), new(k * .27f, .95f, -.4f), scale: new(.1f, .16f, .1f));
                }
                Part(root, Cube, green, new(0, .78f, -.4f), scale: new(.56f, .14f, .03f));
                Part(root, Cube, Plain(C("#f2e6c8"), .4f), new(0, .78f, -.42f), scale: new(.48f, .08f, .01f));
                break;
            }
            case 0: // plane tree in its cast-iron grate: pale mottled bark
                Part(root, Cylinder, Plain(C("#3a3d44"), .4f), new(0, .01f, 0), scale: new(.7f, .01f, .7f));
                Part(root, Cylinder, Paint((u, v) => Mathf.PerlinNoise(u * 8, v * 8) > .55f ? C("#d9cfb8") : C("#9a8a6a"), .2f), new(0, .3f * s, 0), scale: new(.15f, .3f * s, .15f));
                Part(root, Sphere, Plain(C("#6cbb52"), .25f), new(0, .85f * s, 0), scale: new Vector3(1f, .85f, 1f) * s);
                Part(root, Sphere, Plain(C("#82c964"), .25f), new(-.12f * s, 1.05f * s, -.12f * s), scale: new Vector3(.55f, .45f, .55f) * s);
                break;
            case 1 when r < .45f: // Roman palazzo: warm plaster (ochre, burnt orange, yellow or pink), green-shuttered windows, a
            {                     // stone cornice, an arched doorway, a low terracotta roof, flowers on a little balcony
                float h = 1.1f + .7f * r / .45f;
                int tone = (int)(r * 40) % 4;
                palazzo[tone] ??= Paint((u, v) => Mathf.Repeat(u * 4, 1) is > .3f and < .7f && Mathf.Repeat(v * 4, 1) is > .3f and < .75f
                    ? (Mathf.Repeat(u * 4, 1) is > .42f and < .58f ? C("#2a3240") : C("#3f6b45")) // a dark pane between green shutters
                    : C(new[] { "#e9b46a", "#d9824a", "#efcf7a", "#e8a58f" }[tone]), .25f);
                var stone = Plain(C("#efe3cc"), .3f);
                var tiles = Plain(C("#c8643c"), .3f);
                Part(root, Cube, palazzo[tone], new(0, h / 2, 0), scale: new(1.3f, h, 1f));
                Part(root, Cube, stone, new(0, h - .03f, 0), scale: new(1.36f, .08f, 1.06f)); // cornice
                Part(root, Cube, stone, new(0, .22f, 0), scale: new(1.32f, .44f, 1.02f)); // rusticated ground floor
                Part(root, Cube, Plain(C("#5a3a22"), .3f), new(0, .18f, -.515f), scale: new(.3f, .36f, .02f)); // the door
                Part(root, Cylinder, Plain(C("#5a3a22"), .3f), new(0, .36f, -.515f), new(90, 0, 0), new(.3f, .01f, .3f)); // its arch
                Part(root, Prism(new(-.68f, h + .02f, -.54f), new(-.68f, h + .02f, .54f), new(-.68f, h + .3f, 0), new(1.36f, 0, 0)), tiles, Vector3.zero);
                Part(root, Cube, Plain(C("#2a2d35"), .4f), new(0, .62f, -.55f), scale: new(.5f, .04f, .1f)); // balcony
                for (int k = 0; k < 4; k++)
                    Part(root, Sphere, Plain(k % 2 == 0 ? C("#e3263a") : C("#3e9b3a"), .3f), new(-.18f + k * .12f, .68f, -.56f), scale: Vector3.one * .09f);
                break;
            }
            case 1 when r < .51f: // Vespa: a rounded pastel body, a seat, the handlebars, two little wheels
            {
                var paint = Plain(C(r < .48f ? "#9fd8c4" : "#e8423c"), .7f);
                var black = Plain(C("#2a2d35"), .4f);
                Part(root, Sphere, paint, new(0, .2f, .08f), scale: new(.2f, .2f, .42f)); // the rear body
                Part(root, Cube, paint, new(0, .25f, -.2f), new(-15, 0, 0), new(.16f, .34f, .06f)); // the leg shield
                Part(root, Cube, black, new(0, .3f, .1f), scale: new(.12f, .04f, .26f)); // seat
                Part(root, Cube, Plain(C("#d9dde3"), .8f), new(0, .44f, -.22f), scale: new(.3f, .03f, .03f)); // handlebars
                foreach (float z in new[] { -.25f, .25f }) Part(root, Cylinder, black, new(0, .08f, z), new(0, 0, 90), new(.16f, .03f, .16f));
                break;
            }
            case 1 when r < .57f: // Fiat 500: a little round car, pastel, a white roof
            {
                var paint = Plain(C(new[] { "#f2d36b", "#9fd8c4", "#e8423c" }[(int)(r * 100) % 3]), .7f);
                var black = Plain(C("#2a2d35"), .4f);
                Part(root, Sphere, paint, new(0, .2f, 0), scale: new(.44f, .3f, .78f));
                Part(root, Sphere, Plain(C("#9fd8ff"), .9f), new(0, .3f, .04f), scale: new(.38f, .24f, .46f)); // the windows
                Part(root, Sphere, Plain(C("#fbf8f0"), .5f), new(0, .38f, .04f), scale: new(.36f, .1f, .4f)); // the roof
                foreach (float z in new[] { -.24f, .24f })
                    foreach (float x in new[] { -.2f, .2f })
                        Part(root, Cylinder, black, new(x, .08f, z), new(0, 0, 90), new(.15f, .03f, .15f));
                break;
            }
            case 1 when r < .63f: // fountain: a round travertine basin of water, a column pouring in the middle
            {
                var stone = Plain(C("#e8dcc4"), .3f);
                Part(root, Cylinder, stone, new(0, .12f, 0), scale: new(1f, .12f, 1f));
                Part(root, Cylinder, Plain(C("#6fb7e0"), .85f), new(0, .2f, 0), scale: new(.86f, .05f, .86f));
                Part(root, Cylinder, stone, new(0, .45f, 0), scale: new(.14f, .3f, .14f));
                Part(root, Cylinder, stone, new(0, .72f, 0), scale: new(.42f, .04f, .42f)); // the upper bowl
                Part(root, Sphere, Plain(C("#cfe8f5"), .9f), new(0, .82f, 0), scale: new(.12f, .18f, .12f)); // the jet
                break;
            }
            case 1 when r < .68f: // ruins: marble columns of different heights on an old plinth, a fallen drum
            {
                var marble = Plain(C("#efe9dc"), .3f);
                Part(root, Cube, Plain(C("#d9cdb4"), .2f), new(0, .06f, 0), scale: new(1.2f, .12f, .5f));
                for (int k = 0; k < 3; k++)
                {
                    float ch = new[] { 1.3f, .7f, 1.05f }[k];
                    Part(root, Cylinder, Paint((u, v) => Mathf.Repeat(u * 12, 1) < .2f ? C("#d9d2c2") : C("#efe9dc"), .3f), new(-.4f + k * .4f, .12f + ch / 2, 0), scale: new(.2f, ch / 2, .2f));
                    Part(root, Cube, marble, new(-.4f + k * .4f, .14f + ch, 0), scale: new(.28f, .06f, .28f)); // capital
                }
                Part(root, Cylinder, marble, new(.15f, .1f, -.45f), new(0, 30, 90), new(.2f, .12f, .2f));
                break;
            }
            case 1 when r < .71f: // Egyptian obelisk on its pedestal
                Part(root, Cube, Plain(C("#d9cdb4"), .2f), new(0, .15f, 0), scale: new(.5f, .3f, .5f));
                Spin(root, P(.14f, .3f, .1f, 1.8f, 0, 1.95f), Plain(C("#e3d2b0"), .3f), segments: 4);
                break;
            case 1 when r < .78f: // café terrace: two tables under a green and white umbrella
            {
                var metal = Plain(C("#2a2d35"), .5f);
                foreach (float x in new[] { -.35f, .35f })
                {
                    Part(root, Cylinder, metal, new(x, .2f, 0), scale: new(.03f, .2f, .03f));
                    Part(root, Cylinder, Plain(C("#fbf8f0"), .4f), new(x, .4f, 0), scale: new(.3f, .02f, .3f));
                    foreach (float z in new[] { -.25f, .25f })
                        Part(root, Cube, Plain(C("#2a2d35"), .4f), new(x, .18f, z), scale: new(.14f, .36f, .12f));
                }
                Part(root, Cylinder, metal, new(0, .7f, 0), scale: new(.03f, .7f, .03f));
                Spin(root, P(0, 1.2f, .75f, 1.1f, 0, 1.36f), Paint((u, v) => (int)(u * 8) % 2 == 0 ? C("#3e9b4f") : Color.white, .4f));
                break;
            }
            case 1 when r < .93f: // Roman stone pine: a tall bare trunk, a flat dark umbrella of needles on top
            {
                var top = Tilted(root, new(0, r * 360, 8));
                Part(top, Cylinder, Plain(C("#9a6a48"), .2f), new(0, .8f * s, 0), scale: new(.1f, .8f * s, .1f));
                Part(top, Sphere, Plain(C("#3f7a3a"), .25f), new(0, 1.7f * s, 0), scale: new Vector3(1.5f, .4f, 1.3f) * s);
                Part(top, Sphere, Plain(C("#4f8c45"), .25f), new(.15f, 1.82f * s, -.1f), scale: new Vector3(1f, .28f, .9f) * s);
                break;
            }
            case 1: // cypress
                Part(root, Cylinder, trunk, new(0, .15f, 0), scale: new(.1f, .15f, .1f));
                Part(root, Sphere, Plain(C("#2f6b3a"), .25f), new(0, 1f * s, 0), scale: new Vector3(.45f, 1.8f, .45f) * s);
                break;
            case 2 when r < .12f: // five-storey pagoda (Chureito): white walls between red posts, red railings, wide grey-green
            {                     // roofs whose corners flare up, a dark spire of rings on top
                var red = Plain(C("#c8322c"), .4f);
                var roof = Plain(C("#4f5e5c"), .45f);
                var wall = Plain(C("#f4ece0"), .3f);
                int tiers = 3 + (int)(r / .06f); // 3 or 4
                float y = 0;
                for (int k = 0; k < tiers; k++)
                {
                    float sz = .78f - k * .1f, hh = k == 0 ? .42f : .32f;
                    Part(root, Cube, wall, new(0, y + hh / 2, 0), scale: new(sz, hh, sz));
                    foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        Part(root, Cube, red, new(x * sz / 2, y + hh / 2, z * sz / 2), scale: new(.06f, hh, .06f)); // corner posts
                    Part(root, Cube, red, new(0, y + hh * .35f, -sz / 2 - .02f), scale: new(sz * .5f, hh * .5f, .02f)); // red door panel
                    if (k > 0) Part(root, Cube, red, new(0, y + .04f, 0), scale: new(sz + .14f, .06f, sz + .14f)); // the railing round the balcony
                    float ry = y + hh;
                    Part(root, Cube, red, new(0, ry + .02f, 0), scale: new(sz + .2f, .05f, sz + .2f)); // red bracket band under the eaves
                    Part(root, Cube, roof, new(0, ry + .07f, 0), scale: new(sz + .62f, .06f, sz + .62f));
                    Part(root, Cube, roof, new(0, ry + .13f, 0), scale: new(sz + .3f, .08f, sz + .3f));
                    foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }) // the flared corners
                        Part(root, Cube, roof, new(x * (sz + .56f) / 2, ry + .12f, z * (sz + .56f) / 2), new(z * 20, x * z * 45, -x * 20), new(.22f, .05f, .12f));
                    y = ry + .17f;
                }
                var spire = Plain(C("#3a4450"), .5f);
                Part(root, Cylinder, spire, new(0, y + .3f, 0), scale: new(.04f, .3f, .04f));
                for (int k = 0; k < 6; k++) Part(root, Cylinder, spire, new(0, y + .12f + k * .07f, 0), scale: new(.12f, .015f, .12f));
                break;
            }
            case 2 when r < .3f: // machiya town house: dark wood lattice front, white plaster upstairs, a grey tiled roof, a
            {                     // red lantern at the door
                float h = .9f + .4f * (r - .12f) / .18f;
                var wood = Plain(C("#5a3a24"), .3f);
                var tiles = Plain(C("#4a4f58"), .4f);
                Part(root, Cube, Plain(C("#f2ebdc"), .3f), new(0, h / 2, 0), scale: new(1.2f, h, .9f));
                Part(root, Cube, Paint((u, v) => Mathf.Repeat(u * 14, 1) < .35f ? C("#3e2716") : C("#8a6040"), .3f), new(0, .28f, -.46f), scale: new(1.2f, .56f, .02f));
                Part(root, Cube, tiles, new(0, .62f, -.52f), new(-25, 0, 0), new(1.3f, .04f, .26f)); // the little roof over the ground floor
                Part(root, Cube, wood, new(0, h * .78f, -.46f), scale: new(.8f, .16f, .02f)); // upstairs slats
                Part(root, Prism(new(-.64f, h, -.52f), new(-.64f, h, .52f), new(-.64f, h + .32f, 0), new(1.28f, 0, 0)), tiles, Vector3.zero);
                Part(root, Sphere, Plain(C("#e8423c"), .6f), new(.45f, .46f, -.55f), scale: new(.12f, .18f, .12f));
                break;
            }
            case 2 when r < .36f: // torii: two red pillars, the straight tie beam, the black top beam curving up at its ends
            {
                var red = Plain(C("#d33a2c"), .4f);
                var black = Plain(C("#2a2d35"), .4f);
                foreach (float x in new[] { -.5f, .5f })
                {
                    Part(root, Cylinder, red, new(x, .6f, 0), scale: new(.1f, .6f, .1f));
                    Part(root, Cylinder, black, new(x, .05f, 0), scale: new(.14f, .05f, .14f));
                }
                Part(root, Cube, red, new(0, 1f, 0), scale: new(1.25f, .08f, .08f));
                Part(root, Cube, red, new(0, 1.2f, 0), scale: new(1.4f, .1f, .12f));
                Part(root, Cube, black, new(0, 1.29f, 0), scale: new(1.5f, .07f, .16f));
                foreach (int k in new[] { -1, 1 }) Part(root, Cube, black, new(k * .82f, 1.31f, 0), new(0, 0, k * 14), new(.2f, .07f, .16f));
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
            case 2 when r < .47f: // red paper lanterns glowing on a wooden post
            {
                var wood = Plain(C("#6b4a2e"), .3f);
                Part(root, Cylinder, wood, new(0, .6f, 0), scale: new(.05f, .6f, .05f));
                Part(root, Cube, wood, new(0, 1.18f, 0), scale: new(.6f, .04f, .04f));
                var glow = Plain(C("#e8423c"), .61f); // its own smoothness: Plain shares materials, this one glows
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", C("#ff6a3a") * .6f);
                foreach (float x in new[] { -.25f, .25f })
                {
                    Part(root, Sphere, glow, new(x, 1f, 0), scale: new(.16f, .22f, .16f));
                    Part(root, Cylinder, Plain(C("#2a2d35"), .4f), new(x, 1.12f, 0), scale: new(.08f, .015f, .08f));
                }
                break;
            }
            case 2 when r < .52f: // koi pond: a ring of stones round blue water, three koi
            {
                Part(root, Cylinder, Plain(C("#9a9c96"), .2f), new(0, .02f, 0), scale: new(1.3f, .04f, 1f));
                Part(root, Cylinder, Plain(C("#5fa3d6"), .8f), new(0, .04f, 0), scale: new(1.1f, .02f, .82f));
                for (int k = 0; k < 3; k++)
                    Part(root, Sphere, Plain(C(k == 1 ? "#fbf8f0" : "#f07a2a"), .6f), new(-.25f + k * .25f, .06f, (k - 1) * .15f), new(0, k * 70, 0),
                        new(.16f, .03f, .07f));
                break;
            }
            case 2: // cherry tree in full bloom: a dark crooked trunk, billowing pink clouds of blossom, petals fallen round it
            {
                var bark = Plain(C("#4a3328"), .2f);
                Part(root, Cylinder, Plain(C("#f6c9d8"), .2f), new(0, .005f, 0), scale: new(1.3f * s, .005f, 1.3f * s)); // fallen petals
                Part(root, Cylinder, bark, new(0, .3f * s, 0), new(0, 0, 8), new(.14f, .3f * s, .14f));
                Part(root, Cylinder, bark, new(.15f * s, .65f * s, 0), new(0, 0, -35), new(.07f, .22f * s, .07f));
                string[] pinks = { "#f7c6d6", "#f4adc6", "#fbd9e4", "#f29fbd" };
                for (int k = 0; k < 5; k++)
                {
                    float a = k * 1.26f + r * 9;
                    var c = C(pinks[(k + (int)(r * 40)) % 4]);
                    Part(root, Sphere, Plain(c, .2f), new(Mathf.Cos(a) * .35f * s, (.85f + .15f * (k % 2)) * s, Mathf.Sin(a) * .3f * s), scale: Vector3.one * (.6f + .1f * (k % 3)) * s);
                }
                Part(root, Sphere, Plain(C("#fbd9e4"), .2f), new(0, 1.15f * s, 0), scale: Vector3.one * .65f * s);
                break;
            }
            case 3 when r < .28f: // brick apartment block: rows of windows, a black fire escape zigzagging down the front, a
            {                    // wooden water tank on stilts on the roof
                float h = 1.4f + 1.2f * r / .28f;
                brick ??= Paint((u, v) => Mathf.Repeat(u * 4, 1) is > .25f and < .75f && Mathf.Repeat(v * 6, 1) is > .3f and < .75f
                    ? C("#3a4a62") : C("#b5563a"), .25f);
                Part(root, Cube, brick, new(0, h / 2, 0), scale: new(1.2f, h, 1f));
                Part(root, Cube, Plain(C("#8a3f2a"), .3f), new(0, h + .03f, 0), scale: new(1.26f, .08f, 1.06f)); // cornice
                var iron = Plain(C("#23262c"), .5f);
                for (float y = .55f; y < h - .2f; y += .45f) // fire escape landings and their ladders
                {
                    Part(root, Cube, iron, new(.15f, y, -.56f), scale: new(.6f, .03f, .14f));
                    Part(root, Cube, iron, new(.15f, y + .22f, -.62f), new(0, 0, (int)(y / .45f) % 2 == 0 ? 40 : -40), new(.03f, .5f, .02f));
                }
                var wood = Plain(C("#8a6a45"), .2f);
                foreach (float x in new[] { -.15f, .15f }) Part(root, Cylinder, iron, new(x - .2f, h + .2f, .2f), scale: new(.03f, .16f, .03f));
                Part(root, Cylinder, wood, new(-.2f, h + .52f, .2f), scale: new(.3f, .18f, .3f));
                Spin(root, P(.16f, h + .7f, 0, h + .88f), Plain(C("#5a4632"), .2f), new(-.2f, 0, .2f));
                break;
            }
            case 3 when r < .5f: // glass tower: a tall shaft of blue glass with a grid of mullions, a set-back crown
            {
                float h = 2.6f + 1.6f * (r - .28f) / .22f;
                glassTower ??= Paint((u, v) => Mathf.Repeat(u * 6, 1) < .1f || Mathf.Repeat(v * 14, 1) < .12f
                    ? C("#c9d6e3") : Color.Lerp(C("#5b86b8"), C("#8fb6dc"), v), .85f);
                Part(root, Cube, glassTower, new(0, h / 2, 0), scale: new(1f, h, 1f));
                Part(root, Cube, glassTower, new(0, h + .3f, 0), scale: new(.7f, .6f, .7f));
                Part(root, Cylinder, Plain(C("#d9dde3"), .8f), new(0, h + .9f, 0), scale: new(.05f, .3f, .05f)); // antenna
                break;
            }
            case 3 when r < .57f: // yellow cab
            {
                var yellow = Plain(C("#f5c518"), .5f);
                var black = Plain(C("#2a2d35"), .4f);
                Part(root, Cube, yellow, new(0, .16f, 0), scale: new(.42f, .18f, .9f));
                Part(root, Cube, yellow, new(0, .3f, .05f), scale: new(.38f, .14f, .46f)); // the cabin
                Part(root, Cube, Plain(C("#9fd8ff"), .9f), new(0, .3f, .05f), scale: new(.39f, .1f, .4f)); // its windows
                Part(root, Cube, Paint((u, v) => Mathf.Repeat(u * 6, 1) < .5f == v < .5f ? Color.white : C("#2a2d35"), .3f),
                    new(0, .2f, 0), scale: new(.43f, .04f, .91f)); // checker stripe
                Part(root, Cube, Plain(Color.white, .6f), new(0, .4f, .05f), scale: new(.14f, .06f, .06f)); // roof light
                foreach (float z in new[] { -.3f, .3f })
                    foreach (float x in new[] { -.2f, .2f })
                        Part(root, Cylinder, black, new(x, .08f, z), new(0, 0, 90), new(.16f, .03f, .16f));
                break;
            }
            case 3 when r < .63f: // a red fire hydrant beside a blue mailbox
            {
                var red = Plain(C("#d7192a"), .5f);
                Part(root, Cylinder, red, new(-.2f, .16f, 0), scale: new(.12f, .16f, .12f));
                Part(root, Sphere, red, new(-.2f, .32f, 0), scale: Vector3.one * .13f);
                Part(root, Cylinder, red, new(-.2f, .2f, 0), new(0, 0, 90), new(.06f, .1f, .06f));
                Part(root, Cube, Plain(C("#2f5aa8"), .5f), new(.22f, .28f, 0), scale: new(.26f, .4f, .24f));
                Spin(root, P(.13f, .48f, 0, .56f), Plain(C("#2f5aa8"), .5f), new(.22f, 0, 0));
                break;
            }
            case 3 when r < .68f: // hot dog cart under a striped umbrella
            {
                Part(root, Cube, Plain(C("#dfe4ea"), .9f), new(0, .3f, 0), scale: new(.7f, .36f, .4f));
                Part(root, Cube, Plain(C("#e8423c"), .5f), new(0, .3f, -.205f), scale: new(.6f, .12f, .01f));
                foreach (float x in new[] { -.25f, .25f }) Part(root, Cylinder, Plain(C("#2a2d35"), .4f), new(x, .1f, .22f), new(0, 0, 90), new(.18f, .02f, .18f));
                Part(root, Cylinder, Plain(C("#8a8f99"), .5f), new(0, .75f, 0), scale: new(.03f, .3f, .03f));
                Spin(root, P(0, 1.02f, .5f, .92f, 0, 1.12f), Paint((u, v) => (int)(u * 8) % 2 == 0 ? C("#f5c518") : C("#3c6fa8"), .4f));
                break;
            }
            case 3 when r < .74f: // the Stars and Stripes on a flagpole
                Part(root, Cylinder, Plain(C("#d9dde3"), .7f), new(0, .8f, 0), scale: new(.04f, .8f, .04f));
                Part(root, Sphere, Plain(C("#e8c64a"), .7f), new(0, 1.62f, 0), scale: Vector3.one * .07f);
                Part(root, Cube, Paint((u, v) => u < .42f && v > .5f
                        ? (Mathf.Repeat(u * 14, 1) < .4f && Mathf.Repeat(v * 10, 1) < .4f ? Color.white : C("#2a3f7a"))
                        : (int)(v * 13) % 2 == 0 ? C("#c8322c") : Color.white, .3f),
                    new(.33f, 1.38f, 0), scale: new(.6f, .38f, .015f));
                break;
            case 3 when r < .78f: // billboard on stilts: a burger and a soda on red
            {
                var wood = Plain(C("#8a6a45"), .2f);
                foreach (float x in new[] { -.5f, .5f }) Part(root, Cylinder, wood, new(x, .55f, .05f), scale: new(.06f, .55f, .06f));
                Part(root, Cube, Plain(C("#fbf8f0"), .3f), new(0, 1.2f, 0), scale: new(1.5f, .7f, .06f));
                Part(root, Cube, Plain(C("#d7192a"), .4f), new(0, 1.2f, -.035f), scale: new(1.4f, .6f, .01f));
                Part(root, Sphere, Plain(C("#e3a656"), .4f), new(-.35f, 1.26f, -.05f), scale: new(.34f, .18f, .02f)); // bun
                Part(root, Cube, Plain(C("#6b3a1e"), .3f), new(-.35f, 1.15f, -.05f), scale: new(.34f, .07f, .02f)); // patty
                Part(root, Cube, Plain(Color.white, .3f), new(.3f, 1.2f, -.05f), scale: new(.18f, .36f, .02f)); // soda cup
                Part(root, Cube, Plain(C("#d7192a"), .3f), new(.3f, 1.2f, -.06f), scale: new(.18f, .08f, .01f));
                break;
            }
            case 3: // a street tree in its square planter
                Part(root, Cube, Plain(C("#8f949c"), .2f), new(0, .06f, 0), scale: new(.6f, .12f, .6f));
                RoundTree(C(r < .9f ? "#5fae4a" : "#4f9a3e"));
                break;
        }
        return root;
    }
}
