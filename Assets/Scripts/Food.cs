using System.Collections.Generic;
using UnityEngine;

// Procedural food: the four countries' catalogs (France, Italy, Japan, USA) and their products. Products are assembled from parts:
// spun silhouettes (Lathe, optionally ribbed or looping into a torus), Unity's primitive meshes, plain colors
// and painted textures (sprinkles, chips, seeds, dots, stripes). All sizes are relative: Game.Wrap fits each
// product to its slot.
public static partial class Products
{
    // ---------- countries: 12 products each ----------

    static GameObject France(int id) => id switch
    {
        0 => Baguette(),
        1 => Croissant(),
        2 => Macaron(),
        3 => Eclair(),
        4 => Wine(),
        5 => PainAuChocolat(),
        6 => CremeBrulee(),
        7 => Caneles(),
        8 => Camembert(),
        9 => Saucisson(),
        10 => Moutarde(),
        _ => Confiture(),
    };

    static GameObject Italy(int id) => id switch
    {
        0 => Pizza(),
        1 => Tomate(),
        2 => Cornet(),
        3 => Tiramisu(),
        4 => Pasta(),
        5 => Bottle("OLIO", liquid: C("#5a6e1c"), label: C("#1f4a2a"), cap: C("#d9b44a"), fill: .95f),
        6 => Cannolo(),
        7 => Parmesan(),
        8 => Jar("PESTO", content: C("#4f8a2a"), lid: C("#d9b44a"), label: C("#fbf3e4"), ink: C("#2f6b2a")),
        9 => Salami(),
        10 => Panettone(),
        _ => Espresso(),
    };

    static GameObject Japan(int id) => id switch
    {
        0 => Maki(),
        1 => Nigiri(),
        2 => Onigiri(),
        3 => Ramen(),
        4 => Dango(),
        5 => Bento(),
        6 => Matcha(),
        7 => Takoyaki(),
        8 => Tempura(),
        9 => Gyoza(),
        10 => Ramune(),
        _ => Edamame(),
    };

    static GameObject Usa(int id) => id switch
    {
        0 => Burger(),
        1 => Frites(),
        2 => HotDog(),
        3 => Popcorn(),
        4 => SodaCup(),
        5 => Milkshake(),
        6 => Donut("DonutRose", C("#f58fb5"), sprinkles: true),
        7 => Pilon(),
        8 => Ketchup(),
        9 => Cookie(),
        10 => Muffin(),
        _ => Cupcake(),
    };

    // ---------- bakery ----------

    static GameObject Donut(string name, Color icing, bool sprinkles)
    {
        var root = new GameObject(name);
        var dough = C("#d9954a");
        Spin(root, Torus(.3f, .15f), Paint((u, v) =>
        {
            if (v < .52f + .06f * Mathf.Sin(u * Mathf.PI * 14)) return dough * (v < .2f ? .85f : 1f); // wavy icing edge
            if (sprinkles && Dot(u, v, 26, 10, .45f, .2f, 3, out float k)) return Sprinkle(k);
            if (!sprinkles && Mathf.Abs(Mathf.Repeat(u * 14 + v * 2, 1) - .5f) < .07f) return Color.white; // drizzle
            return icing;
        }, .45f));
        return root;
    }

    static GameObject Cupcake()
    {
        var root = new GameObject("Cupcake");
        Spin(root, P(0, 0, .24f, 0, .31f, .36f, 0, .36f), Plain(C("#f28db2")), ribs: 16, ribAmp: .06f); // pleated paper
        Spin(root, P(0, .32f, .35f, .32f, .37f, .4f, .29f, .47f, .31f, .53f, .21f, .62f, .22f, .68f, .1f, .78f, 0, .8f),
            Paint((u, v) => Dot(u, v, 22, 10, .25f, .2f, 5, out float k) ? Sprinkle(k) : C("#fff3e6"), .35f));
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#d7192a"), .7f), new(0, .84f, 0), scale: Vector3.one * .14f);
        return root;
    }

    // Delivery truck for the map, front towards +X: cargo box with the shop's name, red cab, four wheels.
    // stripe: the current world's color. Wheels are named "wheel" so the map can spin them.
    public static GameObject Truck(Color stripe)
    {
        var root = new GameObject("Truck");
        const float len = 1.25f, h = .9f, d = .8f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
            v >= BoxSide ? stripe // roof in the world color: the map sees it from above
            : Mathf.Abs(v / BoxSide - .2f) < .07f ? stripe // band along the bottom
            : C("#fbf8f0"));
        BoxText(cv, "SHELF SORT", len, d, h, .62f, .26f, C("#e8423c"));
        Part(root, BoxMesh(len, h, d, 0), Textured(cv, .4f), new(-.25f, .28f, 0));

