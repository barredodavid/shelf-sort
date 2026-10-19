using System.Collections.Generic;
using UnityEngine;

// 3D pieces of the level map (Map.cs), built like the products from primitives and plain colors:
// road ribbons, level pads, world gates, barriers and per-world decor.
public static partial class Products
{
    // each world's ground and its decor
    public static Color Ground(int w) => C(new[] { "#9ad66f", "#f2d98c", "#7fc95e", "#c3c9d6" }[w]); // a method: C() needs hexCache, which may not be initialized yet in a static field

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

    // round level stop: darker base, colored top; its top face is at y = .32
    public static GameObject Pad(Color c)
    {
        var root = new GameObject("Pad");
        Part(root, Cylinder, Plain(c * .7f + new Color(0, 0, 0, .3f), .3f), new(0, .08f, 0), scale: new(1.3f, .08f, 1.3f));
        Part(root, Cylinder, Plain(c, .55f), new(0, .22f, 0), scale: new(1.12f, .1f, 1.12f));
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

    // one piece of scenery for world w; r (0..1) picks the variant and its size
    public static GameObject Decor(int w, float r)
    {
        var root = new GameObject("Decor");
        float s = .8f + .5f * r;
        switch (w)
        {
            case 1 when r < .5f: // hay bale lying in the field
                Part(root, Cylinder, Plain(C("#e6bf5a"), .2f), new(0, .3f, 0), new(0, r * 300, 90), new(.6f, .4f, .6f));
                Part(root, Cylinder, Plain(C("#c99a3d"), .2f), new(0, .3f, 0), new(0, r * 300, 90), new(.45f, .41f, .45f));
                break;
            case 3 when r < .55f: // little building with rows of windows
            {
                float h = 1 + 2.4f * r;
                var wall = new[] { C("#f6a5c0"), C("#9fd8ff"), C("#ffd27a"), C("#c7b3f2") }[(int)(r * 40) % 4];
                Part(root, Cube, Plain(wall, .3f), new(0, h / 2, 0), scale: new(1.1f, h, 1.1f));
                Part(root, Cube, Plain(wall * .8f + new Color(0, 0, 0, .2f), .3f), new(0, h + .06f, 0), scale: new(1.2f, .12f, 1.2f));
                var glass = Plain(C("#4a5a78"), .8f);
                for (float y = .35f; y < h - .2f; y += .45f)
                    Part(root, Cube, glass, new(0, y, -.56f), scale: new(.8f, .2f, .02f));
                break;
            }
            case 3: // street lamp
                Part(root, Cylinder, Plain(C("#3a3f4b"), .5f), new(0, .7f, 0), scale: new(.08f, .7f, .08f));
                Part(root, Sphere, Plain(C("#fff2b0"), .9f), new(0, 1.5f, 0), scale: Vector3.one * .3f);
                break;
            default: // round tree; apple tree in the greengrocer's world, golden in the bakery's
            {
                if (r < .25f) // bush
                {
                    Part(root, Sphere, Plain(C("#4fa83d"), .25f), new(0, .25f, 0), scale: new(.7f, .5f, .7f));
                    break;
                }
                var leaf = w == 1 ? C("#c9b84a") : w == 2 ? C("#4fa83d") : C("#5bb04a");
                Part(root, Cylinder, Plain(C("#8a5a35"), .2f), new(0, .3f * s, 0), scale: new(.14f, .3f * s, .14f));
                Part(root, Sphere, Plain(leaf, .25f), new(0, .85f * s, 0), scale: new Vector3(.95f, .85f, .95f) * s);
                Part(root, Sphere, Plain(leaf * 1.2f, .25f), new(-.12f * s, 1.05f * s, -.12f * s), scale: new Vector3(.55f, .45f, .55f) * s);
                if (w == 2)
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * 1.3f + r * 6;
                        Part(root, Sphere, Plain(C("#e3263a"), .6f), new(Mathf.Cos(a) * .42f * s, (.7f + .25f * Mathf.Sin(a * 2)) * s, Mathf.Sin(a) * .42f * s), scale: Vector3.one * .16f);
                    }
                break;
            }
        }
        return root;
    }
}
