using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Products added for the countries: France and Italy's missing ones, and Japan's. Same building blocks as Food.cs.
public static partial class Products
{
    // ---------- France / Italy ----------

    // camembert: open poplar box holding the cheese, a slice cut out and set in front showing the creamy inside,
    // the lid standing up behind with its label facing the player
    static GameObject Camembert()
    {
        var root = new GameObject("Camembert");
        var box = new Canvas(256, 256);
        box.Paint((u, v) => C("#dcbc80") * (.88f + .16f * Mathf.PerlinNoise(u * 80, v * 3)));
        LatheText(box, "CAMEMBERT", 2 * Mathf.PI * .42f, .13f, .07f, .07f, C("#c8282e")); // printed round the box
        Spin(root, P(0, 0, .42f, 0, .42f, .13f, .4f, .13f, .4f, .03f, 0, .03f), Textured(box, .2f), new(0, 0, .12f)); // open box

        var rind = Paint((u, v) => C("#f8f6ef") * (.93f + .08f * Mathf.PerlinNoise(u * 40, v * 10)), .1f);
        var paste = Plain(C("#f3e0a2"), .35f);
        var wheel = P(0, 0, .36f, 0, .38f, .03f, .38f, .17f, .36f, .2f, 0, .2f);
        void CutFace(GameObject parent, float deg, Vector3 at) => // flat creamy face where the knife went through
            Part(parent, Cube, paste, at + new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * .18f, .1f, Mathf.Sin(deg * Mathf.Deg2Rad) * .18f),
                new(0, -deg, 0), new(.36f, .19f, .004f));
        // the wheel with a 50° slice missing at the front (-Z is 270°)
        var whole = new Vector3(0, .03f, .12f);
        Part(root, Lathe(wheel, 48, 0, 0, 295, 310), rind, whole);
        CutFace(root, 295, whole);
        CutFace(root, 245, whole);
        // the slice, pulled out towards the player
        var slice = new Vector3(0, 0, -.34f); // just outside the box's front wall (z = -.3)
        Part(root, Lathe(wheel, 12, 0, 0, 245, 50), rind, slice);
        CutFace(root, 295, slice);
        CutFace(root, 245, slice);