        var red = Plain(C("#e8423c"), .55f);
        Part(root, Prim(PrimitiveType.Cube), red, new(.62f, .56f, 0), scale: new(.5f, .62f, d * .94f)); // cab
        Part(root, Prim(PrimitiveType.Cube), red, new(.82f, .4f, 0), scale: new(.32f, .3f, d * .94f)); // hood
        var glass = Plain(C("#9fd8ff"), .9f);
        Part(root, Prim(PrimitiveType.Cube), glass, new(.875f, .72f, 0), new(0, 0, -12), new(.03f, .26f, d * .78f)); // windshield
        Part(root, Prim(PrimitiveType.Cube), glass, new(.62f, .72f, -d * .48f), scale: new(.3f, .24f, .02f)); // side windows
        Part(root, Prim(PrimitiveType.Cube), glass, new(.62f, .72f, d * .48f), scale: new(.3f, .24f, .02f));
        Part(root, Prim(PrimitiveType.Cube), Plain(C("#3a3f4b"), .3f), new(.1f, .22f, 0), scale: new(1.9f, .12f, d * .9f)); // chassis
        Part(root, Prim(PrimitiveType.Cube), Plain(C("#cfd4dc"), .8f), new(1, .26f, 0), scale: new(.06f, .1f, d)); // bumper
        var lamp = Plain(C("#ffd23f"), .9f);
        Part(root, Prim(PrimitiveType.Sphere), lamp, new(.98f, .42f, -.28f), scale: Vector3.one * .1f);
        Part(root, Prim(PrimitiveType.Sphere), lamp, new(.98f, .42f, .28f), scale: Vector3.one * .1f);

