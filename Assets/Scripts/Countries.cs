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

    // slice of tiramisu on a plate: two rows of coffee-soaked ladyfingers (their round ends show on the cut sides)
    // between thick mascarpone layers, a velvety cocoa top with two coffee beans, cocoa dusted on the plate
    static GameObject Tiramisu()
    {
        var root = new GameObject("Tiramisu");
        Spin(root, P(0, 0, .46f, 0, .48f, .03f, .44f, .05f, 0, .04f), Paint((u, v) =>
            Dot(u, v, 40, 8, .25f, .2f, 5, out _) && v > .5f ? C("#8a5a3a") : C("#fbfbf7"), .7f)); // cocoa specks on the plate
        var sponge = Paint((u, v) =>
        {
            float fu = Mathf.Repeat(u * 4, 1) - .5f, fv = v - .5f; // four ladyfinger ends side by side
            float r = fu * fu / .2f + fv * fv / .22f;
            return r < 1 ? Color.Lerp(C("#b57a42"), C("#7a4522"), r * r) : C("#5e321a"); // soaked darker at the rim
        }, .2f);
        var cream = Paint((u, v) => C("#f7ecd6") * (.96f + .06f * Mathf.PerlinNoise(u * 12, v * 6)), .35f);
        float y = .04f;
        foreach (var (mat, h) in new[] { (sponge, .12f), (cream, .13f), (sponge, .12f), (cream, .14f) })
        {
            Part(root, Cube, mat, new(0, y + h / 2, 0), scale: new(.58f, h, .46f));
            y += h;
        }
        Part(root, Cube, Paint((u, v) => C("#4a2412") * (.8f + .4f * Mathf.PerlinNoise(u * 40, v * 40)), .05f),
            new(0, y + .015f, 0), scale: new(.6f, .03f, .48f)); // cocoa, a hair wider than the slice
        var bean = Plain(C("#2e1608"), .6f);
        Part(root, Sphere, bean, new(-.1f, y + .05f, -.05f), new(0, 30, 0), new(.09f, .05f, .065f));
        Part(root, Sphere, bean, new(.06f, y + .05f, .06f), new(0, -40, 0), new(.09f, .05f, .065f));
        return root;
    }

    // extra-virgin olive oil: tall slim dark green bottle with a long neck and a gold cap, cream label with a gold
    // frame, the name and an olive branch
    static GameObject OliveOil()
    {
        const float H = 1.45f, R = .2f, labelLo = .2f, labelHi = .55f, capFrom = 1.28f / H;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            if (v >= capFrom) return Ridged(C("#c9a24a"), u);
            if (v < labelLo || v > labelHi) return C("#23401f") * Sheen(u);
            float f = (v - labelLo) / (labelHi - labelLo);
            if (f < .05f || f > .95f) return C("#c9a24a");
            // olive branch under the name, on the front: a stem, two leaves, two olives
            float du = Mathf.DeltaAngle(FrontU * 360, u * 360) / 360 * 2 * Mathf.PI * R / (H * (labelHi - labelLo)); // label heights
            var p = new Vector2(du, f - .3f);
            bool olive = (p - new Vector2(-.06f, -.02f)).sqrMagnitude < .004f || (p - new Vector2(.05f, -.05f)).sqrMagnitude < .004f;
            bool leaf = Mathf.Pow((p.x + .12f) / .1f, 2) + Mathf.Pow((p.y - .08f) / .035f, 2) < 1
                || Mathf.Pow((p.x - .11f) / .1f, 2) + Mathf.Pow((p.y - .05f) / .035f, 2) < 1;
            bool stem = Mathf.Abs(p.y - .06f + p.x * .1f) < .01f && Mathf.Abs(p.x) < .2f;
            return olive ? C("#3b4a1a") : leaf ? C("#6f8f3a") : stem ? C("#5a4a2a") : C("#f6efd9");
        });
        LatheText(cv, "OLIO", 2 * Mathf.PI * R, H, (labelLo + (labelHi - labelLo) * .72f) * H, (labelHi - labelLo) * H * .32f, C("#23401f"));
        return Spun("OLIO", P(0, 0, .18f, 0, .2f, .03f, .2f, .9f, .17f, 1f, .1f, 1.08f, .07f, 1.14f, .07f, 1.28f, .085f, 1.29f, .085f, 1.43f,
            .06f, 1.45f, 0, 1.45f), cv, .75f);
    }

    // mozzarella di bufala: a glossy white ball, pinched into a little knot on top where it was tied off, two basil
    // leaves on it, in a blue-glazed bowl with a white rim
    static GameObject Mozzarella()
    {
        var root = new GameObject("Mozzarella");
        Spin(root, P(0, 0, .3f, 0, .32f, .03f, .44f, .2f, .46f, .24f, .42f, .24f, .3f, .08f, 0, .08f),
            Paint((u, v) => v > .85f ? Color.white : C("#5c93cf"), .6f));
        var moz = Plain(C("#fbfaf4"), .8f);
        Spin(root, Ball(.34f, .4f, .82f), moz);
        Spin(root, P(0, .66f, .06f, .66f, .04f, .72f, .05f, .76f, 0, .78f), moz); // the knot
        var basil = Plain(C("#3f9a2e"), .55f);
        Part(root, Sphere, basil, new(.09f, .67f, -.07f), new(0, 30, -14), new(.24f, .03f, .12f));
        Part(root, Sphere, basil, new(-.07f, .67f, -.02f), new(0, -50, 12), new(.2f, .03f, .1f));
        return root;
    }

    // Parmigiano Reggiano: a big wedge cut from the wheel, straw-coloured and crumbly inside with white crystals,
    // the golden rind along its back dotted with the stamped name; two crumbs in front
    static GameObject Parmesan()
    {
        var root = new GameObject("Parmesan");
        var grain = Paint((u, v) =>
        {
            if (Dot(u, v, 24, 24, .2f, .15f, 9, out _)) return C("#fff6dc"); // crystals
            return Mathf.PerlinNoise(u * 18, v * 18) < .25f ? C("#e2c06e") : C("#f5dc94"); // little crevices
        }, .15f, 256);
        var rind = Paint((u, v) => v > .3f && v < .7f && Dot(u, v, 70, 12, .55f, .3f, 4, out _) ? C("#8a5a20")
            : C("#d9a441") * (.92f + .12f * Mathf.PerlinNoise(u * 60, v * 8)), .25f, 256);
        const float R = .76f, Rin = .7f, H = .6f, from = 62, span = 56; // centred on +Z: the point faces the player
        Part(root, Lathe(P(0, 0, Rin, 0, Rin, H, 0, H), 16, 0, 0, from, span), grain, Vector3.zero);
        Part(root, Lathe(P(Rin, 0, R, 0, R, H, Rin, H), 16, 0, 0, from, span), rind, Vector3.zero);
        foreach (float d in new[] { from, from + span }) // the two cut faces
            Part(root, Cube, grain, new(Mathf.Cos(d * Mathf.Deg2Rad) * R / 2, H / 2, Mathf.Sin(d * Mathf.Deg2Rad) * R / 2), new(0, -d, 0), new(R, H, .004f));
        Part(root, Cube, grain, new(-.12f, .05f, -.12f), new(10, 30, 5), Vector3.one * .1f);
        Part(root, Cube, grain, new(.1f, .04f, -.18f), new(-8, 70, 12), Vector3.one * .08f);
        return root;
    }

    // pesto alla genovese: squat glass jar showing the green sauce (basil flecks, a little oil), navy screw lid,
    // navy label with the name over a gold rule
    static GameObject Pesto()
    {
        const float H = .74f, R = .39f, labelLo = .12f, labelHi = .44f, lidFrom = .56f / H;
        var navy = C("#1d2f6b");
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            if (v >= lidFrom) return Ridged(navy, u) * .85f;
            float y = v * H;
            if (y > labelLo && y < labelHi) return Mathf.Abs(y - labelLo - .06f) < .008f ? C("#d9b44a") : navy;
            return (Dot(u, v, 40, 14, .5f, .3f, 8, out float k) ? (k < .25f ? C("#9cb84a") : C("#355f1c")) : C("#5a8a2a")) * Sheen(u);
        });
        LatheText(cv, "PESTO", 2 * Mathf.PI * R, H, .32f, .13f, Color.white);
        return Spun("PESTO", P(0, 0, .36f, 0, .38f, .03f, .39f, .08f, .39f, .48f, .35f, .54f, .32f, .56f, .36f, .57f, .37f, .58f,
            .37f, .72f, .35f, .74f, 0, .74f), cv, .6f);
    }

    // salami lying on a wooden board with its string, cut end showing the marbled inside, two slices in front
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
        Part(root, Cube, Paint((u, v) => Color.Lerp(C("#c08a52"), C("#a5703c"), Mathf.PerlinNoise(u * 2, v * 30)), .25f),
            new(-.08f, -.03f, -.1f), scale: new(1.4f, .06f, .75f)); // wooden board
        return root;
    }

    // panettone: about as wide as tall, a short pleated paper case, the dark dome puffing out over it like a mushroom,
    // split in a cross on top, a few raisins
    static GameObject Panettone()
    {
        var root = new GameObject("Panettone");
        Spin(root, P(0, 0, .42f, 0, .42f, .5f, .46f, .54f, .47f, .6f, .43f, .71f, .31f, .81f, .15f, .87f, 0, .88f), Paint((u, v) =>
        {
            if (v < .57f) return C("#c9a04a") * (Mathf.Repeat(u * 24, 1) < .5f ? 1f : .88f) * (Mathf.Abs(v - .3f) < .05f ? .75f : 1f); // pleated paper
            if (Mathf.Abs(Mathf.Repeat(u * 4, 1) - .5f) < .03f && v > .88f) return C("#e9c27a"); // the cross cut
            if (Dot(u, v, 18, 8, .25f, .25f, 13, out _)) return C("#3b1a10"); // raisins
            return Color.Lerp(C("#7a3e15"), C("#b8682a"), Mathf.PerlinNoise(u * 16, v * 16));
        }, .35f, 256));
        return root;
    }

    // ---------- Japan ----------

    static Color Rice => C("#fbfbf6");

    // packed grains: white ovals with thin grey gaps between them
    static Color RiceGrain(float u, float v) => Dot(u, v, 28, 28, .9f, .42f, 4, out _) ? Rice : C("#efeee6");
    static Material RiceMat() => Paint(RiceGrain, .25f, 256);

    // white plate with a blue rim, squashed to an oval (sx, sz), a dab of wasabi and pink pickled ginger at the front
    static void SushiPlate(GameObject root, float sx, float sz)
    {
        Spin(root, P(0, 0, .46f, 0, .48f, .03f, .44f, .05f, 0, .04f), Plain(C("#fbfbf7"), .7f), scale: new(sx, 1, sz));
        Spin(root, Torus(.45f, .016f), Plain(C("#2b4a8a"), .5f), new(0, .025f, 0), scale: new(sx, 1, sz));
        Part(root, Sphere, Plain(C("#9bc43c"), .3f), new(.48f * sx - .22f, .07f, -.48f * sz + .16f), scale: new(.12f, .07f, .1f));
        var ginger = Plain(C("#f2b3b8"), .4f);
        Part(root, Cylinder, ginger, new(.48f * sx - .38f, .055f, -.48f * sz + .18f), new(8, 0, 0), new(.13f, .006f, .1f));
        Part(root, Cylinder, ginger, new(.48f * sx - .34f, .065f, -.48f * sz + .2f), new(-6, 30, 0), new(.12f, .006f, .09f));
    }

    // two maki rolls on a plate: nori outside, rice, salmon and cucumber in the middle
    static GameObject Maki()
    {
        var root = new GameObject("Maki");
        SushiPlate(root, 1.6f, 1.15f);
        foreach (float x in new[] { -.3f, .3f })
        {
            var at = new Vector3(x, .04f, x * .3f + .08f);
            Spin(root, P(0, 0, .28f, 0, .28f, .4f, 0, .4f), Plain(C("#1f2f24"), .45f), at);
            Spin(root, P(0, .4f, .25f, .4f, .25f, .42f, 0, .42f), RiceMat(), at);
            Spin(root, P(0, .42f, .085f, .42f, .085f, .435f, 0, .435f), Plain(C("#ff8a5c"), .5f), at + new Vector3(-.04f, 0, .03f));
            Spin(root, P(0, .42f, .05f, .42f, .05f, .436f, 0, .436f), Plain(C("#6cbb3c"), .5f), at + new Vector3(.08f, 0, -.05f)); // cucumber
        }
        return root;
    }

    // salmon nigiri on a plate: an oval of rice, a soft rounded slice of salmon draped over it, its fat lines across
    static GameObject Nigiri()
    {
        var root = new GameObject("Nigiri");
        SushiPlate(root, 1.35f, 1f);
        Spin(root, Capsule(.72f, .17f), RiceMat(), new(.36f, .21f, .06f), new(0, 0, 90), scale: new(1, 1, 1.25f));
        // the slice: a flattened capsule longer than the rice, bent so both ends hang down over it; bright orange with
        // a few wide, soft white fat lines slanting across
        var slice = Lathe(Capsule(.9f, .14f), 28);
        var vs = slice.vertices;
        for (int i = 0; i < vs.Length; i++) vs[i].x -= .75f * Mathf.Pow((vs[i].y - .45f) / .45f, 2); // local x ends up as height (squashed below)
        slice.vertices = vs;
        slice.RecalculateNormals();
        slice.RecalculateBounds();
        Part(root, slice, Paint((u, v) =>
        {
            float fat = Mathf.Clamp01(1 - Mathf.Abs(Mathf.Repeat(v * 5 + u * .6f, 1) - .5f) / .09f);
            return Color.Lerp(Color.Lerp(C("#ff6a2a"), C("#ff9350"), Mathf.PerlinNoise(u * 6, v * 6)), C("#ffe6d6"), fat * .9f);
        }, .75f, 256), new(.45f, .42f, .06f), new(0, 0, 90), new(.2f, 1, 1.55f)); // thin: about a sixth of the rice's height
        return root;
    }

    // onigiri: a thick triangle of packed rice, corners and edges just rounded, a tall strip of nori wrapped round its base
    static GameObject Onigiri()
    {
        var root = new GameObject("Onigiri");
        const int N = 60, M = 10;
        const float depth = .2f, R = .5f;
        // the outline: a triangle (corner up) blended with a circle, which rounds its corners
        var outline = new Vector2[N + 1];
        for (int i = 0; i <= N; i++)
        {
            float t = i * 2 * Mathf.PI / N, phi = Mathf.Repeat(t - Mathf.PI / 2, 2 * Mathf.PI / 3) - Mathf.PI / 3;
            float r = Mathf.Lerp(.5f / Mathf.Cos(phi), .75f, .2f) * R;
            outline[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r;
        }
        float lift = -outline.Min(p => p.y);
        var c = new Vector2(0, lift);
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        // rings from front to back, pulled in toward the edges for a pillowy rim; uv = position, so the nori lines up
        for (int j = 0; j <= M; j++)
        {
            float s = -1 + 2f * j / M, f = .7f + .3f * Mathf.Sqrt(1 - s * s);
            foreach (var o in outline)
            {
                var p = c + o * f;
                verts.Add(new Vector3(p.x, p.y, s * depth));
                uvs.Add(new Vector2(p.x + .5f, p.y));
            }
        }
        int capF = verts.Count, capB = capF + 1;
        verts.Add(new Vector3(c.x, c.y, -depth - .03f));
        verts.Add(new Vector3(c.x, c.y, depth + .03f));
        uvs.Add(new Vector2(c.x + .5f, c.y));
        uvs.Add(new Vector2(c.x + .5f, c.y));
        var center = new Vector3(c.x, c.y, 0);
        void Tri(int a, int b, int d) // wound to face outwards
        {
            bool outward = Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[d] - verts[a]), (verts[a] + verts[b] + verts[d]) / 3 - center) > 0;
            tris.AddRange(outward ? new[] { a, b, d } : new[] { a, d, b });
        }
        int row = N + 1;
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < M; j++)
            {
                int a = j * row + i, b = a + row;
                Tri(a, b, a + 1);
                Tri(a + 1, b, b + 1);
            }
            Tri(capF, i, i + 1);
            Tri(capB, M * row + i, M * row + i + 1);
        }
        // a tall rectangle of nori over the bottom middle, front, back and underneath; the rice grains everywhere else
        Part(root, Finish(verts, uvs, tris), Paint((u, v) => Mathf.Abs(u - .5f) < .2f && v < .36f
            ? C("#1c2a20") * (.85f + .3f * Mathf.PerlinNoise(u * 40, v * 40)) : RiceGrain(u, v), .3f, 256), Vector3.zero);
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

    // bento: an open box, black lacquer outside and red inside, a divider down the middle. Left: a mound of rice with
    // a pickled plum and sesame. Right: grilled salmon, two slices of rolled omelette, fried chicken on lettuce,
    // broccoli and a cherry tomato, all heaped a little above the rim.
    static GameObject Bento()
    {
        var root = new GameObject("Bento");
        var black = Plain(C("#1c1c22"), .75f);
        var red = Plain(C("#b3202a"), .6f);
        Part(root, Cube, black, new(0, .02f, 0), scale: new(1f, .04f, .7f));
        foreach (int s in new[] { -1, 1 })
        {
            Part(root, Cube, black, new(0, .13f, s * .33f), scale: new(1f, .26f, .04f));
            Part(root, Cube, black, new(s * .48f, .13f, 0), scale: new(.04f, .26f, .62f));
            Part(root, Cube, red, new(0, .14f, s * .307f), scale: new(.92f, .24f, .005f)); // red lining
            Part(root, Cube, red, new(s * .457f, .14f, 0), scale: new(.005f, .24f, .62f));
        }
        Part(root, Cube, red, new(0, .045f, 0), scale: new(.92f, .01f, .62f));
        Part(root, Cube, red, new(0, .13f, 0), scale: new(.02f, .22f, .62f)); // divider

        // rice, domed, a few sesame seeds, the plum in the middle
        var rice = Paint((u, v) => Dot(u, v, 14, 14, .12f, .12f, 17, out _) ? C("#2a2a2a") : RiceGrain(u, v), .25f, 256);
        Part(root, Cube, rice, new(-.24f, .14f, 0), scale: new(.42f, .18f, .6f));
        Part(root, Sphere, rice, new(-.24f, .23f, 0), scale: new(.42f, .09f, .58f));
        Part(root, Sphere, Plain(C("#b8182e"), .45f), new(-.24f, .28f, 0), scale: new(.11f, .09f, .11f));

        // back: grilled salmon, two slices of tamagoyaki
        Part(root, Cube, Paint((u, v) => Mathf.Repeat(u * 4 + v * 4, 1) < .12f ? C("#8a3a1e")
            : Color.Lerp(C("#f08a5c"), C("#f6a882"), Mathf.PerlinNoise(u * 8, v * 8)), .4f), new(.12f, .22f, .16f), new(0, 12, 0), new(.2f, .14f, .22f));
        var tamago = Paint((u, v) => Mathf.Repeat(new Vector2(u - .5f, v - .5f).magnitude * 6, 1) < .2f ? C("#f7e08a") : C("#f5c33a"), .35f);
        foreach (float x in new[] { .3f, .41f })
            Part(root, Cube, tamago, new(x, .22f, .16f), new(0, 0, x > .35f ? -6 : 4), new(.09f, .13f, .2f));

        // front: fried chicken on a lettuce leaf, broccoli, a cherry tomato
        Part(root, Sphere, Plain(C("#7cc24a"), .35f), new(.14f, .2f, -.15f), new(0, 20, 6), new(.26f, .05f, .22f));
        var karaage = Paint((u, v) => Color.Lerp(C("#9a521c"), C("#d38c3c"), Mathf.PerlinNoise(u * 20, v * 20)), .35f);
        foreach (var p in new Vector3[] { new(.08f, .25f, -.12f), new(.19f, .25f, -.2f), new(.17f, .28f, -.08f) })
            Part(root, Sphere, karaage, p, new(20, p.x * 300, 0), new(.13f, .11f, .12f));
        var broccoli = Plain(C("#3e8f2e"), .3f);
        foreach (var p in new Vector3[] { new(.36f, .27f, -.12f), new(.41f, .26f, -.18f), new(.33f, .25f, -.19f), new(.39f, .29f, -.15f) })
            Part(root, Sphere, broccoli, p, scale: Vector3.one * .085f);
        Part(root, Sphere, Plain(C("#e3261c"), .8f), new(.4f, .25f, -.02f), scale: Vector3.one * .1f);
        return root;
    }

    // matcha: a wide, shallow cream tea bowl with a brown foot, filled near the rim with bright green froth swirled by
    // the whisk; the bamboo whisk standing beside it (handle, then fine prongs flaring into a bulb), the bamboo scoop
    // lying in front
    static GameObject Matcha()
    {
        var root = new GameObject("Matcha");
        Spin(root, P(0, 0, .18f, 0, .22f, .04f, .4f, .16f, .46f, .3f, .45f, .32f, .43f, .28f, 0, .27f), Paint((u, v) =>
            v < .15f ? C("#8a6a4a") : C("#ece2cc") * (.94f + .1f * Mathf.PerlinNoise(u * 30, v * 10)), .55f));
        Part(root, Cylinder, Paint((u, v) =>
        {
            var p = new Vector2(u - .5f, v - .5f);
            float swirl = Mathf.Abs(Mathf.Sin(p.magnitude * 30 + Mathf.Atan2(p.y, p.x) * 2)); // whisk marks
            if (Dot(u, v, 30, 30, .4f, .25f, 7, out _)) return C("#c9e68e"); // little bubbles
            return Color.Lerp(C("#6fae2e"), C("#a6d65a"), swirl * .6f + .3f * Mathf.PerlinNoise(u * 10, v * 10));
        }, .45f, 256), new(0, .29f, 0), scale: new(.86f, .01f, .86f));

        var bamboo = Plain(C("#e7d49e"), .35f);
        var whisk = new Vector3(.62f, 0, .08f);
        Spin(root, P(0, 0, .075f, 0, .075f, .22f, 0, .22f), Paint((u, v) => Mathf.Abs(v - .45f) < .04f ? C("#b89a5a") : C("#e2cc90"), .35f), whisk); // handle with its node
        Part(root, Cylinder, bamboo, whisk + new Vector3(0, .32f, 0), scale: new(.08f, .1f, .08f)); // the inner prongs
        for (int i = 0; i < 18; i++)
        {
            float a = i * 20 * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 p1 = whisk + d * .06f + Vector3.up * .22f, p2 = whisk + d * .16f + Vector3.up * .44f, p3 = whisk + d * .1f + Vector3.up * .56f;
            foreach (var (lo, hi) in new[] { (p1, p2), (p2, p3) })
                Part(root, Cube, bamboo, (lo + hi) / 2, Quaternion.FromToRotation(Vector3.up, hi - lo).eulerAngles,
                    new(.016f, (hi - lo).magnitude + .01f, .016f));
        }
        Part(root, Cube, Plain(C("#c9a86a"), .35f), new(.05f, .01f, -.5f), new(0, 18, 0), new(.55f, .015f, .045f)); // scoop
        return root;
    }

    // takoyaki: six golden octopus balls packed in a pointed wooden boat tray with upturned ends, glossy dark sauce
    // dripping down from their tops, a zigzag of mayo across, green seaweed and bonito flakes, a toothpick in one
    static GameObject Takoyaki()
    {
        var root = new GameObject("Takoyaki");
        // the boat: sections along x, flat bottom, flared walls, pointed and lifted at both ends; thin veneer, both sides
        const int N = 24;
        float L = .62f, wall = .13f;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int start = verts.Count;
            for (int i = 0; i <= N; i++)
            {
                float x = Mathf.Lerp(-L, L, (float)i / N), f = x / L, w = .33f * (1 - f * f), lift = .1f * f * f * f * f;
                var ring = new[] { new Vector3(x, wall + lift, -w), new Vector3(x, lift, -w * .72f), new Vector3(x, lift, w * .72f), new Vector3(x, wall + lift, w) };
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(ring[k]);
                    uvs.Add(new Vector2((float)i / N, k / 3f));
                }
            }
            for (int i = 0; i < N; i++)
                for (int k = 0; k < 3; k++)
                {
                    int a = start + i * 4 + k, b = a + 4;
                    tris.AddRange(side == 0 ? new[] { a, a + 1, b + 1, a, b + 1, b } : new[] { a, b + 1, a + 1, a, b, b + 1 });
                }
        }
        Part(root, Finish(verts, uvs, tris), Paint((u, v) => Color.Lerp(C("#f6d9a0"), C("#e6bf7e"), Mathf.PerlinNoise(u * 40, v * 3)), .25f), Vector3.zero);

        // the balls: golden, the sauce poured over their tops and running down in drips
        var ball = Paint((u, v) => v > .74f + .06f * Mathf.Sin(u * 2 * Mathf.PI * 6) ? C("#4a1e0c")
            : Color.Lerp(C("#b8702e"), C("#e3a85a"), Mathf.PerlinNoise(u * 12, v * 12)), .6f);
        for (int i = 0; i < 6; i++)
            Part(root, Sphere, ball, new((i % 3 - 1) * .26f, .165f, (i / 3 - .5f) * .26f), new(0, i * 60, 0), Vector3.one * .3f);
        // mayo zigzag over the top
        var mayo = Plain(Color.white, .5f);
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = new(-.36f + k * .18f, .315f, k % 2 == 0 ? -.13f : .13f), q = new(-.36f + (k + 1) * .18f, .315f, k % 2 == 0 ? .13f : -.13f);
            Part(root, Cube, mayo, (p + q) / 2, new(0, -Mathf.Atan2(q.z - p.z, q.x - p.x) * Mathf.Rad2Deg, 0), new((q - p).magnitude, .012f, .016f));
        }
        var bonito = Plain(C("#e3b98e"), .2f);
        var aonori = Plain(C("#3e7a2a"), .3f);
        for (int k = 0; k < 16; k++) // bonito flakes and seaweed sprinkled on top
        {
            var p = new Vector3((Hash(k, 1, 3) - .5f) * .7f, .3f, (Hash(k, 2, 3) - .5f) * .4f);
            if (k % 2 == 0) Part(root, Cube, bonito, p, new(Hash(k, 3, 3) * 50, Hash(k, 4, 3) * 180, 20), new(.08f, .006f, .06f));
            else Part(root, Sphere, aonori, p + new Vector3(0, .01f, 0), scale: Vector3.one * .03f);
        }
        Part(root, Cylinder, Plain(C("#efe2bf"), .3f), new(.33f, .34f, .12f), new(0, 0, -28), new(.014f, .12f, .014f)); // toothpick
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

    // gyoza: five pleated crescents fanned round a white plate, pale skin with a frilled seam on top, crisp golden-brown
    // undersides, flecks of spring onion; a little dish of soy sauce in the middle
    static GameObject Gyoza()
    {
        var root = new GameObject("Gyoza");
        Spin(root, P(0, 0, .46f, 0, .48f, .03f, .44f, .05f, 0, .04f), Plain(C("#fbfbf7"), .7f), scale: new(1.3f, 1, 1.3f));
        Spin(root, Torus(.45f, .016f), Plain(C("#2b4a8a"), .5f), new(0, .025f, 0), scale: new(1.3f, 1, 1.3f));
        var skin = Paint((u, v) =>
        {
            float s = Mathf.Sin(2 * Mathf.PI * u); // -1 underneath, 1 on top
            if (s < -.1f) return Color.Lerp(C("#d9994a"), C("#8f5320"), -s); // pan-fried crust
            if (s > .3f && Dot(u, v, 10, 30, .15f, .3f, 5, out _)) return C("#5aa83a"); // spring onion
            return C("#f3e3c2") * (Mathf.Repeat(v * 9, 1) < .15f ? .9f : 1f);
        }, .45f);
        float Radius(float t) => .17f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t)), .5f);
        Vector3 Spine(float t) => new(Mathf.Lerp(-.33f, .33f, t), .06f + Radius(t) * .65f, -.07f * Mathf.Sin(Mathf.PI * t));
        var body = Sweep(Spine, Radius, .75f);
        var seam = Sweep(t => Spine(t) + Vector3.up * Radius(t) * .68f, // the pleated frill along the top
            t => .035f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t)), .5f) * (1 + .6f * Mathf.Abs(Mathf.Sin(t * 28))), 1.6f);
        for (int k = 0; k < 5; k++)
        {
            float a = k * 72 + 18;
            var g = Tilted(root, new(0, a, 0), Quaternion.Euler(0, a, 0) * Vector3.back * .38f); // convex side outwards
            g.transform.localScale = Vector3.one * .78f;
            Part(g, body, skin, Vector3.zero);
            Part(g, seam, skin, Vector3.zero);
        }
        Spin(root, P(0, .04f, .1f, .04f, .14f, .13f, 0, .11f), Plain(Color.white, .7f)); // soy dish
        Part(root, Cylinder, Plain(C("#2a120a"), .9f), new(0, .115f, 0), scale: new(.22f, .004f, .22f));
        return root;
    }

    // soy sauce: the classic table dispenser, a glass bottle wide at the base and narrowing steadily to its neck,
    // near-black sauce up to the shoulder, a small white label; a flat-topped red cylinder cap with its little spout
    static GameObject SoySauce()
    {
        const float H = .8f, R = .27f, labelLo = .14f, labelHi = .3f, level = .6f;
        var cv = new Canvas(256, 256);
        cv.Paint((u, v) =>
        {
            float y = v * H;
            if (y > labelLo && y < labelHi) return Color.white;
            return y < level ? C("#2a120a") * (Sheen(u) * 1.4f) : C("#d4dde0") * Sheen(u); // sauce, then empty glass
        });
        LatheText(cv, "SOJA", 2 * Mathf.PI * R, H, (labelLo + labelHi) / 2, .1f, C("#d7192a"));
        var root = Spun("SOJA", P(0, 0, .24f, 0, .27f, .03f, .27f, .12f, .25f, .3f, .2f, .5f, .155f, .66f, .13f, .76f, .13f, H, 0, H), cv, .85f);
        var red = Plain(C("#d7192a"), .55f);
        Spin(root, P(0, .76f, .145f, .76f, .15f, .78f, .15f, .93f, .14f, .95f, 0, .95f), red); // the cap
        Part(root, Cylinder, red, new(.06f, .97f, -.08f), scale: new(.045f, .025f, .045f)); // spout at the front edge
        return root;
    }

    // yakitori (beef-wrapped cheese): three skewers lying diagonally on a round cream plate dotted with sauce, each a long
    // roll of thin beef lacquered dark and glossy in tare and sprinkled with sesame, closed at the back, a bulge of
    // golden melting cheese at the front where the stick comes out
    static GameObject Yakitori()
    {
        var root = new GameObject("Yakitori");
        Spin(root, P(0, 0, .46f, 0, .48f, .03f, .44f, .05f, 0, .04f), Plain(C("#efe8da"), .6f), scale: new(1.15f, 1, 1.15f));
        foreach (var (x, z, r) in new[] { (-.42f, -.3f, .05f), (.38f, -.36f, .035f), (.45f, .18f, .04f) }) // drips of tare on the plate
            Part(root, Sphere, Plain(C("#3e170a"), .9f), new(x, .045f, z), scale: new(r, .008f, r * .8f));
        var beef = Paint((u, v) =>
        {
            if (Dot(u, v, 10, 26, .35f, .22f, 4, out _)) return C("#f3e6c4"); // sesame
            return Mathf.PerlinNoise(u * 5, v * 8) < .3f ? C("#2a0e05") : Color.Lerp(C("#3e170a"), C("#74341a"), Mathf.PerlinNoise(u * 4, v * 20));
        }, .85f, 256);
        var roll = Sweep(t => new Vector3(.01f * Mathf.Sin(t * 9), 0, Mathf.Lerp(-.2f, .32f, t)),
            t => .11f * (.92f + .08f * Mathf.Sin(t * 23)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(t * Mathf.PI / 2)), .35f), .85f); // closed at the back
        var cheese = Paint((u, v) => Color.Lerp(C("#f2c23a"), C("#fbe39a"), Mathf.PerlinNoise(u * 6, v * 6)), .8f);
        var stick = Plain(C("#ecdcae"), .3f);
        var group = Tilted(root, new(0, 75, 0), Vector3.zero); // rolls across the view (which looks along local (.57, 0, -.82)), a little diagonal
        for (int i = 0; i < 3; i++)
        {
            var s = Tilted(group, new(0, (Hash(i, 1, 7) - .5f) * 10, 0), new(-.27f + i * .27f, .14f, (Hash(i, 2, 7) - .5f) * .08f));
            Part(s, roll, beef, Vector3.zero);
            Part(s, Sphere, cheese, new(0, -.005f, -.22f), scale: new(.24f, .19f, .2f)); // melting out of the front
            Part(s, Sphere, cheese, new(.02f, -.09f, -.28f), scale: new(.2f, .02f, .14f)); // and pooling on the plate
            Part(s, Cylinder, stick, new(0, 0, -.44f), new(90, 0, 0), new(.03f, .14f, .03f));
        }
        return root;
    }
}
