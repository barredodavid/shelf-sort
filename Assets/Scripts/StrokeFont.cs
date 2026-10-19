using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A tiny stroke font for product labels: each capital letter is a few polylines in a .6 x 1 box, drawn later
// as round, thick pen strokes (distance to the nearest segment), so text stays crisp at any size.
public static class StrokeFont
{
    public class Strokes
    {
        public readonly List<(Vector2 a, Vector2 b)> Segments = new();
        public float Width;

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

    const float Advance = .6f, Gap = .2f;

    public static Strokes Layout(string text)
    {
        var s = new Strokes();
        float x = 0;
        foreach (char ch in Plain(text))
        {
            if (Glyphs.TryGetValue(ch, out var lines))
                foreach (var line in lines)
                    for (int i = 0; i + 1 < line.Count; i++)
                        s.Segments.Add((line[i] + new Vector2(x, 0), line[i + 1] + new Vector2(x, 0)));
            x += Advance + Gap;
        }
        s.Width = Mathf.Max(0, x - Gap);
        return s;
    }

    // capitals without accents: the glyph set has none (É -> E, Ç -> C, ...)
    static string Plain(string text) => new(text.ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
        .ToArray());

    static List<Vector2> L(params float[] xy)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
        return pts;
    }

    static List<Vector2> Arc(float cx, float cy, float rx, float ry, float fromDeg, float toDeg, int n = 14)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (float)i / n);
            pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
        }
        return pts;
    }

    static List<Vector2> Join(params List<Vector2>[] parts) => parts.SelectMany(p => p).ToList();

    static readonly Dictionary<char, List<Vector2>[]> Glyphs = new()
    {
        ['A'] = new[] { L(.03f, .07f, .3f, .93f, .57f, .07f), L(.14f, .38f, .46f, .38f) },
        ['B'] = new[]
        {
            Join(L(.07f, .07f, .07f, .93f, .34f, .93f), Arc(.34f, .72f, .19f, .21f, 90, -90), L(.34f, .51f, .07f, .51f)),
            Join(L(.34f, .51f, .37f, .51f), Arc(.37f, .29f, .2f, .22f, 90, -90), L(.37f, .07f, .07f, .07f)),
        },
        ['C'] = new[] { Arc(.32f, .5f, .27f, .43f, 50, 310, 20) },
        ['D'] = new[] { Join(L(.07f, .07f, .07f, .93f), Arc(.07f, .5f, .47f, .43f, 90, -90, 20), L(.07f, .07f)) },
        ['E'] = new[] { L(.53f, .93f, .07f, .93f, .07f, .07f, .53f, .07f), L(.07f, .5f, .42f, .5f) },
        ['F'] = new[] { L(.53f, .93f, .07f, .93f, .07f, .07f), L(.07f, .5f, .42f, .5f) },
        ['G'] = new[] { Join(Arc(.32f, .5f, .27f, .43f, 50, 320, 20), L(.53f, .45f, .34f, .45f)) },
        ['H'] = new[] { L(.07f, .07f, .07f, .93f), L(.53f, .07f, .53f, .93f), L(.07f, .5f, .53f, .5f) },
        ['I'] = new[] { L(.3f, .07f, .3f, .93f), L(.14f, .93f, .46f, .93f), L(.14f, .07f, .46f, .07f) },
        ['J'] = new[] { Join(L(.52f, .93f, .52f, .3f), Arc(.31f, .3f, .21f, .23f, 0, -180)) },
        ['K'] = new[] { L(.07f, .07f, .07f, .93f), L(.53f, .93f, .07f, .4f), L(.22f, .57f, .55f, .07f) },
        ['L'] = new[] { L(.07f, .93f, .07f, .07f, .52f, .07f) },
        ['M'] = new[] { L(.05f, .07f, .08f, .93f, .3f, .4f, .52f, .93f, .55f, .07f) },
        ['N'] = new[] { L(.07f, .07f, .07f, .93f, .53f, .07f, .53f, .93f) },
        ['O'] = new[] { Arc(.3f, .5f, .27f, .43f, 0, 360, 28) },
        ['P'] = new[] { Join(L(.07f, .07f, .07f, .93f, .33f, .93f), Arc(.33f, .71f, .21f, .22f, 90, -90), L(.07f, .49f)) },
        ['Q'] = new[] { Arc(.3f, .5f, .27f, .43f, 0, 360, 28), L(.36f, .27f, .58f, .02f) },
        ['R'] = new[] { Join(L(.07f, .07f, .07f, .93f, .33f, .93f), Arc(.33f, .71f, .21f, .22f, 90, -90), L(.07f, .49f)), L(.3f, .49f, .55f, .07f) },
        ['S'] = new[] { Join(Arc(.3f, .715f, .25f, .215f, 25, 270), Arc(.3f, .285f, .25f, .215f, 90, -155)) },
        ['T'] = new[] { L(.03f, .93f, .57f, .93f), L(.3f, .93f, .3f, .07f) },
        ['U'] = new[] { Join(L(.07f, .93f, .07f, .35f), Arc(.3f, .35f, .23f, .28f, 180, 360), L(.53f, .93f)) },
        ['V'] = new[] { L(.03f, .93f, .3f, .07f, .57f, .93f) },
        ['W'] = new[] { L(.02f, .93f, .15f, .07f, .3f, .6f, .45f, .07f, .58f, .93f) },
        ['X'] = new[] { L(.05f, .93f, .55f, .07f), L(.05f, .07f, .55f, .93f) },
        ['Y'] = new[] { L(.03f, .93f, .3f, .5f, .57f, .93f), L(.3f, .5f, .3f, .07f) },
        ['Z'] = new[] { L(.05f, .93f, .55f, .93f, .05f, .07f, .55f, .07f) },
        ['?'] = new[] { Join(Arc(.3f, .7f, .24f, .22f, 160, -90), L(.3f, .3f)), L(.3f, .07f, .3f, .08f) },
    };
}