        var tire = Plain(C("#2a2d35"), .2f);
        var hub = Plain(C("#dfe3ea"), .7f);
        foreach (float x in new[] { -.55f, .7f })
            foreach (float z in new[] { -d / 2, d / 2 })
            {
                var wheel = Part(root, Prim(PrimitiveType.Cylinder), tire, new(x, .2f, z), new(90, 0, 0), new(.4f, .06f, .4f));
                wheel.name = "wheel";
                var cap = Part(wheel.gameObject, Prim(PrimitiveType.Cube), hub, new(0, 0, 0), default, new(.45f, 1.1f, .2f)); // spoke bar: shows the spin
                cap.name = "hub";
            }
        return root;
    }

    // one smooth tube bent into a crescent: fat in the middle, pointed tips, pinched grooves between the rolls
    static GameObject Croissant()
    {
        // fat middle, thin horns curling round towards the player, deep grooves between seven rolls
        var root = new GameObject("Croissant");
        float Radius(float t) => .26f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t)), .6f) * (1 - .22f * Mathf.Pow(Mathf.Abs(Mathf.Cos(7 * Mathf.PI * t)), 6));
        Vector3 Center(float t)
        {
            float a = Mathf.Lerp(-105, 105, t) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * .4f, Radius(t) * .85f, Mathf.Cos(a) * .4f - .2f);
        }
        var mat = Paint((u, v) =>
        {
            float groove = Mathf.Pow(Mathf.Abs(Mathf.Cos(7 * Mathf.PI * v)), 6);
            float top = .8f + .3f * Mathf.Sin(2 * Mathf.PI * u); // u = .25 is the top: lighter, more baked
            return Color.Lerp(Color.Lerp(C("#f0b35a"), C("#d98a32"), Mathf.PerlinNoise(u * 12, v * 30)), C("#8f4a18"), groove * .85f) * top;
        }, .55f);
        Part(Tilted(root, new(-25, 0, 0)), Sweep(Center, Radius, .85f), mat, Vector3.zero);
        return root;
    }

    // Tube along path(t), t 0..1, round section of radius(t) squashed vertically by flat. u runs around, v along.
    // square < 1 squares the section off (a rounded rectangle instead of an ellipse).
    static Mesh Sweep(System.Func<float, Vector3> path, System.Func<float, float> radius, float flat, int rings = 64, int sides = 20, float square = 1)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float t = (float)i / rings;
            var tangent = (path(Mathf.Min(t + .01f, 1)) - path(Mathf.Max(t - .01f, 0))).normalized;
            var side = Vector3.Cross(tangent, Vector3.up).normalized;
            for (int j = 0; j <= sides; j++)
            {
                float th = 2 * Mathf.PI * j / sides, r = radius(t);
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                c = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), square);
                s = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), square);
                verts.Add(path(t) + side * c * r + Vector3.up * s * r * flat);
                uvs.Add(new Vector2((float)j / sides, t));
            }
        }
        int row = sides + 1;
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * row + j, b = a + row; // (b - a) along the path, (a + 1 - a) around: their cross points outwards
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        return Finish(verts, uvs, tris);
    }

    // baguette: long, slightly uneven, golden crust browner on the sides, flour dusting, five opened score cuts
    static GameObject Baguette()
    {
        var root = new GameObject("Baguette");
        const float L = 1.83f, R = .13f;
        // lying along X: the spun axis is turned 90°, so u = 0 (local +X) ends up on top
        Spin(root, P(0, 0, .06f, .015f, .11f, .06f, .13f, .15f, .135f, .4f, .128f, .9f, .135f, 1.4f, .13f, 1.65f, .11f, 1.75f, .06f, 1.81f, 0, L),
            Paint((u, v) =>
            {
                float up = Mathf.Cos(u * Mathf.PI * 2);
                float around = (Mathf.Repeat(u + .5f, 1) - .5f) * 2 * Mathf.PI * R; // arc length from the top
                for (int k = 0; k < 5; k++) // score cuts: diagonal slits, pale crumb inside a dark rim
                {
                    float along = (v - (.16f + k * .17f)) * L;
                    float s = along * .87f + around * .5f, t = -along * .5f + around * .87f;
                    float e = s * s / (.13f * .13f) + t * t / (.035f * .035f);
                    if (e < 1) return C("#efd29a");
                    if (e < 1.6f) return C("#8a4a18");
                }
                if (Dot(u, v, 20, 60, .12f, .25f, 11, out _)) return C("#f3e6cf"); // flour
                var crust = up > .2f ? C("#d4913c") : up > -.5f ? C("#b8702e") : C("#e3b877");
                return crust * (.88f + .2f * Mathf.PerlinNoise(u * 18, v * 40));
            }, .4f, 256), new(L / 2, R, 0), new(0, 0, 90));
        return root;
    }

    static GameObject Miche()
    {
        var root = new GameObject("Miche");
        Spin(root, P(0, 0, .42f, 0, .46f, .08f, .43f, .22f, .32f, .34f, .16f, .4f, 0, .42f), Paint((u, v) =>
        {
            if (v > .7f && Mathf.Abs(Mathf.Repeat(u * 4, 1) - .5f) < .05f) return C("#8a5520"); // scored cross
            if (v > .55f && Dot(u, v, 40, 20, .35f, .25f, 9, out _)) return C("#f3e6cf"); // flour
            return C("#c98a3d");
        }, .25f));
        return root;
    }

    // two macarons: a pink one, a pistachio one leaning on it
    static GameObject Macaron()
    {
        var root = new GameObject("Macaron");
        MacaronAt(root, C("#f6a5c0"), C("#fff4e0"), new(-.22f, 0, -.12f), default);
        MacaronAt(root, C("#a8d98a"), C("#fff4e0"), new(.24f, .05f, .02f), new(0, 30, -16)); // leaning on the pink one
        return root;
    }

    // one macaron (diameter ~.5): glossy domed shells, each sitting on its ruffled "foot", a ring of filling between
    static void MacaronAt(GameObject root, Color shell, Color filling, Vector3 pos, Vector3 euler)
    {
        var m = Tilted(root, euler, pos);
        var glossy = Plain(shell, .55f);
        var foot = Plain(shell * 1.08f, .2f);
        Spin(m, P(0, 0, .22f, 0, .25f, .03f, 0, .03f), glossy); // bottom shell's flat underside
        Spin(m, P(.25f, .03f, .265f, .05f, .25f, .08f), foot, ribs: 40, ribAmp: .06f);
        Spin(m, P(0, .08f, .235f, .08f, .235f, .14f, 0, .14f), Plain(filling, .3f));
        Spin(m, P(.25f, .14f, .265f, .17f, .25f, .19f), foot, ribs: 40, ribAmp: .06f);
        Spin(m, P(0, .19f, .25f, .19f, .245f, .22f, .2f, .27f, .1f, .3f, 0, .305f), glossy); // top dome
    }

    static GameObject Cookie()
    {
        var root = new GameObject("Cookie");
        Spin(root, P(0, 0, .4f, 0, .43f, .04f, .41f, .09f, 0, .1f), Plain(C("#d69a52"), .2f));
        // chips sit on the flat top (a spun disk's top has no room for a painted pattern)
        var chip = Plain(C("#4a2a14"), .4f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * 2.4f, r = .08f + .27f * Hash(i, 1, 11);
            Part(root, Prim(PrimitiveType.Sphere), chip, new(Mathf.Cos(a) * r, .1f, Mathf.Sin(a) * r), scale: new(.09f, .05f, .09f));
        }
        return root;
    }

    static GameObject Tarte()
    {
        var root = new GameObject("Tarte");
        var crust = Plain(C("#d9a35a"), .3f);
        Spin(root, P(0, 0, .38f, 0, .45f, .13f, .43f, .15f, .37f, .1f, 0, .1f), crust);
        Spin(root, P(0, .06f, .37f, .06f, .37f, .11f, 0, .11f), Plain(C("#c4213c"), .7f));
        foreach (float o in new[] { -.2f, 0, .2f }) // lattice
        {
            float len = 2 * Mathf.Sqrt(.37f * .37f - o * o);
            Part(root, Prim(PrimitiveType.Cube), crust, new(0, .12f, o), scale: new(len, .025f, .06f));
            Part(root, Prim(PrimitiveType.Cube), crust, new(o, .125f, 0), scale: new(.06f, .025f, len));
        }
        return root;
    }

    static GameObject Muffin()
    {
        var root = new GameObject("Muffin");
        Spin(root, P(0, 0, .25f, 0, .3f, .34f, 0, .34f), Plain(C("#6fb6e8")), ribs: 16, ribAmp: .06f);
        Spin(root, P(0, .3f, .36f, .3f, .43f, .36f, .42f, .48f, .32f, .6f, .16f, .66f, 0, .68f),
            Paint((u, v) => Dot(u, v, 18, 8, .45f, .2f, 4, out _) ? C("#3e2210") : C("#b0683a"), .3f));
        return root;
    }

    // éclair: crackled golden choux pastry, glossy chocolate glaze dripping in waves down the sides, a piped white line
    static GameObject Eclair()
    {
        var root = new GameObject("Eclair");
        Spin(root, P(0, 0, .07f, .01f, .12f, .06f, .13f, .15f, .135f, .45f, .13f, .75f, .12f, .84f, .07f, .89f, 0, .9f), Paint((u, v) =>
        {
            float up = Mathf.Cos(u * Mathf.PI * 2), around = Mathf.Repeat(u + .5f, 1) - .5f;
            float edge = .12f - .06f * Mathf.Abs(Mathf.Sin(v * Mathf.PI * 7)); // glaze on the top half, a softly wavy edge
            if (up > edge)
            {
                if (v > .12f && v < .88f && Mathf.Abs(around - .035f * Mathf.Sin(v * 60)) < .01f) return C("#fbf3e4"); // piped line
                return C("#2a1a12"); // dark chocolate glaze
            }
            float n = Mathf.PerlinNoise(u * 30, v * 60);
            return Mathf.Abs(n - .5f) < .02f ? C("#d1a05a") : Color.Lerp(C("#e8bc72"), C("#f5d595"), n); // crackled choux, pale gold
        }, .55f, 256), new(.45f, .12f, 0), new(0, 0, 90), scale: new(1, 1, 1.12f));
        return root;
    }

    static GameObject Gateau()
    {
        // chocolate cake, white icing dripping down the sides, strawberries and cream on top, a cherry in the middle
        var root = new GameObject("Gateau");
        Spin(root, P(0, 0, .4f, 0, .42f, .02f, .42f, .44f, .4f, .49f, .36f, .5f, 0, .5f), Paint((u, v) =>
        {
            float drip = .62f + .14f * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 9)), 6) + .08f * Hash((int)(u * 18), 0, 4);
            return v > drip ? C("#fffaf2") : Mathf.Abs(v - .3f) < .03f ? C("#f6a5c0") : C("#5b2d17");
        }, .35f, 256));
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI * 2 / 6;
            Part(root, Prim(PrimitiveType.Sphere), Plain(C("#e3263a"), .6f), new(Mathf.Cos(a) * .28f, .55f, Mathf.Sin(a) * .28f), scale: new(.13f, .16f, .13f));
            Part(root, Prim(PrimitiveType.Sphere), Plain(C("#fffaf2"), .3f), new(Mathf.Cos(a + .5f) * .3f, .52f, Mathf.Sin(a + .5f) * .3f), scale: Vector3.one * .09f);
        }
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#b0102a"), .7f), new(0, .58f, 0), scale: Vector3.one * .17f);
        return root;
    }

    // ---------- greengrocer ----------

    static GameObject Pomme()
    {
        var root = new GameObject("Pomme");
        Spin(root, P(0, .02f, .17f, 0, .3f, .06f, .38f, .2f, .39f, .34f, .34f, .48f, .22f, .55f, .08f, .51f, 0, .47f),
            Paint((u, v) => Color.Lerp(C("#c4161c"), C("#f2b632"), .35f * Mathf.PerlinNoise(u * 6, v * 3) * (1 - v)), .55f));
        Stem(root, .55f);
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#4caf3a"), .3f), new(.09f, .6f, 0), new(0, 20, 25), new(.16f, .03f, .08f));
        return root;
    }

    static GameObject Orange()
    {
        var root = new GameObject("Orange");
        Spin(root, Ball(.36f, .36f), Paint((u, v) => C("#f28b1c") * (.92f + .12f * Mathf.PerlinNoise(u * 40, v * 20)), .35f));
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#4a8f2a"), .3f), new(0, .72f, 0), scale: new(.08f, .04f, .08f));
        return root;
    }

    static GameObject Citron()
    {
        var root = new GameObject("Citron");
        Spin(root, P(0, 0, .07f, .03f, .2f, .13f, .27f, .3f, .2f, .47f, .07f, .57f, 0, .6f),
            Paint((u, v) => C("#f5d327") * (.92f + .12f * Mathf.PerlinNoise(u * 40, v * 20)), .4f), new(.3f, .27f, 0), new(0, 0, 90));
        return root;
    }

    static GameObject Poire()
    {
        var root = new GameObject("Poire");
        Spin(root, P(0, 0, .2f, .01f, .3f, .1f, .32f, .22f, .25f, .36f, .16f, .46f, .13f, .56f, .08f, .64f, 0, .66f),
            Paint((u, v) => Dot(u, v, 30, 20, .3f, .15f, 6, out _) ? C("#8a9a2a")
                : Color.Lerp(C("#b5cf3a"), C("#e3d84a"), v), .35f));
        Stem(root, .66f);
        return root;
    }

    static GameObject Tomate()
    {
        var root = new GameObject("Tomate");
        Spin(root, Ball(.36f, .27f, .75f), Plain(C("#e2321e"), .65f), ribs: 6, ribAmp: .05f);
        Calyx(root, .54f, 5, .2f);
        return root;
    }

    static GameObject Citrouille()
    {
        var root = new GameObject("Citrouille");
        Spin(root, Ball(.42f, .3f, .72f), Plain(C("#f07f19"), .3f), ribs: 8, ribAmp: .1f);
        Part(root, Prim(PrimitiveType.Cylinder), Plain(C("#6b4a1f"), .2f), new(0, .64f, 0), new(0, 0, 8), new(.07f, .08f, .07f));
        return root;
    }

    static GameObject Aubergine()
    {
        var root = new GameObject("Aubergine");
        Spin(root, P(0, 0, .16f, .03f, .25f, .18f, .26f, .4f, .2f, .6f, .13f, .76f, 0, .8f), Plain(C("#5b2a7a"), .7f));
        Spin(root, P(0, .72f, .17f, .72f, .1f, .84f, 0, .86f), Plain(C("#4a8f2a"), .3f), ribs: 5, ribAmp: .3f);
        Stem(root, .86f, C("#4a8f2a"));
        return root;
    }

    static GameObject Carotte()
    {
        var root = new GameObject("Carotte");
        Spin(root, P(0, 0, .04f, .05f, .1f, .3f, .15f, .62f, .17f, .78f, .15f, .84f, 0, .85f),
            Paint((u, v) => C("#f07a1a") * (Mathf.Repeat(v * 14, 1) < .08f ? .82f : 1f), .3f));
        foreach (float tilt in new[] { -25f, 0, 25f })
            Part(root, Prim(PrimitiveType.Cylinder), Plain(C("#4a9e2f"), .3f), new(-tilt * .003f, 1f, 0), new(0, 0, tilt), new(.035f, .17f, .035f));
        return root;
    }

    static GameObject Raisin()
    {
        var root = new GameObject("Raisin");
        var grape = Plain(C("#6d2c8a"), .55f);
        int[] perRow = { 1, 2, 3, 4 }; // a bunch hangs narrow at the bottom
        for (int row = 0; row < perRow.Length; row++)
            for (int i = 0; i < perRow[row]; i++)
            {
                float x = (i - (perRow[row] - 1) / 2f) * .19f, z = (row + i) % 2 == 0 ? .05f : -.05f;
                Part(root, Prim(PrimitiveType.Sphere), grape, new(x, .11f + row * .16f, z), scale: Vector3.one * .21f);
            }
        Stem(root, .7f);
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#4caf3a"), .3f), new(.12f, .78f, 0), new(0, 0, 20), new(.2f, .03f, .12f));
        return root;
    }

    static GameObject Fraise()
    {
        var root = new GameObject("Fraise");
        Spin(root, P(0, 0, .07f, .06f, .19f, .22f, .27f, .4f, .26f, .5f, .14f, .57f, 0, .58f),
            Paint((u, v) => Dot(u, v, 14, 10, .8f, .12f, 8, out _) ? C("#f4d35e") : C("#e3242b"), .5f)); // seeds
        Calyx(root, .57f, 6, .17f);
        return root;
    }

    static GameObject Champignon()
    {
        var root = new GameObject("Champignon");
        Spin(root, P(0, 0, .13f, 0, .11f, .32f, 0, .32f), Plain(C("#f1e3c8"), .2f));
        Spin(root, P(0, .24f, .4f, .24f, .42f, .3f, .35f, .45f, .2f, .55f, 0, .58f), Paint((u, v) =>
            v < .15f ? C("#f1e3c8") // underside
            : Dot(u, v, 10, 6, .7f, .22f, 2, out _) ? Color.white : C("#d8261f"), .4f));
        return root;
    }

    static GameObject Pasteque()
    {
        var root = new GameObject("Pasteque");
        Spin(root, Ball(.4f, .4f), Paint((u, v) =>
            Mathf.Repeat(u * 12 + .15f * Mathf.Sin(v * 12), 1) < .35f ? C("#1f6b2a") : C("#4fa63a"), .4f), scale: new(1.25f, 1, 1));
        return root;
    }

    // ---------- fast food ----------

    static GameObject Burger()
    {
        var root = new GameObject("Burger");
        var bun = Paint((u, v) => v > .35f && Dot(u, v, 16, 8, .6f, .16f, 7, out _) ? C("#fff2d0") : C("#e0a14a"), .35f);
        Spin(root, P(0, 0, .36f, 0, .4f, .05f, .4f, .12f, 0, .12f), Plain(C("#e0a14a"), .35f));
        Spin(root, P(0, .12f, .42f, .12f, .43f, .16f, .42f, .19f, 0, .2f), Plain(C("#5a2e1a"), .3f)); // patty
        Part(root, Prim(PrimitiveType.Cube), Plain(C("#ffcc29"), .4f), new(0, .21f, 0), new(0, 45, 0), new(.66f, .03f, .66f)); // cheese
        Spin(root, P(0, .22f, .46f, .22f, .46f, .25f, 0, .25f), Plain(C("#6cc04a"), .3f), ribs: 14, ribAmp: .07f); // lettuce
        Spin(root, P(0, .25f, .37f, .25f, .37f, .29f, 0, .29f), Plain(C("#e2321e"), .5f)); // tomato
        Spin(root, P(0, .29f, .4f, .29f, .42f, .36f, .35f, .49f, .2f, .57f, 0, .59f), bun);
        return root;
    }

    static GameObject Frites()
    {
        // red carton (four panels flaring outwards) with a fan of fries sticking out
        var root = new GameObject("Frites");
        var box = Plain(C("#d9261c"), .4f);
        const float h = .5f;
        foreach (var (pos, euler, size) in new (Vector3, Vector3, Vector3)[]
                 {
                     (new(0, h / 2, -.16f), new(-8, 0, 0), new(.5f, h, .04f)), // front
                     (new(0, h / 2, .16f), new(8, 0, 0), new(.5f, h, .04f)), // back
                     (new(-.25f, h / 2, 0), new(0, 0, -6), new(.04f, h, .34f)),
                     (new(.25f, h / 2, 0), new(0, 0, 6), new(.04f, h, .34f)),
                 })
            Part(root, Prim(PrimitiveType.Cube), box, pos, euler, size);
        Part(root, Prim(PrimitiveType.Cube), Plain(C("#ffd23f"), .4f), new(0, h * .55f, -.2f), new(-8, 0, 0), new(.36f, .07f, .02f)); // logo band
        for (int i = 0; i < 16; i++)
        {
            float x = -.19f + .38f * (i / 2) / 7f, z = i % 2 == 0 ? -.06f : .06f, len = .55f + .2f * Hash(i, 2, 13);
            var fry = Plain(C(Hash(i, 5, 13) < .5f ? "#ffd23f" : "#f2b632"), .3f);
            Part(root, Prim(PrimitiveType.Cube), fry, new(x * 1.2f, .15f + len / 2, z), new((Hash(i, 4, 13) - .5f) * 14, 0, -x * 70), new(.07f, len, .07f));
        }
        return root;
    }

    static GameObject HotDog()
    {
        var root = new GameObject("HotDog");
        Spin(root, Capsule(1, .15f), Plain(C("#e3a656"), .3f), new(.5f, .15f, 0), new(0, 0, 90), scale: new(1, 1, 1.15f));
        Spin(root, Capsule(1.12f, .09f), Paint((u, v) =>
        {
            float zig = (Mathf.Abs(Mathf.Repeat(v * 9, 1) - .5f) * 2 - .5f) * .1f; // mustard zigzag along the top
            float around = Mathf.Repeat(u + .5f, 1) - .5f;
            return Mathf.Abs(around - zig) < .025f ? C("#ffd21f") : C("#b5432a");
        }, .55f), new(.56f, .27f, 0), new(0, 0, 90));
        return root;
    }

    // pizza on a wooden peel, stood up and leaning back like on a display stand so the toppings face the player:
    // raised golden crust, a line of sauce, cheese with browned spots, shiny pepperoni, olives and basil
    static GameObject Pizza()
    {
        var whole = new GameObject("Pizza");
        var root = Tilted(whole, new(-58, 0, 0));
        var wood = Plain(C("#c58a45"), .25f);
        Spin(root, P(0, -.04f, .52f, -.04f, .52f, 0, 0, 0), wood); // the peel
        Part(root, Cube, wood, new(0, -.02f, .66f), scale: new(.14f, .04f, .36f));
        Spin(root, P(0, 0, .44f, 0, .47f, .03f, .47f, .07f, .43f, .1f, .39f, .075f, 0, .07f),
            Paint((u, v) => Color.Lerp(C("#ecb862"), C("#b8722e"), Mathf.PerlinNoise(u * 24, v * 8) * .7f), .35f));
        Part(root, Cylinder, Plain(C("#d9452b"), .5f), new(0, .075f, 0), scale: new(.84f, .008f, .84f));
        Part(root, Cylinder, Paint((u, v) =>
        {
            float n = Mathf.PerlinNoise(u * 9, v * 9);
            return n > .66f ? C("#e3923a") : n < .25f ? C("#fff0b0") : C("#ffd257"); // browned and melted spots
        }, .45f), new(0, .08f, 0), scale: new(.76f, .008f, .76f));
        var pep = Plain(C("#b8282e"), .75f);
        var olive = Plain(C("#2a2a2e"), .6f);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 2 * Mathf.PI / 6, r = i == 6 ? 0 : .23f;
            Spin(root, P(0, 0, .075f, 0, .07f, .014f, 0, .02f), pep, new(Mathf.Cos(a) * r, .09f, Mathf.Sin(a) * r));
            if (i < 6) Spin(root, Torus(.028f, .014f), olive, new(Mathf.Cos(a + .5f) * .3f, .09f, Mathf.Sin(a + .5f) * .3f));
        }
        var basil = Plain(C("#3e9b3a"), .4f);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f + 1;
            Part(root, Sphere, basil, new(Mathf.Cos(a) * .13f, .095f, Mathf.Sin(a) * .13f), new(0, i * 60, 0), new(.12f, .015f, .07f));
        }
        return whole;
    }

    static GameObject SodaCup()
    {
        var root = new GameObject("Soda");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => Mathf.Abs(v - .25f - .04f * Mathf.Sin(u * Mathf.PI * 6)) < .05f ? Color.white : C("#e8312f"));
        LatheText(cv, "SODA", 2 * Mathf.PI * .27f, .72f, .42f, .15f, Color.white);
        Spin(root, P(0, 0, .22f, 0, .29f, .7f, .3f, .72f, 0, .72f), Textured(cv, .35f));
        Spin(root, P(0, .71f, .31f, .71f, .31f, .75f, .24f, .8f, 0, .81f), Plain(C("#fbfbf7"), .4f)); // lid
        Part(root, Prim(PrimitiveType.Cylinder), Plain(C("#e8312f"), .5f), new(.06f, .95f, 0), new(0, 0, -12), new(.03f, .2f, .03f));
        return root;
    }

    static GameObject Cornet()
    {
        var root = new GameObject("Cornet");
        Spin(root, P(0, 0, .05f, .03f, .24f, .58f, .26f, .62f, 0, .62f), Paint((u, v) =>
            Mathf.Repeat(u * 10 + v * 6, 1) < .12f || Mathf.Repeat(u * 10 - v * 6, 1) < .12f ? C("#b57834") : C("#e2a85c"), .25f)); // waffle
        Spin(root, Ball(.28f, .76f), Plain(C("#f7a8c4"), .35f));
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#d7192a"), .7f), new(0, 1.06f, 0), scale: Vector3.one * .1f);
        return root;
    }

    static GameObject Milkshake()
    {
        var root = new GameObject("Milkshake");
        Spin(root, P(0, 0, .17f, 0, .2f, .05f, .26f, .62f, .27f, .64f, 0, .64f),
            Paint((u, v) => Mathf.Abs(Mathf.Repeat(u - FrontU + .08f, 1) - .5f) < .025f ? Color.white : C("#f5b3c8"), .7f)); // glass glint
        Spin(root, P(0, .6f, .28f, .6f, .26f, .68f, .18f, .74f, .19f, .78f, .08f, .86f, 0, .88f), Plain(C("#fffaf2"), .3f));
        Part(root, Prim(PrimitiveType.Sphere), Plain(C("#d7192a"), .7f), new(0, .93f, 0), scale: Vector3.one * .1f);
        Part(root, Prim(PrimitiveType.Cylinder), Plain(C("#2f7ae5"), .5f), new(.1f, .9f, 0), new(0, 0, -15), new(.03f, .2f, .03f));
        return root;
    }

    static GameObject Popcorn()
    {
        var root = new GameObject("Popcorn");
        Spin(root, P(0, 0, .22f, 0, .3f, .62f, 0, .62f), Paint((u, v) => (int)(u * 12) % 2 == 0 ? C("#e8312f") : Color.white, .3f));
        var corn = Plain(C("#fff1c4"), .2f);
        for (int i = 0; i < 16; i++)
        {
            float a = i * 2.4f, r = .25f * Mathf.Sqrt(Hash(i, 4, 17)), y = .64f + .16f * (1 - r / .25f) + .04f * Hash(i, 5, 17);
            Part(root, Prim(PrimitiveType.Sphere), corn, new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), scale: Vector3.one * (.12f + .04f * Hash(i, 6, 17)));
        }
        return root;
    }

    static GameObject Ketchup()
    {
        var root = new GameObject("Ketchup");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v > .25f && v < .62f ? C("#fff4e0") : C("#d81f26"));
        LatheText(cv, "KETCHUP", 2 * Mathf.PI * .29f, .85f, .37f, .13f, C("#d81f26"));
        Spin(root, P(0, 0, .24f, 0, .28f, .05f, .29f, .55f, .25f, .68f, .14f, .78f, .13f, .84f, 0, .85f), Textured(cv, .55f));
        Spin(root, P(0, .84f, .14f, .84f, .14f, .92f, .06f, 1f, 0, 1.02f), Plain(C("#fbfbf7"), .4f));
        return root;
    }

    // fried chicken drumstick: crispy teardrop of meat, white bone with its knob, leaning diagonally
    static GameObject Pilon()
    {
        var root = new GameObject("Pilon");
        var body = Tilted(root, new(0, 0, -50), new(-.1f, .12f, 0));
        Spin(body, P(0, 0, .2f, .03f, .3f, .15f, .32f, .3f, .27f, .45f, .17f, .58f, .1f, .66f, 0, .68f),
            Paint((u, v) => Color.Lerp(C("#b45a1c"), C("#e5993e"), Mathf.PerlinNoise(u * 24, v * 12)), .35f));
        var bone = Plain(C("#fbf6ea"), .4f);
        Part(body, Prim(PrimitiveType.Cylinder), bone, new(0, .78f, 0), scale: new(.1f, .14f, .1f));
        Part(body, Prim(PrimitiveType.Sphere), bone, new(-.05f, .93f, 0), scale: Vector3.one * .14f);
        Part(body, Prim(PrimitiveType.Sphere), bone, new(.05f, .93f, 0), scale: Vector3.one * .14f);
        return root;
    }

    // a child pivot turned by euler: parts built on it come out leaning
    static GameObject Tilted(GameObject root, Vector3 euler, Vector3 pos = default)
    {
        var t = new GameObject("tilt");
        t.transform.SetParent(root.transform, false);
        t.transform.localEulerAngles = euler;
        t.transform.localPosition = pos;
        return t;
    }

    static GameObject Burrito()
    {
        var root = new GameObject("Burrito");
        // foil wrap, tortilla, and the filling showing at the open end (rice, beans, salsa, lettuce)
        Spin(root, Capsule(.9f, .2f), Paint((u, v) =>
            v < .42f ? C("#d6dade") * (.9f + .15f * Mathf.PerlinNoise(u * 30, v * 30)) * ((int)(v * 30) % 3 == 0 ? .88f : 1f)
            : v > .9f ? (Dot(u, v, 16, 40, .7f, .3f, 21, out float k) ? new[] { C("#6b3a1e"), C("#e2321e"), C("#5cbf3a") }[(int)(k * 9) % 3] : C("#fbf6ea"))
            : Dot(u, v, 14, 10, .4f, .2f, 12, out _) ? C("#c48a4a") : C("#f1d9a4"), .4f, 256), new(.45f, .2f, 0), new(0, 0, 90));
        return root;
    }

    static GameObject Cafe()
    {
        var root = new GameObject("Cafe");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v > .3f && v < .62f ? C("#8a5a3a") : C("#fbfbf7")); // cardboard sleeve
        LatheText(cv, "CAFE", 2 * Mathf.PI * .24f, .66f, .3f, .12f, Color.white);
        Spin(root, P(0, 0, .2f, 0, .26f, .66f, 0, .66f), Textured(cv, .3f));
        Spin(root, P(0, .65f, .28f, .65f, .28f, .7f, .2f, .74f, 0, .75f), Plain(C("#3a2a22"), .4f)); // lid
        return root;
    }

    // ---------- shared bits ----------

    static void Stem(GameObject root, float y, Color? color = null) =>
        Part(root, Prim(PrimitiveType.Cylinder), Plain(color ?? C("#6b4a1f"), .2f), new(0, y + .07f, 0), new(0, 0, 10), new(.035f, .08f, .035f));

    // green leaves radiating on top of a tomato or a strawberry
    static void Calyx(GameObject root, float y, int leaves, float length)
    {
        var green = Plain(C("#3e8f2e"), .3f);
        for (int i = 0; i < leaves; i++)
        {
            float a = i * 360f / leaves;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.right;
            Part(root, Prim(PrimitiveType.Cube), green, dir * length * .45f + Vector3.up * y, new(0, a, -12), new(length, .02f, .05f));
        }
        Stem(root, y - .02f, C("#3e8f2e"));
    }

    static Color Sprinkle(float k) => new[] { Color.white, C("#ff5c8a"), C("#ffd23f"), C("#4aa8ff"), C("#6cd46c") }[(int)(k * 97) % 5];

    // ---------- building blocks ----------

    static Vector2[] P(params float[] xy)
    {
        var pts = new Vector2[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
        return pts;
    }

    // sphere silhouette (squash < 1 flattens it), bottom at cy - r * squash
    static Vector2[] Ball(float r, float cy, float squash = 1, int n = 14)
    {
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(-90, 90, (float)i / n) * Mathf.Deg2Rad;
            pts[i] = new Vector2(Mathf.Max(0, Mathf.Cos(a) * r), cy + Mathf.Sin(a) * r * squash);
        }
        return pts;
    }

    // a closed loop around (R, r): spun, it becomes a donut lying on y = 0
    static Vector2[] Torus(float R, float r, int n = 18)
    {
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(-90, 270, (float)i / n) * Mathf.Deg2Rad;
            pts[i] = new Vector2(R + Mathf.Cos(a) * r, r + Mathf.Sin(a) * r);
        }
        return pts;
    }

    // pill shape along Y, length long, radius r (spun, then usually turned to lie along X)
    static Vector2[] Capsule(float length, float r) =>
        P(0, 0, r * .55f, r * .1f, r * .9f, r * .45f, r, r, r, length - r, r * .9f, length - r * .45f, r * .55f, length - r * .1f, 0, length);

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }
    }

    // scattered dots: the texture is cut into cells, `density` of them hold one dot of `radius` (in cell units)
    static bool Dot(float u, float v, int cellsU, int cellsV, float density, float radius, int seed, out float which)
    {
        float fu = u * cellsU, fv = v * cellsV;
        int cx = (int)fu, cy = (int)fv;
        which = Hash(cx, cy, seed);
        if (which > density) return false;
        float dx = fu - cx - (.3f + .4f * Hash(cx, cy, seed + 1)), dy = fv - cy - (.3f + .4f * Hash(cx, cy, seed + 2));
        return dx * dx + dy * dy < radius * radius;
    }

    static readonly Dictionary<(Color, float), Material> plainMats = new();

    static Material Plain(Color c, float smooth = .3f)
    {
        if (!plainMats.TryGetValue((c, smooth), out var m))
        {
            m = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial) { color = c };
            m.SetFloat("_Smoothness", smooth);
            plainMats[(c, smooth)] = m;
        }
        return m;
    }

    static Material Textured(Canvas cv, float smooth)
    {
        var m = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
        {
            mainTexture = cv.Bake(),
            color = Color.white,
        };
        m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Material Paint(System.Func<float, float, Color> paint, float smooth, int size = 128)
    {
        var cv = new Canvas(size, size);
        cv.Paint(paint);
        return Textured(cv, smooth);
    }

    static readonly Dictionary<PrimitiveType, Mesh> prims = new();

    static Mesh Prim(PrimitiveType t)
    {
        if (!prims.TryGetValue(t, out var m))
        {
            var g = GameObject.CreatePrimitive(t);
            m = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
            prims[t] = m;
        }
        return m;
    }

    static Transform Part(GameObject root, Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default, Vector3? scale = null)
    {
        var go = new GameObject("part");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale ?? Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Transform Spin(GameObject root, Vector2[] profile, Material mat, Vector3 pos = default, Vector3 euler = default,
        int ribs = 0, float ribAmp = 0, Vector3? scale = null, int segments = 32) =>
        Part(root, Lathe(profile, segments, ribs, ribAmp), mat, pos, euler, scale);
}