        return root;
    }

    // pain au chocolat (after a photo): a rounded loaf, glossy dark-caramel top mottled with lighter patches,
    // paler flaky sides; each blunt end shows the rolled dough as a cream spiral with the two chocolate sticks in it
    static GameObject PainAuChocolat()
    {
        var root = new GameObject("PainAuChocolat");
        const float R = .34f, flat = .55f, L = .7f;
        float Radius(float t) => R * (1 - .12f * Mathf.Pow(2 * t - 1, 4)); // a little narrower towards the ends
        var crust = Paint((u, v) =>
        {
            float up = Mathf.Sin(2 * Mathf.PI * u), n = Mathf.PerlinNoise(u * 14, v * 18);
            if (up > .15f)
            {
                var top = Color.Lerp(C("#5e2a0c"), C("#8a4418"), n);
                return Color.Lerp(top, C("#a8602a"), Mathf.Clamp01((Mathf.PerlinNoise(u * 12 + 5, v * 12) - .55f) * 3)); // soft lighter patches
            }
            var side = Mathf.Repeat(up * 10, 1) < .25f ? C("#9a6430") : C("#c08a4c"); // flaky layers
            return Color.Lerp(side, C("#6e3410"), Mathf.Clamp01((up + .15f) * 3)); // blend into the glaze
        }, .5f, 256);
        Part(root, Sweep(t => new Vector3(Mathf.Lerp(-L / 2, L / 2, t), R * flat, 0), Radius, flat, 40, 32), crust, Vector3.zero);

        // the ends: rolled layers spiralling in, two chocolate sticks caught in the roll
        var spiral = Paint((u, v) =>
        {
            var d = new Vector2(u - .5f, v - .5f);
            float r = d.magnitude, a = Mathf.Atan2(d.y, d.x) / (2 * Mathf.PI);
            if (r > .46f) return C("#a85c26"); // crust rim
            foreach (var c in new[] { new Vector2(.36f, .44f), new Vector2(.62f, .52f) })
                if (((new Vector2(u, v) - c) * new Vector2(1, 1.5f)).magnitude < .11f) return C("#2e160a");
            return Mathf.Repeat(r * 5 - a, 1) < .2f ? C("#c8924e") : C("#fbe2b0"); // a light cream: the ends face away from the sun
        }, .3f, 256);
        float end = Radius(0);
        foreach (float x in new[] { -L / 2, L / 2 })
            Part(root, Cylinder, spiral, new(x, R * flat, 0), new(0, 0, 90), new(2 * end * flat, .005f, 2 * end));
        return root;
    }

    // crème brûlée: low, wide fluted ramekin on a saucer, the caramel well burnt: amber with dark brown patches
    static GameObject CremeBrulee()
    {
        var root = new GameObject("CremeBrulee");
        Spin(root, P(0, 0, .5f, 0, .54f, .04f, .52f, .06f, 0, .05f), Plain(C("#fbfbf7"), .75f)); // saucer
        Spin(root, P(0, .05f, .4f, .05f, .42f, .07f, .42f, .21f, .39f, .21f, .38f, .19f, 0, .19f), Plain(C("#fbfbf7"), .7f),
            ribs: 28, ribAmp: .03f, segments: 84);
        Part(root, Cylinder, Paint((u, v) =>
        {
            float burnt = Mathf.PerlinNoise(u * 8 + 3, v * 8);
            if (Mathf.Abs(Mathf.PerlinNoise(u * 5, v * 5) - .5f) < .008f) return C("#e8b46a"); // a crack
            var amber = Color.Lerp(C("#9a4e18"), C("#c27a30"), Mathf.PerlinNoise(u * 4, v * 4 + 7));
            return Color.Lerp(amber, C("#4a200a"), Mathf.Clamp01((burnt - .5f) * 3)); // burnt patches
        }, .85f, 256), new(0, .195f, 0), scale: new(.76f, .006f, .76f));
        return root;
    }

    // moutarde (after the classic Dijon jar): bulging glass jar of pale mustard, black lid with a gold band,
    // a black ribbon with gold edges running down from the lid to a black label with a scalloped gold frame
    static GameObject Moutarde()
    {
        var root = new GameObject("Moutarde");
        const float H = .82f, R = .34f;
        var black = C("#1c1a17");
        var gold = C("#b89a52");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            float du = Mathf.Abs(Mathf.Repeat(u - FrontU + .5f, 1) - .5f); // distance round from the front
            float top = .58f - .05f * Mathf.Abs(Mathf.Sin(du * Mathf.PI * 9)); // scalloped top edge of the label
            if (du < .2f && v > .12f && v < top) // label, gold frame
                return du > .185f || v < .14f || v > top - .02f ? gold : black;
            if (du < .035f && v >= top) return du > .025f ? gold : black; // ribbon up to the lid
            return Dot(u, v, 40, 30, .3f, .15f, 8, out _) ? C("#c9a83a") : C("#e8d27a"); // mustard behind the glass
        });
        LatheText(cv, "MOUTARDE", 2 * Mathf.PI * R, H, .36f * H, .1f, gold);
        LatheText(cv, "DIJON", 2 * Mathf.PI * R, H, .22f * H, .07f, Color.white);
        Spin(root, P(0, 0, .3f, 0, .33f, .04f, .34f, .2f, .32f, .45f, .33f, .62f, .31f, .72f, .27f, .8f, .27f, H, 0, H), Textured(cv, .8f));
        Spin(root, P(0, .8f, .3f, .8f, .31f, .82f, .31f, .95f, .29f, .97f, 0, .97f),
            Paint((u, v) => v > .45f && v < .62f ? gold : black, .6f)); // lid, gold band round it
        return root;
    }

    // canelés: two dark caramelised fluted cakes, deep mahogany sides, a lighter top with its little hollow
    static GameObject Caneles()
    {
        var root = new GameObject("Caneles");
        var crust = Paint((u, v) => v > .86f ? C("#6e3412") : Color.Lerp(C("#1e0904"), C("#3e170a"), Mathf.PerlinNoise(u * 12, v * 10)), .45f); // deep: the light pushes browns to orange
        foreach (var (x, z) in new[] { (-.24f, .06f), (.24f, -.06f) })
            Spin(root, P(0, 0, .2f, 0, .21f, .02f, .2f, .3f, .18f, .4f, .17f, .44f, .1f, .45f, .07f, .41f, 0, .41f), crust, new(x, 0, z), ribs: 10, ribAmp: .1f);
        return root;
    }

    // saucisson: dry sausage with a white powdery bloom and a string net, one end cut, slices on a wooden board
    static GameObject Saucisson()
    {
        var root = new GameObject("Saucisson");
        Part(root, Cube, Plain(C("#c99a62"), .25f), new(0, .03f, 0), scale: new(1.05f, .06f, .6f)); // board
        var casing = Paint((u, v) =>
        {
            if (Mathf.Abs(Mathf.Repeat(u * 6 + v * 5, 1) - .5f) < .04f || Mathf.Abs(Mathf.Repeat(u * 6 - v * 5, 1) - .5f) < .04f)
                return C("#fbf8f0"); // string net
            return Color.Lerp(C("#e9e3d8"), C("#9a6a5a"), Mathf.PerlinNoise(u * 14, v * 30) * .7f); // white bloom over the meat
        }, .15f, 256);
        var marbled = Paint((u, v) => Dot(u, v, 12, 12, .65f, .28f, 6, out _) ? C("#f3d9cf") : C("#8a2232"), .35f);
        Spin(root, P(0, 0, .14f, 0, .15f, .03f, .15f, .74f, .12f, .8f, .06f, .84f, 0, .85f), casing, new(.36f, .21f, .05f), new(0, 0, 90));
        Part(root, Cylinder, marbled, new(.365f, .21f, .05f), new(0, 0, 90), new(.3f, .005f, .3f)); // cut end
        Part(root, Cube, Plain(C("#fbf8f0"), .2f), new(-.52f, .21f, .05f), scale: new(.1f, .04f, .04f)); // string end
        for (int k = 0; k < 3; k++)
            Part(root, Cylinder, marbled, new(-.2f + k * .2f, .07f + k * .004f, -.2f), new(4, 0, 0), new(.24f, .015f, .24f));
        return root;
    }

    // jar of jam: glass full of red jam, a cream label, the lid hidden under red-and-white gingham tied with string
    static GameObject Confiture()
    {
        var root = new GameObject("Confiture");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v > .25f && v < .6f ? C("#fbf3e4") : C("#9a1428") * (.8f + .35f * Sheen(u).r));
        LatheText(cv, "CONFITURE", 2 * Mathf.PI * .33f, .66f, .28f, .12f, C("#9a1428"));
        Spin(root, P(0, 0, .3f, 0, .33f, .03f, .33f, .56f, .3f, .62f, .28f, .66f, 0, .66f), Textured(cv, .8f));
        bool Check(float a, float b) => ((int)(a * 10) + (int)(b * 10)) % 2 == 0;
        var gingham = Paint((u, v) => Check(u, v) ? C("#d7282f") : Color.white, .3f);
        Spin(root, P(.38f, .5f, .37f, .62f, .32f, .7f, 0, .72f), Paint((u, v) => Check(u * 3, v) ? C("#d7282f") : Color.white, .3f), ribs: 9, ribAmp: .07f); // draped cloth
        Part(root, Cylinder, gingham, new(0, .715f, 0), scale: new(.62f, .006f, .62f));
        Spin(root, Torus(.3f, .015f), Plain(C("#c99a62"), .3f), new(0, .6f, 0)); // string
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
            Spin(root, P(0, .42f, .085f, .42f, .085f, .435f, 0, .435f), Plain(C("#ff8a5c"), .5f), at + new Vector3(-.04f, 0, .03f));
            Spin(root, P(0, .42f, .05f, .42f, .05f, .436f, 0, .436f), Plain(C("#6cbb3c"), .5f), at + new Vector3(.08f, 0, -.05f)); // cucumber
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
        for (int i = 0; i < 7; i++) // wavy noodles across the broth
        {
            float z0 = -.18f + i * .05f;
            Part(root, Sweep(t => new Vector3(Mathf.Lerp(-.3f, .3f, t), .37f, z0 + .03f * Mathf.Sin(t * 14 + i)), t => .016f, 1, 40, 6), noodle, Vector3.zero);
        }
        Part(root, Sphere, Plain(Color.white, .5f), new(.17f, .37f, .1f), scale: new(.2f, .07f, .15f)); // half egg
        Part(root, Sphere, Plain(C("#ffa31a"), .6f), new(.17f, .395f, .1f), scale: new(.09f, .03f, .07f));
        Part(root, Cylinder, Plain(Color.white, .3f), new(-.2f, .37f, .12f), scale: new(.16f, .012f, .16f)); // naruto
        Part(root, Cylinder, Plain(C("#f06292"), .3f), new(-.2f, .382f, .12f), scale: new(.07f, .006f, .07f));
        Part(root, Cylinder, Plain(C("#b56a3a"), .3f), new(-.05f, .37f, -.2f), scale: new(.22f, .015f, .22f)); // chashu
        Part(root, Cube, Plain(C("#1f2f24"), .4f), new(.12f, .44f, .3f), new(-35, 0, 0), new(.22f, .22f, .02f)); // nori leaning on the rim
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

    // matcha: wide tea bowl with a dark glaze dripping from the rim, bright frothy tea, bamboo whisk beside it
    static GameObject Matcha()
    {
        var root = new GameObject("Matcha");
        Spin(root, P(0, 0, .2f, 0, .24f, .04f, .4f, .2f, .43f, .34f, .41f, .36f, .39f, .3f, 0, .3f), Paint((u, v) =>
        {
            float drip = .72f - .14f * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 7)), 4);
            return v > drip ? C("#3e3a34") : C("#c2a27a") * (.9f + .15f * Mathf.PerlinNoise(u * 20, v * 8));
        }, .55f));
        Part(root, Cylinder, Paint((u, v) => Dot(u, v, 14, 14, .5f, .3f, 7, out _) ? C("#c9e48a") : C("#8fc24a"), .35f),
            new(0, .31f, 0), scale: new(.78f, .01f, .78f));
        Spin(root, P(0, 0, .11f, 0, .13f, .24f, .06f, .34f, .06f, .62f, 0, .62f), Plain(C("#e3cf96"), .3f), new(.56f, 0, .1f), ribs: 18, ribAmp: .18f);
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
        var bonito = Plain(C("#e8c79a"), .2f);
        var aonori = Plain(C("#3e7a2a"), .3f);
        for (int k = 0; k < 14; k++) // bonito flakes and seaweed sprinkled on top
        {
            var p = new Vector3((Hash(k, 1, 3) - .5f) * .8f, .36f, (Hash(k, 2, 3) - .5f) * .45f);
            if (k % 2 == 0) Part(root, Cube, bonito, p, new(Hash(k, 3, 3) * 60, Hash(k, 4, 3) * 180, 20), new(.1f, .008f, .07f));
            else Part(root, Sphere, aonori, p + new Vector3(0, .01f, 0), scale: Vector3.one * .035f);
        }
        return root;
    }

    // two prawn tempura on a white plate: curved bodies in lumpy golden batter, orange tails fanned out
    static GameObject Tempura()
    {
        var root = new GameObject("Tempura");
        Part(root, Cube, Plain(C("#fbfbf7"), .7f), new(0, .03f, 0), scale: new(1f, .06f, .62f));
        var batter = Paint((u, v) =>
        {
            float n = Mathf.PerlinNoise(u * 28, v * 22);
            return Dot(u, v, 22, 30, .35f, .3f, 5, out _) ? C("#f6d48a") : Color.Lerp(C("#c98a35"), C("#efbd62"), n);
        }, .3f);
        var tail = Plain(C("#f05a2a"), .5f);
        foreach (float z in new[] { -.14f, .14f })
        {
            float Radius(float t) => .13f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Lerp(0, .9f, t))), .4f) * (1 + .12f * Mathf.Sin(t * 47 + z * 30));
            Part(root, Sweep(t => new Vector3(Mathf.Lerp(-.3f, .44f, t), .19f + .05f * Mathf.Sin(Mathf.PI * t), z + .06f * Mathf.Sin(Mathf.PI * t)), Radius, .9f), batter, Vector3.zero);
            foreach (float yaw in new[] { -25f, 25f })
                Part(root, Sphere, tail, new(-.4f, .24f, z), new(0, yaw, 35), new(.2f, .04f, .1f));
        }
        return root;
    }

    // ramune: codd-neck bottle of pale blue glass, the marble caught in its pinched neck, blue wrap label
    static GameObject Ramune()
    {
        var root = new GameObject("Ramune");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) => v > .16f && v < .42f ? C("#2f7ae5") : C("#bfe3e6") * Sheen(u) * (v < .55f ? .9f : 1f));
        LatheText(cv, "RAMUNE", 2 * Mathf.PI * .26f, 1.12f, .32f, .14f, Color.white);
        Spin(root, P(0, 0, .24f, 0, .26f, .04f, .26f, .58f, .2f, .7f, .1f, .78f, .15f, .86f, .09f, .95f, .11f, 1.02f, .1f, 1.12f, 0, 1.12f), Textured(cv, .85f));
        Part(root, Sphere, Plain(C("#e6f6ff"), .95f), new(0, .86f, 0), scale: Vector3.one * .2f); // the marble
        Spin(root, P(0, 1.08f, .12f, 1.08f, .12f, 1.16f, 0, 1.16f), Plain(C("#2f7ae5"), .5f)); // cap
        return root;
    }

    // three gyoza on a black plate: pleated crescents, crisp golden undersides, a little dish of soy sauce
    static GameObject Gyoza()
    {
        var root = new GameObject("Gyoza");
        Part(root, Cube, Plain(C("#1f1f24"), .6f), new(0, .03f, 0), scale: new(1f, .06f, .66f));
        var mat = Paint((u, v) => Mathf.Sin(2 * Mathf.PI * u) < -.2f ? C("#b06a2a") : C("#f5e6c8") * (Mathf.Repeat(v * 9, 1) < .15f ? .9f : 1f), .4f);
        float Radius(float t) => .17f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t)), .5f) * (1 + .12f * Mathf.Abs(Mathf.Sin(t * 28)));
        foreach (float z in new[] { -.18f, 0, .18f })
            Part(root, Sweep(t => new Vector3(Mathf.Lerp(-.38f, .28f, t), .06f + Radius(t) * .65f, z - .06f * Mathf.Sin(Mathf.PI * t)), Radius, .75f), mat, Vector3.zero);
        Spin(root, P(0, .06f, .08f, .06f, .12f, .14f, 0, .12f), Plain(Color.white, .7f), new(.4f, 0, .16f)); // soy dish
        Part(root, Cylinder, Plain(C("#2a120a"), .9f), new(.4f, .125f, .16f), scale: new(.18f, .004f, .18f));
        return root;
    }

    // edamame: bright green pods, each bulging over its three beans, piled in a small blue bowl
    static GameObject Edamame()
    {
        var root = new GameObject("Edamame");
        Spin(root, P(0, 0, .2f, 0, .24f, .04f, .4f, .22f, .42f, .26f, .38f, .24f, 0, .2f), Paint((u, v) => v > .8f ? Color.white : C("#2f6fb8"), .6f));
        var pod = Plain(C("#86c84a"), .35f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 1.05f;
            var p = Tilted(root, new(10 + (i % 2) * 15, a * 57, 0), new(Mathf.Cos(a) * .14f, .26f + (i % 3) * .05f, Mathf.Sin(a) * .12f));
            for (int b = 0; b < 3; b++) // the beans' bumps
                Part(p, Sphere, pod, new((b - 1) * .1f, 0, 0), scale: new(.15f, .1f, .12f));
        }
        return root;
    }
}
