using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Procedural product factory: five shapes (bottle, can, jar spun from a silhouette; carton and cereal box built
// face by face), each painted with a generated texture and a name written in a small stroke font.
// Catalog() lists the 12 products of the procedural world.
public static class Products
{
    public const int Count = 12;

    static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    // index = product id. Shapes and colors chosen so that no two products read alike at a glance.
    public static GameObject Make(int id) => id switch
    {
        0 => Bottle("SODA", liquid: C("#2a0f08"), label: C("#d92323"), cap: C("#d92323"), fill: .78f),
        1 => Carton("LAIT", body: C("#fbfbf7"), accent: C("#2f7ae5")),
        2 => Bottle("JUS", liquid: C("#ff9a1f"), label: C("#3fae4a"), cap: C("#3fae4a"), fill: .9f),
        3 => Can("COLA", body: C("#e8312f"), text: Color.white),
        4 => Jar("MIEL", content: C("#f2a81d"), lid: C("#ffd23f"), label: C("#fff4d6"), ink: C("#a8620a")),
        5 => CerealBox("CHOCO", main: C("#6b3a1e"), accent: C("#ffcf3f")),
        6 => Can("LIMO", body: C("#9ad93a"), text: C("#1f5e1f")),
        7 => Jar("FRAISE", content: C("#c2183a"), lid: C("#e8e3dc"), label: C("#ffe3ea"), ink: C("#c2183a")),
        8 => Bottle("EAU", liquid: C("#bfe9ff"), label: C("#1e88d8"), cap: C("#ffffff"), fill: .82f),
        9 => CerealBox("FLAKES", main: C("#ffd23f"), accent: C("#e8312f")),
        10 => Carton("THE", body: C("#d9f2df"), accent: C("#1f8f5b")),
        _ => Jar("CAFE", content: C("#3b2416"), lid: C("#2b2b2b"), label: C("#e9d8bf"), ink: C("#3b2416")),
    };

    // ---------- shapes ----------

    // (radius, height) silhouettes, bottom center to top center
    static readonly Vector2[] BottleProfile =
    {
        new(0, 0), new(.27f, 0), new(.32f, .03f), new(.34f, .1f), new(.34f, .72f), new(.31f, .84f),
        new(.2f, .97f), new(.13f, 1.04f), new(.13f, 1.1f), new(.155f, 1.115f), new(.155f, 1.27f), new(.13f, 1.3f), new(0, 1.3f),
    };
    static readonly Vector2[] CanProfile =
    {
        new(0, 0), new(.25f, 0), new(.29f, .02f), new(.3f, .06f), new(.3f, .84f), new(.27f, .9f), new(.25f, .92f),
        new(.255f, .94f), new(0, .94f),
    };
    static readonly Vector2[] JarProfile =
    {
        new(0, 0), new(.33f, 0), new(.36f, .03f), new(.37f, .08f), new(.37f, .5f), new(.33f, .56f), new(.3f, .58f),
        new(.34f, .6f), new(.37f, .61f), new(.37f, .74f), new(.34f, .76f), new(0, .76f),
    };

    // u that ends up facing the camera: the side at -Z (u = .75), shifted by the 35° yaw Game.Wrap gives products
    const float FrontU = .75f + 35f / 360f;

    static GameObject Bottle(string name, Color liquid, Color label, Color cap, float fill)
    {
        const float H = 1.3f, R = .34f, labelLo = .23f, labelHi = .58f, capFrom = 1.11f / H;
        var cv = new Canvas(256, 256);
        var clear = new Color(.82f, .88f, .92f);
        var cream = new Color(1f, .97f, .9f);
        cv.Paint((u, v) =>
            v >= capFrom ? Ridged(cap, u)
            : v >= labelLo && v <= labelHi ? (Mathf.Abs((v - labelLo) / (labelHi - labelLo) - .5f) > .43f ? cream : label)
            : (v < fill ? liquid : clear) * Sheen(u));
        LatheText(cv, name, 2 * Mathf.PI * R, H, (labelLo + labelHi) / 2 * H, (labelHi - labelLo) * H * .55f, Color.white);
        return Spun("Bottle", BottleProfile, cv, .6f);
    }

    static GameObject Can(string name, Color body, Color text)
    {
        const float H = .94f, R = .3f;
        var cv = new Canvas(256, 256);
        var metal = new Color(.82f, .84f, .86f);
        cv.Paint((u, v) =>
            v < .06f / H || v > .84f / H ? metal * Sheen(u)
            : Mathf.Abs(v - .62f) < .02f ? Color.white // thin stripe under the name
            : body * Sheen(u));
        LatheText(cv, name, 2 * Mathf.PI * R, H, .43f, .26f, text);
        return Spun("Can", CanProfile, cv, .7f);
    }

    static GameObject Jar(string name, Color content, Color lid, Color label, Color ink)
    {
        const float H = .76f, R = .37f, labelLo = .14f / H, labelHi = .46f / H;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
            v >= .58f / H ? Ridged(lid, u)
            : v >= labelLo && v <= labelHi ? (Mathf.Abs((v - labelLo) / (labelHi - labelLo) - .5f) > .4f ? ink : label)
            : content * Sheen(u));
        LatheText(cv, name, 2 * Mathf.PI * R, H, (labelLo + labelHi) / 2 * H, (labelHi - labelLo) * H * .5f, ink);
        return Spun("Jar", JarProfile, cv, .45f);
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
        return Built("Carton", BoxMesh(w, h, d, gable), cv, .3f);
    }

    static GameObject CerealBox(string name, Color main, Color accent)
    {
        const float w = .72f, d = .26f, h = 1.05f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            if (v >= BoxSide) return main; // top
            // a bowl of cereal: a white disc low on each big face
            float fu = Mathf.Repeat(u, .5f) / .5f, fv = v / BoxSide;
            float dx = (fu - .5f) * w / h, dy = fv - .27f;
            if (u < .5f && dx * dx + dy * dy < .2f * .2f) return dy > 0 ? accent : Color.white;
            return main;
        });
        BoxText(cv, name, w, d, h, .7f, .2f, Color.white, Darker(main));
        return Built("CerealBox", BoxMesh(w, h, d, 0), cv, .3f);
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
