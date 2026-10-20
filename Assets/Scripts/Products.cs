using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Procedural product factory: shapes spun from a silhouette (bottle, jar), built face by face (carton, cereal box,
// crisps bag, cheese wedge) or assembled from primitives (eggs), painted with generated textures and names written
// in a small stroke font. Food.cs holds each country's catalog and its other products.
public static partial class Products
{
    public const int Count = 12;

    // product `id` of world `w`: one country per world (catalogs in Food.cs)
    public static GameObject Make(int w, int id) => w switch
    {
        0 => France(id),
        1 => Italy(id),
        2 => Japan(id),
        _ => Usa(id),
    };

    // "#rrggbb" -> Color, cached: paint functions call it for every texel
    static readonly Dictionary<string, Color> hexCache = new();
    static Color C(string hex)
    {
        if (!hexCache.TryGetValue(hex, out var c))
            hexCache[hex] = c = ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.magenta;
        return c;
    }

    // ---------- shapes ----------

    // how much of its slot a product fills (Game.Wrap): 1 for most; less for those that look too big at full size
    // (or more for wide, flat ones that would look tiny)
    public static float Fill(string name) => name.StartsWith("Pasta") || name.StartsWith("Frites") ? .82f : name.StartsWith("Gyoza") || name.StartsWith("Yakitori") ? 1.25f : 1;

    // u that ends up facing the camera: the side at -Z (u = .75), shifted by the 35° yaw Game.Wrap gives products
    const float FrontU = .75f + 35f / 360f;

    // Bordeaux bottle: dark green glass, red wine, burgundy foil capsule, cream label with a gold frame
    static GameObject Wine()
    {
        const float H = 1.42f, R = .27f, capFrom = 1.2f / H, labelLo = .2f, labelHi = .52f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            if (v >= capFrom) return C("#6e1428") * (.85f + .25f * Sheen(u).r);
            if (v >= labelLo && v <= labelHi)
            {
                float f = (v - labelLo) / (labelHi - labelLo);
                return f < .07f || f > .93f ? C("#c9a24a") : C("#f4ead2");
            }
            return C("#1f3a24") * Sheen(u);
        });
        LatheText(cv, "VIN", 2 * Mathf.PI * R, H, (labelLo + labelHi) / 2 * H, (labelHi - labelLo) * H * .5f, C("#6e1428"));
        return Spun("VIN", P(0, 0, .25f, 0, .27f, .03f, .27f, .82f, .25f, .9f, .15f, .99f, .1f, 1.06f, .1f, 1.36f, .105f, 1.4f, 0, 1.42f), cv, .75f);
    }

    static GameObject Carton(string name, Color body, Color accent)
    {
        const float w = .5f, d = .5f, h = .85f, gable = .2f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
            v >= BoxSide ? accent // roof
            : v < .18f * BoxSide || Mathf.Abs(v - .8f * BoxSide) < .02f ? accent // bottom band, thin top line
            : body);
        BoxText(cv, name, w, d, h, .5f, .2f, accent);
        return Built(name, BoxMesh(w, h, d, gable), cv, .3f);
    }

    static GameObject CerealBox(string name, Color main, Color accent, Color cereal)
    {
        const float w = .72f, d = .26f, h = 1.05f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            if (v >= BoxSide || u >= .5f) return main; // top and narrow sides
            // a bowl of cereal low on each big face: white bowl with a colored rim, a heap of cereal on top
            float fu = u / .5f, fv = v / BoxSide;
            float x = (fu - .5f) * w / h, y = fv - .3f; // physical-ish units, origin at the bowl's rim
            float bowl = x * x / (.3f * .3f) + y * y / (.16f * .16f);
            if (y < 0 && bowl < 1) return y > -.025f ? accent : Color.Lerp(Color.white, new Color(.85f, .88f, .92f), -y / .16f);
            float heap = x * x / (.27f * .27f) + y * y / (.12f * .12f);
            if (y >= 0 && heap < 1)
            {
                // round pieces: lit centers, darker gaps between them
                float cx = Mathf.Repeat(x * 30, 1) - .5f, cy = Mathf.Repeat(y * 30 + (int)(x * 30) * .5f, 1) - .5f;
                return cereal * (cx * cx + cy * cy < .16f ? 1.1f : .7f);
            }
            return main;
        });
        BoxText(cv, name, w, d, h, .7f, .2f, Color.white, Darker(main));
        return Built(name, BoxMesh(w, h, d, 0), cv, .3f);
    }

    // half-dozen eggs in an open grey cardboard tray, 2 rows of 3
    static GameObject Eggs()
    {
        var root = new GameObject("Eggs");
        var tray = Plain(C("#d3cab8"), .1f);
        Part(root, Cube, tray, new(0, .08f, 0), scale: new(.92f, .16f, .62f));
        // lid standing open at the back, its printed face towards the player
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v >= BoxSide ? C("#d3cab8") : Mathf.Abs(v / BoxSide - .75f) < .17f && u < .5f ? C("#f6c445") : C("#d3cab8"));
        BoxText(cv, "OEUFS", .92f, .04f, .7f, .75f, .2f, C("#7a4a1c")); // high up: the eggs hide the lower half
        Part(root, BoxMesh(.92f, .7f, .04f, 0), Textured(cv, .2f), new(0, .14f, .3f), new(-12, 0, 0));

        // egg silhouette: round bottom, narrower taller top
        var pts = new Vector2[17];
        for (int i = 0; i <= 16; i++)
        {
            float a = Mathf.Lerp(-90, 90, i / 16f) * Mathf.Deg2Rad, s = Mathf.Sin(a);
            pts[i] = new Vector2(Mathf.Max(0, Mathf.Cos(a) * .14f * (1 - .18f * Mathf.Max(0, s))), .14f + s * (s > 0 ? .21f : .14f));
        }
        var cup = Plain(C("#c4bba8"), .1f);
        for (int i = 0; i < 6; i++)
        {
            var p = new Vector3((i % 3 - 1) * .29f, .14f, (i / 3 - .5f) * .27f);
            Part(root, Cylinder, cup, p + new Vector3(0, .02f, 0), scale: new(.26f, .04f, .26f)); // molded cup
            var egg = Plain(C(i % 3 == 1 ? "#e9c49c" : "#f5e6cf") * (.96f + .08f * (i % 2)), .35f);
            Part(root, Lathe(pts, 20), egg, p);
        }
        return root;
    }

    // bag of crisps: red bag, crimped silver ends, a pile of chips under the name
    static GameObject Chips()
    {
        var root = new GameObject("Chips");
        const float w = .7f, h = .9f, d = .34f;
        var red = C("#e8312f");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            float fu = Mathf.Repeat(u, .5f) / .5f, fv = v / BoxSide;
            if (u < .5f && fv > .1f && fv < .42f && Dot(fu, fv, 6, 5, .8f, .32f, 3, out _)) return C("#f6c445"); // chips
            return red;
        });
        BoxText(cv, "CHIPS", w, d, h, .64f, .22f, C("#ffd23f"), C("#7a1410"));
        Part(root, BoxMesh(w, h, d, 0), Textured(cv, .6f), new(0, .02f, 0));
        var foil = Plain(C("#d9dde3"), .8f);
        Part(root, Cube, foil, new(0, .02f, 0), scale: new(w + .04f, .06f, d * .6f));
        Part(root, Cube, foil, new(0, h + .02f, 0), scale: new(w + .04f, .08f, d * .6f));
        return root;
    }

    // wedge of cheese: a triangular prism, yellow with holes on every face
    static GameObject Cheese()
    {
        var mat = Paint((u, v) => Dot(u, v, 5, 4, .45f, .3f, 8, out _) ? C("#e8a92a") : C("#ffd23f"), .35f);
        var go = new GameObject("Cheese");
        Part(go, Prism(new(-.48f, 0, -.28f), new(.48f, 0, -.28f), new(0, 0, .5f), new(0, .42f, 0)), mat, Vector3.zero);
        return go;
    }

    // triangle a b c pushed along `extrude` into a solid with flat faces. The two triangles map their own
    // (x, z) or (x, y) onto uv; each side face gets the whole 0..1 square.
    static Mesh Prism(Vector3 a, Vector3 b, Vector3 c, Vector3 extrude)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        var center = (a + b + c) / 3 + extrude / 2;
        void Face(Vector3[] p, Vector2[] uv) // flat face, wound to face away from the center
        {
            int i = verts.Count;
            verts.AddRange(p);
            uvs.AddRange(uv);
            for (int k = 1; k + 1 < p.Length; k++)
            {
                bool outward = Vector3.Dot(Vector3.Cross(p[k] - p[0], p[k + 1] - p[0]), (p[0] + p[k] + p[k + 1]) / 3 - center) > 0;
                tris.AddRange(outward ? new[] { i, i + k, i + k + 1 } : new[] { i, i + k + 1, i + k });
            }
        }
        bool flatY = Mathf.Abs(extrude.y) > Mathf.Abs(extrude.z); // triangle lying in XZ, else standing in XY
        Vector2 Uv(Vector3 p) => flatY ? new Vector2(p.x + .5f, p.z + .5f) : new Vector2(p.x + .5f, p.y);
        Face(new[] { a + extrude, b + extrude, c + extrude }, new[] { Uv(a), Uv(b), Uv(c) });
        Face(new[] { a, b, c }, new[] { Uv(a), Uv(b), Uv(c) });
        foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
            Face(new[] { p, q, q + extrude, p + extrude }, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        return Finish(verts, uvs, tris);
    }

    // ---------- obstacles (same in every world) ----------

    // taped cardboard carton with a big "?": hides the product inside
    public static GameObject Mystery()
    {
        const float s = .8f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
            v >= BoxSide || Mathf.Abs(v / BoxSide - .86f) < .06f ? C("#e8d2a6") // tape over the top and round the box
            : C("#c99a5b") * (Mathf.Repeat(u, .5f) < .015f ? .8f : 1f)); // darker folded edges
        BoxText(cv, "?", s, s, s, .42f, .6f, Color.white, C("#6b4320"));
        return Built("Mystery", BoxMesh(s, s, s, 0), cv, .2f);
    }

    // wooden crate: frame, planks and a diagonal brace on every side
    public static GameObject Crate()
    {
        var cv = new Canvas(256, 256);
        var wood = C("#c58a45");
        cv.Paint((u, v) =>
        {
            if (v >= BoxSide) return wood * .9f;
            float fu = Mathf.Repeat(u, .5f) / .5f, fv = v / BoxSide;
            if (fu < .1f || fu > .9f || fv < .1f || fv > .9f || Mathf.Abs(fu - fv) < .07f) return wood * .72f; // frame + brace
            return Mathf.Repeat(fv * 3.7f, 1) < .06f ? wood * .5f : wood * (.95f + .1f * Hash((int)(fv * 3.7f), 0, 9)); // planks
        });
        return Built("Crate", BoxMesh(1, 1, 1, 0), cv, .15f);
    }

    // ---------- painting helpers ----------

    static Color Sheen(float u) => Color.white * (.85f + .3f * Mathf.Pow(Mathf.Sin(u * Mathf.PI * 2) * .5f + .5f, 4));
    static Color Ridged(Color c, float u) => c * ((int)(u * 42) % 2 == 0 ? 1f : .86f);
    static Color Darker(Color c) => new(c.r * .45f, c.g * .45f, c.b * .45f, 1);

    // name on a spun product: once on the side facing the camera, once on the opposite side
    static void LatheText(Canvas cv, string text, float circ, float height, float centerY, float textH, Color color)
    {
        var region = new Rect(0, 0, 1, 1);
        var phys = new Vector2(circ, height);
        for (int side = 0; side < 2; side++)
            cv.Text(text, region, phys, new Vector2(Mathf.Repeat(FrontU + .5f * side, 1) * circ, centerY), textH, circ * .3f, color, wrap: true);
    }

    // Box textures: left half of the texture = front/back faces (w wide), right half = the two sides (d wide),
    // rows up to BoxSide = the faces' height, rows above = lids/roof
    const float BoxSide = .94f;

    static void BoxText(Canvas cv, string text, float w, float d, float h, float yFrac, float sizeFrac, Color color, Color? outline = null)
    {
        cv.Text(text, new Rect(0, 0, .5f, BoxSide), new Vector2(w, h), new Vector2(w / 2, h * yFrac), h * sizeFrac, w * .86f, color, outline);
        cv.Text(text, new Rect(.5f, 0, .5f, BoxSide), new Vector2(d, h), new Vector2(d / 2, h * yFrac), h * sizeFrac, d * .86f, color, outline);
    }

    // ---------- meshes ----------

    static GameObject Spun(string name, Vector2[] profile, Canvas cv, float smooth) => Built(name, Lathe(profile, 28), cv, smooth);

    static GameObject Built(string name, Mesh mesh, Canvas cv, float smooth)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mat = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
        {
            mainTexture = cv.Bake(),
            color = Color.white,
        };
        mat.SetFloat("_Smoothness", smooth);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // Spins a profile around Y. u runs around the axis, v up the height, so a texture paints it like a label.
    // ribs/ribAmp modulate the radius around the axis (pumpkin lobes, paper-cup pleats). A profile that loops
    // without touching the axis gives a torus (donut).
    // startDeg/spanDeg: spin only part of the way round (a cheese with a slice cut out, or the slice itself)
    static Mesh Lathe(Vector2[] profile, int segments, int ribs = 0, float ribAmp = 0, float startDeg = 0, float spanDeg = 360)
    {
        float lo = profile.Min(p => p.y), span = Mathf.Max(profile.Max(p => p.y) - lo, 1e-4f); // v: 0 lowest, 1 highest
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int p = 0; p < profile.Length; p++)
            for (int s = 0; s <= segments; s++) // one extra column: the texture seam needs its own vertices
            {
                float a = (startDeg + spanDeg * s / segments) * Mathf.Deg2Rad;
                float r = profile[p].x * (1 + ribAmp * Mathf.Cos(ribs * a));
                verts.Add(new Vector3(Mathf.Cos(a) * r, profile[p].y, Mathf.Sin(a) * r));
                uvs.Add(new Vector2((float)s / segments, (profile[p].y - lo) / span));
            }
        int row = segments + 1;
        for (int p = 0; p < profile.Length - 1; p++)
            for (int s = 0; s < segments; s++)
            {
                int a = p * row + s, b = a + row;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        return Finish(verts, uvs, tris);
    }

    // Box with flat faces; gable > 0 adds a milk-carton roof. Faces are listed bottom-left, bottom-right,
    // top-right, top-left as seen from outside.
    static Mesh BoxMesh(float w, float h, float d, float gable)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float x = w / 2, z = d / 2;
        var roofUv = new Vector2(.25f, .97f);

        void Quad(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, float u0, float u1, float v1)
        {
            int i = verts.Count;
            verts.AddRange(new[] { bl, br, tr, tl });
            uvs.AddRange(new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, v1), new Vector2(u0, v1) });
            tris.AddRange(new[] { i, i + 3, i + 2, i, i + 2, i + 1 });
        }

        void Flat(params Vector3[] pts) // roof pieces: one solid color from the top rows of the texture
        {
            int i = verts.Count;
            verts.AddRange(pts);
            uvs.AddRange(pts.Select(_ => roofUv));
            if (pts.Length == 3) tris.AddRange(new[] { i, i + 1, i + 2 });
            else tris.AddRange(new[] { i, i + 3, i + 2, i, i + 2, i + 1 });
        }

        Quad(new(-x, 0, -z), new(x, 0, -z), new(x, h, -z), new(-x, h, -z), 0, .5f, BoxSide); // front
        Quad(new(x, 0, z), new(-x, 0, z), new(-x, h, z), new(x, h, z), 0, .5f, BoxSide); // back
        Quad(new(x, 0, -z), new(x, 0, z), new(x, h, z), new(x, h, -z), .5f, 1, BoxSide); // right
        Quad(new(-x, 0, z), new(-x, 0, -z), new(-x, h, -z), new(-x, h, z), .5f, 1, BoxSide); // left
        if (gable <= 0)
            Flat(new(-x, h, -z), new(x, h, -z), new(x, h, z), new(-x, h, z));
        else
        {
            float r = h + gable;
            Flat(new(-x, h, -z), new(x, h, -z), new(x, r, 0), new(-x, r, 0)); // front slope
            Flat(new(x, h, z), new(-x, h, z), new(-x, r, 0), new(x, r, 0)); // back slope
            Flat(new(x, h, -z), new(x, r, 0), new(x, h, z)); // gable ends
            Flat(new(-x, h, z), new(-x, r, 0), new(-x, h, -z));
            // the folded seal along the ridge
            Quad(new(-x, r, -.03f), new(x, r, -.03f), new(x, r + .08f, -.03f), new(-x, r + .08f, -.03f), .2f, .3f, .1f);
            Quad(new(x, r, .03f), new(-x, r, .03f), new(-x, r + .08f, .03f), new(x, r + .08f, .03f), .2f, .3f, .1f);
        }
        return Finish(verts, uvs, tris);
    }

    static Mesh Finish(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- texture canvas ----------

    class Canvas
    {
        readonly int W, H;
        readonly Color[] px;

        public Canvas(int w, int h)
        {
            W = w;
            H = h;
            px = new Color[w * h];
        }

        public void Paint(System.Func<float, float, Color> paint)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = paint((x + .5f) / W, (y + .5f) / H);
                    c.a = 1;
                    px[y * W + x] = c;
                }
        }

        // Writes text inside `region` (texture uv), whose physical size is `phys`. center/height/maxWidth are in
        // the same physical units, so letters keep their proportions whatever the surface. wrap: the region
        // wraps around horizontally (spun products).
        public void Text(string text, Rect region, Vector2 phys, Vector2 center, float height, float maxWidth, Color color,
            Color? outline = null, bool wrap = false)
        {
            var strokes = StrokeFont.Layout(text);
            float th = Mathf.Min(height, maxWidth / Mathf.Max(strokes.Width, .01f)); // shrink long names to fit
            const float thick = .16f, rim = .1f;
            float reach = thick / 2 + (outline != null ? rim : 0);
            float halfW = (strokes.Width / 2 + reach) * th, halfH = (.5f + reach) * th;
            float pixel = phys.x / (region.width * W) / th; // one texel, in glyph units

            int x0 = Mathf.FloorToInt(region.xMin * W), x1 = Mathf.CeilToInt(region.xMax * W);
            int y0 = Mathf.FloorToInt(region.yMin * H), y1 = Mathf.CeilToInt(region.yMax * H);
            for (int y = y0; y < y1; y++)
            {
                float py = ((y + .5f) / H - region.y) / region.height * phys.y - center.y;
                if (Mathf.Abs(py) > halfH) continue;
                for (int x = x0; x < x1; x++)
                {
                    float pxl = ((x + .5f) / W - region.x) / region.width * phys.x - center.x;
                    if (wrap) pxl = Mathf.Repeat(pxl + phys.x / 2, phys.x) - phys.x / 2;
                    if (Mathf.Abs(pxl) > halfW) continue;
                    float dist = strokes.Distance(new Vector2(pxl / th + strokes.Width / 2, py / th + .5f));
                    ref var c = ref px[y * W + x];
                    if (outline is Color o) c = Color.Lerp(c, o, Mathf.Clamp01((thick / 2 + rim - dist) / pixel + .5f));
                    c = Color.Lerp(c, color, Mathf.Clamp01((thick / 2 - dist) / pixel + .5f));
                }
            }
        }

        public Texture2D Bake()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
