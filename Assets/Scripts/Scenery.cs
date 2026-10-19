using System.Collections.Generic;
using UnityEngine;

// 3D pieces of the level map (Map.cs), built like the products from primitives and plain colors:
// road ribbons, level pads, world gates, barriers and per-world decor.
public static partial class Products
{
    // each world's ground and its decor
    public static Color Ground(int w) => C(new[] { "#9ad66f", "#cfd88a", "#a9dc8f", "#ecc98f" }[w]); // a method: C() needs hexCache, which may not be initialized yet in a static field

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

    // orange traffic cone with a white band
    public static GameObject Cone()
    {
        var root = new GameObject("Cone");
        Part(root, Cube, Plain(C("#e8742a"), .4f), new(0, .02f, 0), scale: new(.42f, .04f, .42f));
        Spin(root, P(0, .04f, .16f, .04f, .03f, .6f, 0, .6f), Paint((u, v) => v > .45f && v < .62f ? Color.white : C("#f08a24"), .5f));
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

    // red and white barrier across the road, for a world still locked
    public static GameObject Barrier()
    {
        var root = new GameObject("Barrier");
        var dark = Plain(C("#3a3f4b"), .3f);
        foreach (float x in new[] { -1.05f, 1.05f }) Part(root, Cylinder, dark, new(x, .3f, 0), scale: new(.1f, .3f, .1f));
        for (int i = 0; i < 6; i++)
            Part(root, Cube, Plain(i % 2 == 0 ? C("#e8423c") : Color.white, .5f), new(-.92f + i * .37f, .62f, 0), scale: new(.37f, .18f, .12f));
        return root;
    }

    // one piece of scenery for country w; r (0..1) picks the variant and its size.
    // France: plane trees, lamp posts, Morris columns, café tables and flower beds; Italy: cypresses and olive bushes; Japan: cherry trees and stone
    // lanterns; USA: palm trees and cacti.
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
            case 0 when r < .3f: // Paris lamp post
                Part(root, Cylinder, Plain(C("#2f4a3a"), .5f), new(0, .7f, 0), scale: new(.07f, .7f, .07f));
                Part(root, Cube, Plain(C("#2f4a3a"), .5f), new(0, 1.42f, 0), scale: new(.22f, .06f, .22f));
                Part(root, Sphere, Plain(C("#fff2b0"), .9f), new(0, 1.55f, 0), scale: Vector3.one * .22f);
                break;
            case 0 when r < .4f: // Morris column, dark green with a little dome
            {
                var green = Plain(C("#2f4a3a"), .5f);
                Part(root, Cylinder, Paint((u, v) => Mathf.Repeat(u * 5, 1) < .5f ? C("#f2e6c8") : C("#e8b25a"), .3f), new(0, .6f, 0), scale: new(.34f, .45f, .34f));
                Part(root, Cylinder, green, new(0, .1f, 0), scale: new(.4f, .1f, .4f));
                Part(root, Cylinder, green, new(0, 1.08f, 0), scale: new(.42f, .04f, .42f));
                Spin(root, Ball(.2f, 1.1f, .8f), green);
                break;
            }
            case 0 when r < .52f: // café table under a red and white umbrella, two chairs
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
            case 0 when r < .62f: // round flower bed
                Part(root, Cylinder, Plain(C("#cfc6b4"), .2f), new(0, .08f, 0), scale: new(.9f, .08f, .9f));
                Part(root, Sphere, Paint((u, v) => Dot(u, v, 14, 8, .5f, .35f, 2, out float k) ? (k < .25f ? C("#e3263a") : C("#f7b6cf")) : C("#4fa83d"), .3f),
                    new(0, .2f, 0), scale: new(.8f, .3f, .8f));
                break;
            case 0: // plane tree: pale mottled bark
                Part(root, Cylinder, Paint((u, v) => Mathf.PerlinNoise(u * 8, v * 8) > .55f ? C("#d9cfb8") : C("#9a8a6a"), .2f), new(0, .3f * s, 0), scale: new(.15f, .3f * s, .15f));
                Part(root, Sphere, Plain(C("#6cbb52"), .25f), new(0, .85f * s, 0), scale: new Vector3(1f, .85f, 1f) * s);
                Part(root, Sphere, Plain(C("#82c964"), .25f), new(-.12f * s, 1.05f * s, -.12f * s), scale: new Vector3(.55f, .45f, .55f) * s);
                break;
            case 1 when r < .4f: // olive bush
                Part(root, Sphere, Plain(C("#8fa36a"), .2f), new(0, .3f, 0), scale: new Vector3(.8f, .55f, .8f) * s);
                break;
            case 1: // cypress
                Part(root, Cylinder, trunk, new(0, .15f, 0), scale: new(.1f, .15f, .1f));
                Part(root, Sphere, Plain(C("#2f6b3a"), .25f), new(0, 1f * s, 0), scale: new Vector3(.45f, 1.8f, .45f) * s);
                break;
            case 2 when r < .3f: // stone lantern
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
            case 3 when r < .4f: // cactus
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
