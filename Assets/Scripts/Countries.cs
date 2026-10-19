using UnityEngine;

// Products added for the countries: France and Italy's missing ones, and Japan's. Same building blocks as Food.cs.
public static partial class Products
{
    // ---------- France / Italy ----------

    // round wooden box with its red label, the cheese's white rind on top
    static GameObject Camembert()
    {
        var root = new GameObject("Camembert");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v > .25f && v < .8f ? C("#c8282e") : C("#e9cf9e") * (.92f + .1f * Mathf.PerlinNoise(u * 40, v * 3)));
        LatheText(cv, "CAMEMBERT", 2 * Mathf.PI * .42f, .3f, .155f, .1f, Color.white);
        Spin(root, P(0, 0, .42f, 0, .42f, .3f, 0, .3f), Textured(cv, .3f));
        Spin(root, P(0, .3f, .4f, .3f, .41f, .33f, .38f, .36f, 0, .36f), Plain(C("#f8f4ea"), .1f));
        return root;
    }

    // packet of spaghetti: tall blue box, a window showing the golden strands, the name on top
    static GameObject Pasta()
    {
        var root = new GameObject("Pasta");
        const float w = .5f, h = 1.1f, d = .22f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            float fu = Mathf.Repeat(u, .5f) / .5f, fv = v / BoxSide;
            if (v >= BoxSide) return C("#1e4fa8");
            if (u < .5f && fu > .2f && fu < .8f && fv > .12f && fv < .55f) // window
                return Mathf.Repeat(fu * 40, 1) < .55f ? C("#f2d27a") : C("#d9b25a");
            return Mathf.Abs(fv - .6f) < .02f ? C("#ffd23f") : C("#1e4fa8");
        });
        BoxText(cv, "SPAGHETTI", w, d, h, .78f, .1f, Color.white);
        Part(root, BoxMesh(w, h, d, 0), Textured(cv, .4f), Vector3.zero);
        return root;
    }

    // espresso: small white cup with its handle on a saucer, dark coffee under a hazelnut crema ring
    static GameObject Espresso()
    {
        var root = new GameObject("Espresso");
        var china = Plain(C("#fbfbf7"), .75f);
        Spin(root, P(0, 0, .42f, 0, .44f, .04f, .4f, .06f, 0, .05f), china);
        Spin(root, P(0, .05f, .14f, .05f, .23f, .12f, .27f, .48f, .25f, .5f, .23f, .45f, 0, .45f), china);
        Spin(root, P(0, .44f, .235f, .44f, .235f, .455f, 0, .455f), Plain(C("#3b1d10"), .7f));
        Spin(root, Torus(.17f, .025f), Plain(C("#b77a45"), .5f), new(0, .432f, 0)); // crema ring
        Spin(root, Torus(.09f, .03f), china, new(.3f, .3f, 0), new(90, 0, 0)); // handle
        return root;
    }

    // slice of tiramisu on a plate: sponge and mascarpone layers, cocoa on top
    static GameObject Tiramisu()
    {
        var root = new GameObject("Tiramisu");
        Spin(root, P(0, 0, .46f, 0, .48f, .03f, .44f, .05f, 0, .04f), Plain(C("#fbfbf7"), .7f));
        var sponge = Plain(C("#9a6438"), .2f);
        var cream = Plain(C("#f6ead2"), .3f);
        for (int i = 0; i < 4; i++)
            Part(root, Cube, i % 2 == 0 ? sponge : cream, new(0, .1f + i * .1f, 0), scale: new(.64f, .1f, .5f));
        Part(root, Cube, Paint((u, v) => C("#5a3018") * (.85f + .3f * Mathf.PerlinNoise(u * 30, v * 30)), .1f),
            new(0, .465f, 0), scale: new(.64f, .03f, .5f));
        return root;
    }

    // cannolo: blistered golden shell, generous ricotta cream at both ends dotted with pistachio and chocolate
    static GameObject Cannolo()
    {
        var root = new GameObject("Cannolo");
        var body = Tilted(root, new(0, 0, 10), new(0, .24f, 0));
        Part(body, Cylinder, Paint((u, v) =>
        {
            float n = Mathf.PerlinNoise(u * 40, v * 14);
            return n > .64f ? C("#c98a3e") : Color.Lerp(C("#7a3c16"), C("#a8602a"), n); // fried shell with little blisters
        }, .45f), Vector3.zero, new(0, 0, 90), new(.4f, .3f, .4f));
        var cream = Plain(C("#fbf6ea"), .25f);
        var pistachio = Plain(C("#7fb23e"), .3f);
        var choc = Plain(C("#3b2416"), .3f);
        foreach (float x in new[] { -.32f, .32f })
        {
            Part(body, Sphere, cream, new(x + Mathf.Sign(x) * .06f, 0, 0), scale: new(.34f, .4f, .4f));
            for (int k = 0; k < 7; k++)
            {
                float a = k * 0.9f;
                var p = new Vector3(x + Mathf.Sign(x) * .2f, Mathf.Cos(a) * .12f, Mathf.Sin(a) * .12f);
                Part(body, Sphere, k % 3 == 0 ? choc : pistachio, p, scale: Vector3.one * .07f);
            }
        }
        return root;
    }

    // Parmigiano: tall wheel with its rind and stamped name, a wedge cut off leaning in front
    static GameObject Parmesan()
    {
        var root = new GameObject("Parmesan");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v > .08f && v < .92f
            ? C("#e3b35a") * (.93f + .12f * (Dot(u, v, 40, 10, .5f, .25f, 3, out _) ? -.4f : Mathf.PerlinNoise(u * 50, v * 6)))
            : C("#d39a45"));
        LatheText(cv, "PARMIGIANO", 2 * Mathf.PI * .46f, .52f, .26f, .12f, C("#7a4a1c"));
        Spin(root, P(0, 0, .43f, 0, .46f, .05f, .46f, .47f, .43f, .52f, 0, .52f), Textured(cv, .25f), new(0, 0, .25f));
        var grain = Paint((u, v) => Dot(u, v, 16, 16, .5f, .3f, 9, out _) ? C("#f9e7b0") : C("#f0d68a"), .2f);
        Part(root, Prism(new(-.28f, 0, -.62f), new(.28f, 0, -.62f), new(0, 0, -.25f), new(0, .4f, 0)), grain, Vector3.zero);
        Part(root, Cube, Plain(C("#d39a45"), .25f), new(0, .2f, -.635f), scale: new(.58f, .4f, .03f)); // the wedge's rind
        return root;
    }

    // salami lying with its string, cut end showing the marbled inside, two slices flat in front
    static GameObject Salami()
    {
        var root = new GameObject("Salami");
        var marbled = Paint((u, v) => Dot(u, v, 12, 12, .65f, .28f, 6, out _) ? C("#f3d9cf") : C("#a8283a"), .4f);
        var casing = Paint((u, v) => C("#6e2626") * (.85f + .3f * Mathf.PerlinNoise(u * 24, v * 40)) + (Dot(u, v, 18, 30, .3f, .2f, 4, out _) ? new Color(.25f, .25f, .25f, 0) : Color.clear), .3f);
        Spin(root, P(0, 0, .17f, 0, .17f, .8f, .14f, .9f, .08f, .96f, 0, .97f), casing, new(.4f, .17f, .1f), new(0, 0, 90));
        Part(root, Cylinder, marbled, new(.405f, .17f, .1f), new(0, 0, 90), new(.34f, .005f, .34f)); // cut end
        Part(root, Cube, Plain(Color.white, .2f), new(-.6f, .17f, .1f), scale: new(.1f, .05f, .05f)); // string
        Part(root, Cylinder, marbled, new(-.1f, .02f, -.28f), scale: new(.32f, .02f, .32f));
        Part(root, Cylinder, marbled, new(.24f, .045f, -.3f), new(6, 0, 0), new(.32f, .02f, .32f));
        return root;
    }

    // panettone: tall loaf in its golden paper, dark domed top split in a cross, a few raisins
    static GameObject Panettone()
    {
        var root = new GameObject("Panettone");
        Spin(root, P(0, 0, .3f, 0, .3f, .7f, .33f, .78f, .29f, .92f, .17f, 1f, 0, 1.02f), Paint((u, v) =>
        {
            if (v < .69f) return C("#c9a04a") * (Mathf.Repeat(u * 24, 1) < .5f ? 1f : .88f) * (Mathf.Abs(v - .35f) < .06f ? .75f : 1f); // pleated paper
            if (Mathf.Abs(Mathf.Repeat(u * 4, 1) - .5f) < .03f && v > .85f) return C("#e9c27a"); // the cross cut
            if (Dot(u, v, 18, 8, .25f, .25f, 13, out _)) return C("#3b1a10"); // raisins
            return Color.Lerp(C("#7a3e15"), C("#b8682a"), Mathf.PerlinNoise(u * 16, v * 16));
        }, .35f, 256));
        return root;
    }

    // ---------- Japan ----------

    static Color Rice => C("#fbfbf6");

    static Material RiceMat() => Paint((u, v) => Dot(u, v, 18, 18, .5f, .3f, 4, out _) ? C("#ecebe2") : Rice, .25f);

    // two maki rolls: nori outside, rice, salmon in the middle
    static GameObject Maki()
    {
        var root = new GameObject("Maki");
        foreach (float x in new[] { -.3f, .3f })
        {
            var at = new Vector3(x, 0, x * .3f);
            Spin(root, P(0, 0, .28f, 0, .28f, .4f, 0, .4f), Plain(C("#1f2f24"), .45f), at);
            Spin(root, P(0, .4f, .25f, .4f, .25f, .42f, 0, .42f), RiceMat(), at);
            Spin(root, P(0, .42f, .1f, .42f, .1f, .435f, 0, .435f), Plain(C("#ff8a5c"), .5f), at);
        }
        return root;
    }

    // rice block with a striped salmon slice and a nori band
    static GameObject Nigiri()
    {
        var root = new GameObject("Nigiri");
        Spin(root, Capsule(.72f, .17f), RiceMat(), new(.36f, .15f, 0), new(0, 0, 90), scale: new(1, 1, 1.25f));
        Part(root, Cube, Paint((u, v) => Mathf.Repeat(u * 6 + v * 2, 1) < .16f ? C("#ffd6c4") : C("#ff7a4a"), .6f),
            new(0, .33f, 0), new(0, 0, -4), new(.86f, .08f, .44f));
        Part(root, Cube, Plain(C("#1f2f24"), .4f), new(0, .26f, 0), scale: new(.14f, .2f, .48f));
        return root;
    }

    // triangle of rice wrapped at the bottom in nori
    static GameObject Onigiri()
    {
        var root = new GameObject("Onigiri");
        Part(root, Prism(new(-.42f, 0, -.16f), new(.42f, 0, -.16f), new(0, .72f, -.16f), new(0, 0, .32f)), RiceMat(), Vector3.zero);
        Part(root, Cube, Plain(C("#1f2f24"), .4f), new(0, .15f, 0), scale: new(.36f, .3f, .34f));
        return root;
    }

    // red bowl of ramen: broth, noodles, half egg, naruto, chashu, nori, chopsticks across
    static GameObject Ramen()
    {
        var root = new GameObject("Ramen");
        Spin(root, P(0, 0, .2f, 0, .26f, .04f, .44f, .33f, .47f, .4f, .43f, .4f, .41f, .34f, 0, .34f),
            Paint((u, v) => v > .72f && v < .8f ? Color.white : C("#c8282e"), .5f));
        Spin(root, P(0, .33f, .42f, .33f, .42f, .36f, 0, .36f), Plain(C("#d39a55"), .5f));
        var noodle = Plain(C("#f6d77a"), .4f);
        for (int i = 0; i < 6; i++)
            Part(root, Cylinder, noodle, new(-.12f + i * .04f, .365f, -.05f + (i % 2) * .05f), new(90, 0, 15 + i * 6), new(.025f, .26f, .025f));
        Part(root, Sphere, Plain(Color.white, .5f), new(.17f, .37f, .1f), scale: new(.2f, .07f, .15f)); // half egg
        Part(root, Sphere, Plain(C("#ffa31a"), .6f), new(.17f, .395f, .1f), scale: new(.09f, .03f, .07f));
        Part(root, Cylinder, Plain(Color.white, .3f), new(-.2f, .37f, .12f), scale: new(.16f, .012f, .16f)); // naruto
        Part(root, Cylinder, Plain(C("#f06292"), .3f), new(-.2f, .382f, .12f), scale: new(.07f, .006f, .07f));
        Part(root, Cylinder, Plain(C("#b56a3a"), .3f), new(-.05f, .37f, -.2f), scale: new(.22f, .015f, .22f)); // chashu
        Part(root, Cube, Plain(C("#1f2f24"), .4f), new(.05f, .48f, .3f), new(-15, 0, 0), new(.3f, .3f, .02f)); // nori
        var stick = Plain(C("#c79a5a"), .4f);
        Part(root, Cylinder, stick, new(0, .43f, -.06f), new(0, 20, 84), new(.03f, .5f, .03f));
        Part(root, Cylinder, stick, new(0, .43f, .02f), new(0, 26, 84), new(.03f, .5f, .03f));
        return root;
    }

    // three dumplings on a skewer: pink, white, green
    static GameObject Dango()
    {
        var root = new GameObject("Dango");
        var body = Tilted(root, new(0, 0, -12));
        Part(body, Cylinder, Plain(C("#d9b37a"), .3f), new(0, .6f, 0), scale: new(.035f, .6f, .035f));
        string[] colors = { "#9ccf6a", "#fbf6ea", "#f7a8c4" };
        for (int i = 0; i < 3; i++)
            Part(body, Sphere, Plain(C(colors[i]), .45f), new(0, .28f + i * .29f, 0), scale: Vector3.one * .32f);
        return root;
    }

    // lacquered box: rice with a pickled plum, salmon, tamago, broccoli
    static GameObject Bento()
    {
        var root = new GameObject("Bento");
        Part(root, Cube, Plain(C("#b3202a"), .6f), new(0, .11f, 0), scale: new(.92f, .22f, .64f));
        var black = Plain(C("#1f1f24"), .6f);
        Part(root, Cube, black, new(0, .2f, 0), scale: new(.84f, .06f, .56f));
        Part(root, Cube, black, new(.02f, .26f, 0), scale: new(.03f, .08f, .56f)); // divider
        Part(root, Cube, RiceMat(), new(-.2f, .26f, 0), scale: new(.38f, .08f, .52f));
        Part(root, Sphere, Plain(C("#c2183a"), .6f), new(-.2f, .31f, 0), scale: Vector3.one * .1f);
        Part(root, Cube, Plain(C("#ff8a5c"), .5f), new(.22f, .27f, -.13f), scale: new(.34f, .09f, .22f));
        Part(root, Cube, Plain(C("#ffd23f"), .4f), new(.3f, .28f, .14f), scale: new(.18f, .11f, .2f));
        var green = Plain(C("#3e9b3a"), .3f);
        for (int i = 0; i < 4; i++)
            Part(root, Sphere, green, new(.12f + (i % 2) * .06f, .3f, .1f + (i / 2) * .07f), scale: Vector3.one * .09f);
        return root;
    }

    // matcha in an earthy bowl, its bamboo whisk beside it
    static GameObject Matcha()
    {
        var root = new GameObject("Matcha");
        Spin(root, P(0, 0, .18f, 0, .22f, .04f, .32f, .2f, .34f, .36f, 0, .36f),
            Paint((u, v) => v > .8f ? C("#3e3a34") : C("#8a7560") * (.9f + .15f * Mathf.PerlinNoise(u * 20, v * 8)), .5f));
        Spin(root, P(0, .34f, .31f, .34f, .31f, .365f, 0, .365f), Plain(C("#8dbb4a"), .3f));
        Spin(root, P(0, 0, .1f, 0, .12f, .22f, .05f, .32f, .05f, .52f, 0, .52f), Plain(C("#d9c48a"), .3f), new(.48f, 0, .05f), ribs: 16, ribAmp: .15f);
        return root;
    }

    // six octopus balls in a little boat tray: sauce, mayo, green flakes
    static GameObject Takoyaki()
    {
        var root = new GameObject("Takoyaki");
        var wood = Plain(C("#e2c08c"), .2f);
        Part(root, Cube, wood, new(0, .04f, 0), scale: new(.98f, .08f, .62f));
        foreach (float z in new[] { -.3f, .3f }) Part(root, Cube, wood, new(0, .1f, z), scale: new(.98f, .14f, .03f));
        var ball = Plain(C("#c27a35"), .4f);
        var sauce = Plain(C("#5a2a12"), .7f);
        for (int i = 0; i < 6; i++)
        {
            var p = new Vector3((i % 3 - 1) * .3f, .2f, (i / 3 - .5f) * .27f);
            Part(root, Sphere, ball, p, scale: Vector3.one * .3f);
            Part(root, Sphere, sauce, p + new Vector3(0, .09f, 0), scale: new(.24f, .1f, .24f));
        }
        var mayo = Plain(Color.white, .5f);
        for (int k = 0; k < 3; k++)
            Part(root, Cube, mayo, new(-.3f + k * .3f, .33f, 0), new(0, 35, 0), new(.03f, .02f, .55f));
        return root;
    }

    // two fried prawns: golden batter, red tails
    static GameObject Tempura()
    {
        var root = new GameObject("Tempura");
        var batter = Paint((u, v) => Color.Lerp(C("#c98a35"), C("#f2c46a"), Mathf.PerlinNoise(u * 30, v * 18)), .35f);
        var tail = Plain(C("#e8423c"), .5f);
        foreach (var (z, yaw) in new[] { (-.14f, 10f), (.14f, -8f) })
        {
            var body = Tilted(root, new(0, yaw, 0), new(0, 0, z));
            Spin(body, Capsule(.85f, .14f), batter, new(.42f, .14f, 0), new(0, 0, 90));
            Part(body, Sphere, tail, new(-.5f, .2f, .05f), new(0, 0, 30), new(.18f, .08f, .14f));
            Part(body, Sphere, tail, new(-.5f, .2f, -.05f), new(0, 0, 30), new(.18f, .08f, .14f));
        }
        return root;
    }

    // two pleated dumplings, browned underneath
    static GameObject Gyoza()
    {
        var root = new GameObject("Gyoza");
        var mat = Paint((u, v) => Mathf.Sin(2 * Mathf.PI * u) < -.35f ? C("#c98a45") : C("#f3e2c0"), .35f);
        float Radius(float t) => .2f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t)), .5f) * (1 + .07f * Mathf.Sin(t * 60));
        foreach (float z in new[] { -.2f, .2f })
            Part(root, Sweep(t => new Vector3(Mathf.Lerp(-.42f, .42f, t), Radius(t) * .6f, z - .12f * Mathf.Sin(Mathf.PI * t)), Radius, .7f), mat, Vector3.zero);
        return root;
    }

    // green pods in a small blue bowl
    static GameObject Edamame()
    {
        var root = new GameObject("Edamame");
        Spin(root, P(0, 0, .2f, 0, .24f, .04f, .4f, .22f, .42f, .26f, 0, .26f), Paint((u, v) => v > .8f ? Color.white : C("#2f6fb8"), .6f));
        var pod = Plain(C("#7cc35a"), .4f);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 2.4f;
            Spin(root, Capsule(.42f, .07f), pod, new(Mathf.Cos(a) * .16f, .28f + (i % 3) * .04f, Mathf.Sin(a) * .14f), new(70 + i * 9, i * 50, 90));
        }
        return root;
    }
}
