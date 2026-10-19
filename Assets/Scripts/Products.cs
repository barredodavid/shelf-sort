using System.Collections.Generic;
using UnityEngine;

// Procedural products: a silhouette (profile) spun around the vertical axis into a mesh, painted with a generated
// label texture. One shape + different colors/labels = many distinct products, no art files.
public static class Products
{
    // Soda bottle profile, (radius, height) from the bottom center to the top center
    static readonly Vector2[] BottleProfile =
    {
        new(0, 0), new(.27f, 0), new(.32f, .03f), new(.34f, .1f), new(.34f, .72f), new(.31f, .84f),
        new(.2f, .97f), new(.13f, 1.04f), new(.13f, 1.1f), new(.155f, 1.115f), new(.155f, 1.27f), new(.13f, 1.3f), new(0, 1.3f),
    };
    const float BottleH = 1.3f, BottleR = .34f;
    // u that ends up facing the camera: the side at -Z (u = .75), shifted by the 35° yaw Game.Wrap gives products
    const float FrontU = .75f + 35f / 360f;

    // liquid: height (0..1 of the bottle) up to which the body shows `body`; above it, clear plastic.
    // text: written on the label (letters from Glyphs only); null draws a round logo instead.
    public static GameObject Bottle(Color body, Color label, Color cap, string text = null, float liquid = 1)
    {
        var go = new GameObject("Bottle");
        go.AddComponent<MeshFilter>().sharedMesh = Lathe(BottleProfile, 28);
        var mat = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
        {
            mainTexture = BottleTexture(body, label, cap, 1.11f / BottleH, liquid, text),
            color = Color.white,
        };
        mat.SetFloat("_Smoothness", .6f); // glossy plastic
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // Spins a profile around Y. u runs around the axis, v up the height, so a texture paints it like a label.
    static Mesh Lathe(Vector2[] profile, int segments)
    {
        float top = profile[^1].y;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int p = 0; p < profile.Length; p++)
            for (int s = 0; s <= segments; s++) // one extra column: the texture seam needs its own vertices
            {
                float a = 2 * Mathf.PI * s / segments;
                verts.Add(new Vector3(Mathf.Cos(a) * profile[p].x, profile[p].y, Mathf.Sin(a) * profile[p].x));
                uvs.Add(new Vector2((float)s / segments, profile[p].y / top));
            }
        int row = segments + 1;
        for (int p = 0; p < profile.Length - 1; p++)
            for (int s = 0; s < segments; s++)
            {
                int a = p * row + s, b = a + row;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Liquid with a soft vertical sheen up to `liquid`, clear plastic above, a label band (borders, a wave, then
    // either text or a round logo on each side), and a ridged cap from capFrom upward
    static Texture2D BottleTexture(Color body, Color label, Color cap, float capFrom, float liquid, string text)
    {
        const int w = 256, h = 256;
        const float labelLo = .23f, labelHi = .58f;
        var px = new Color[w * h];
        var cream = new Color(1f, .97f, .9f);
        var clear = new Color(.82f, .88f, .92f);
        var strokes = text != null ? TextStrokes(text) : null;
        float circ = 2 * Mathf.PI * BottleR; // label space in real units, so letters aren't stretched around the bottle
        float textH = (labelHi - labelLo) * BottleH * .55f, thick = .15f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w, v = (float)y / h;
                float sheen = .85f + .3f * Mathf.Pow(Mathf.Sin(u * Mathf.PI * 2) * .5f + .5f, 4);
                Color c;
                if (v >= capFrom)
                    c = cap * (x / 6 % 2 == 0 ? 1f : .85f); // ridges
                else if (v >= labelLo && v <= labelHi)
                {
                    float lv = (v - labelLo) / (labelHi - labelLo);
                    if (strokes != null)
                    {
                        c = label;
                        if (lv < .07f || lv > .93f) c = cream; // borders
                        // the word, once per side of the bottle, centered in the band
                        for (int side = 0; side < 2; side++)
                        {
                            float gx = (Mathf.Repeat(u - FrontU - .5f * side + .5f, 1) - .5f) * circ / textH + strokes.width / 2;
                            float gy = (lv - .5f) * (labelHi - labelLo) * BottleH / textH + .5f;
                            float d = strokes.Distance(new Vector2(gx, gy));
                            float pixel = circ / w / textH;
                            c = Color.Lerp(c, Color.white, Mathf.Clamp01((thick / 2 - d) / pixel + .5f));
                        }
                    }
                    else
                    {
                        c = cream;
                        if (lv < .08f || lv > .92f) c = label; // borders
                        float wave = .3f + .08f * Mathf.Sin(u * Mathf.PI * 8);
                        if (lv < wave && lv > .08f) c = Color.Lerp(label, cream, .35f); // wave along the bottom
                        for (int side = 0; side < 2; side++)
                        {
                            float dx = (Mathf.Repeat(u - FrontU - .5f * side + .5f, 1) - .5f) * 4.2f, dy = lv - .6f;
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            if (r < .28f) c = label;
                            if (r < .14f) c = cream;
                            if (r < .08f) c = cap;
                        }
                    }
                }
                else
                    c = (v < liquid ? body : clear) * sheen;
                c.a = 1;
                px[y * w + x] = c;
            }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // ---------- a tiny stroke font: letters as polylines in a .6 x 1 box, drawn with round, thick pens ----------
    // ponytail: only the letters used so far; add more to Glyphs as product names need them

    class Strokes
    {
        public readonly List<(Vector2 a, Vector2 b)> Segments = new();
        public float width;

        public float Distance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var (a, b) in Segments)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }
    }

    static List<Vector2> Arc(Vector2 c, Vector2 r, float fromDeg, float toDeg, int n = 14)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (float)i / n);
            pts.Add(c + new Vector2(Mathf.Cos(a) * r.x, Mathf.Sin(a) * r.y));
        }
        return pts;
    }

    static readonly Dictionary<char, List<Vector2>[]> Glyphs = new()
    {
        ['O'] = new[] { Arc(new(.3f, .5f), new(.27f, .43f), 0, 360, 28) },
        ['D'] = new[] { new List<Vector2> { new(.06f, .07f), new(.06f, .93f) }, Arc(new(.06f, .5f), new(.48f, .43f), 90, -90, 20) },
        ['A'] = new[] { new List<Vector2> { new(.03f, .07f), new(.3f, .93f), new(.57f, .07f) }, new List<Vector2> { new(.14f, .38f), new(.46f, .38f) } },
        ['S'] = new[]
        {
            Merge(Arc(new(.3f, .715f), new(.25f, .215f), 25, 270), Arc(new(.3f, .285f), new(.25f, .215f), 90, -155)),
        },
    };

    static List<Vector2> Merge(List<Vector2> a, List<Vector2> b)
    {
        a.AddRange(b);
        return a;
    }

    static Strokes TextStrokes(string text)
    {
        var s = new Strokes();
        const float advance = .6f, gap = .2f;
        float x = 0;
        foreach (char ch in text.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(ch, out var lines))
                foreach (var line in lines)
                    for (int i = 0; i + 1 < line.Count; i++)
                        s.Segments.Add((line[i] + new Vector2(x, 0), line[i + 1] + new Vector2(x, 0)));
            x += advance + gap;
        }
        s.width = x - gap;
        return s;
    }
}
