using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Juice without assets: synthesized sounds, confetti bursts, tweens, rounded UI texture.
public static class Fx
{
    static AudioSource src;
    static readonly Dictionary<string, AudioClip> clips = new();
    static Material confettiMat;

    public static void Init(GameObject host)
    {
        src = host.AddComponent<AudioSource>();
        clips["pick"] = Make(.07f, (t, p) => Mathf.Lerp(700, 950, p), .22f);
        clips["drop"] = Make(.1f, (t, p) => Mathf.Lerp(320, 180, p), .35f);
        clips["back"] = Make(.12f, (t, p) => Mathf.Lerp(260, 200, p), .2f);
        clips["match"] = Arp(new[] { 523f, 659f, 784f }, .06f, .3f);
        clips["win"] = Arp(new[] { 523f, 659f, 784f, 1047f, 1319f }, .1f, .3f);
        clips["lose"] = Arp(new[] { 392f, 330f, 262f, 196f }, .15f, .3f);
        confettiMat = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultParticleMaterial;
    }

    public static void Play(string name, float pitch = 1)
    {
        src.pitch = pitch;
        src.PlayOneShot(clips[name]);
    }

    static AudioClip Arp(float[] notes, float step, float vol) =>
        Make(step * notes.Length + .15f, (t, p) => notes[Mathf.Min((int)(t / step), notes.Length - 1)], vol, step, notes.Length);

    // Sine + octave, plucked envelope (re-triggered on each of the `notes` steps for arpeggios)
    static AudioClip Make(float dur, System.Func<float, float, float> freq, float vol, float retrig = 0, int notes = 1)
    {
        const int sr = 44100;
        int n = (int)(dur * sr);
        var d = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr, p = (float)i / n;
            phase += 2 * System.Math.PI * freq(t, p) / sr;
            float local = retrig > 0 ? t - retrig * Mathf.Min(Mathf.Floor(t / retrig), notes - 1) : t;
            float env = Mathf.Exp(-local * 22) * Mathf.Clamp01(local * 500);
            d[i] = (float)(System.Math.Sin(phase) + .3 * System.Math.Sin(2 * phase)) * env * vol;
        }
        var c = AudioClip.Create("fx", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    public static void Burst(Vector3 pos, int count = 20)
    {
        var go = new GameObject("Burst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.duration = .5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4, 8);
        main.startSize = new ParticleSystem.MinMaxCurve(.12f, .25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.gravityModifier = 1.5f;
        var g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, .76f, .24f), 0), new GradientColorKey(new Color(.95f, .35f, .5f), .33f),
            new GradientColorKey(new Color(.35f, .75f, 1f), .66f), new GradientColorKey(new Color(.45f, .85f, .4f), 1),
        }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0, count) }); // SetBurst(0, ..) needs an existing burst
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = .3f;
        go.GetComponent<ParticleSystemRenderer>().material = confettiMat;
        ps.Play();
    }

    // ---------- tweens (run with StartCoroutine; stop quietly if the object was destroyed) ----------

    // Squash-and-stretch around the rest scale (1): starting from the current scale would compound when taps
    // overlap and leave the object deformed.
    public static IEnumerator Squash(Transform t, float dur = .3f)
    {
        var s0 = Vector3.one;
        for (float e = 0; e < dur; e += Time.deltaTime)
        {
            if (!t) yield break;
            float k = e / dur, a = Mathf.Sin(k * Mathf.PI * 2) * (1 - k) * .3f;
            t.localScale = new Vector3(s0.x * (1 + a), s0.y * (1 - a), s0.z * (1 + a));
            yield return null;
        }
        if (t) t.localScale = s0;
    }

    public static IEnumerator Move(Transform t, Vector3 from, Vector3 to, Vector3 s0, Vector3 s1, float dur)
    {
        for (float e = 0; e < dur; e += Time.deltaTime)
        {
            if (!t) yield break;
            float k = 1 - Mathf.Pow(1 - e / dur, 3); // ease-out cubic
            t.position = Vector3.LerpUnclamped(from, to, k);
            t.localScale = Vector3.LerpUnclamped(s0, s1, k);
            yield return null;
        }
        if (t) { t.position = to; t.localScale = s1; }
    }

    public static IEnumerator Shake(Transform cam, Vector3 home, float amp = .18f, float dur = .25f)
    {
        for (float e = 0; e < dur; e += Time.deltaTime)
        {
            cam.position = home + (Vector3)Random.insideUnitCircle * amp * (1 - e / dur);
            yield return null;
        }
        cam.position = home;
    }

    // Stylized ice face: clear blue core, frosted white border (noisy), two diagonal glints and a few cracks.
    // Alpha carries the effect, so the material must be transparent.
    public static Texture2D IceTexture(int size = 128, int seed = 7)
    {
        var rng = new System.Random(seed);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)); // 0 at the border, .5 in the middle
                float noise = Mathf.PerlinNoise(u * 9 + seed, v * 9);
                float frost = Mathf.Clamp01(1 - (edge - .03f) / (.13f + .07f * noise)); // ragged frosted rim
                var c = new Color(.72f, .9f, 1f, .32f);
                c = Color.Lerp(c, new Color(.97f, .99f, 1f, .9f), frost);
                // two glints across the face, upper left
                float d = u + (1 - v);
                if (Mathf.Abs(d - .62f) < .05f || Mathf.Abs(d - .8f) < .022f)
                    c = Color.Lerp(c, new Color(1, 1, 1, .85f), .85f * Mathf.Clamp01((1.25f - d) * 3));
                px[y * size + x] = c;
            }

        // cracks: short jagged white lines from the rim inward
        for (int k = 0; k < 3; k++)
        {
            Vector2 p = new(rng.Next(size), rng.Next(2) == 0 ? 2 : size - 3);
            float a = (float)rng.NextDouble() * Mathf.PI;
            for (int step = 0; step < size / 3; step++)
            {
                a += ((float)rng.NextDouble() - .5f) * .9f;
                p += new Vector2(Mathf.Cos(a), p.y < size / 2 ? Mathf.Abs(Mathf.Sin(a)) : -Mathf.Abs(Mathf.Sin(a)));
                int cx = Mathf.Clamp((int)p.x, 0, size - 1), cy = Mathf.Clamp((int)p.y, 0, size - 1);
                px[cy * size + cx] = new Color(1, 1, 1, .95f);
                if (cx + 1 < size) px[cy * size + cx + 1] = Color.Lerp(px[cy * size + cx + 1], new Color(1, 1, 1, .9f), .6f);
            }
        }

        var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // White five-pointed star with soft edges (4x4 supersampling), tinted with GUI.color
    public static Texture2D Star(int size)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2 + i * Mathf.PI / 5, r = (i % 2 == 0 ? .5f : .21f) * size;
            pts[i] = new Vector2(size / 2f + Mathf.Cos(a) * r, size / 2f + Mathf.Sin(a) * r * 1.02f - size * .03f);
        }
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                        if (Inside(pts, new Vector2(x + (sx + .5f) / 4, y + (sy + .5f) / 4))) inside++;
                t.SetPixel(x, y, new Color(1, 1, 1, inside / 16f));
            }
        t.Apply();
        return t;
    }

    // even-odd point-in-polygon
    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                c = !c;
        return c;
    }

    // White rounded square; used as a 9-sliced IMGUI background, tinted with GUI.backgroundColor
    // Icons, white on transparent: 0 undo arrow, 1 shuffle arrows, 2 stopwatch, 3 plane, 4 speaker, 5 speaker muted. Round pen strokes
    // (distance to segments, like StrokeFont) plus triangular arrowheads, in a 0..1 box with y up.
    public static Texture2D Icon(int kind, int size = 128)
    {
        var s = new StrokeFont.Strokes();
        var heads = new List<Vector2[]>();
        void Line(params Vector2[] p) { for (int i = 0; i + 1 < p.Length; i++) s.Segments.Add((p[i], p[i + 1])); }
        void Arc(Vector2 c, float r, float from, float to)
        {
            for (int i = 0; i < 16; i++)
            {
                float a0 = Mathf.Deg2Rad * Mathf.Lerp(from, to, i / 16f), a1 = Mathf.Deg2Rad * Mathf.Lerp(from, to, (i + 1) / 16f);
                s.Segments.Add((c + r * new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), c + r * new Vector2(Mathf.Cos(a1), Mathf.Sin(a1))));
            }
        }
        void Head(Vector2 tip, Vector2 dir) // filled arrowhead pointing along dir
        {
            dir.Normalize();
            var n = new Vector2(-dir.y, dir.x);
            heads.Add(new[] { tip, tip - dir * .2f + n * .14f, tip - dir * .2f - n * .14f });
        }

        switch (kind)
        {
            case 0: // ↶ : arrow pointing left, looping round on the right
                Line(new(.3f, .66f), new(.58f, .66f));
                Arc(new(.58f, .45f), .21f, 90, -90);
                Line(new(.58f, .24f), new(.3f, .24f));
                Head(new(.1f, .66f), Vector2.left);
                break;
            case 1: // ⇄ crossed : two arrows swapping lanes
                Line(new(.12f, .28f), new(.32f, .28f), new(.6f, .72f), new(.72f, .72f));
                Line(new(.12f, .72f), new(.32f, .72f), new(.6f, .28f), new(.72f, .28f));
                Head(new(.92f, .72f), Vector2.right);
                Head(new(.92f, .28f), Vector2.right);
                break;
            case 3: // airliner seen from above, nose up: fuselage, swept wings, tail
                Line(new(.5f, .12f), new(.5f, .86f));
                Line(new(.5f, .86f), new(.5f, .9f));
                heads.Add(new Vector2[] { new(.5f, .66f), new(.06f, .4f), new(.5f, .5f) });
                heads.Add(new Vector2[] { new(.5f, .66f), new(.94f, .4f), new(.5f, .5f) });
                heads.Add(new Vector2[] { new(.5f, .24f), new(.3f, .1f), new(.5f, .16f) });
                heads.Add(new Vector2[] { new(.5f, .24f), new(.7f, .1f), new(.5f, .16f) });
                break;
            case 4: // speaker with sound waves
            case 5: // speaker, crossed out
                heads.Add(new Vector2[] { new(.1f, .38f), new(.28f, .38f), new(.28f, .62f) }); // the box
                heads.Add(new Vector2[] { new(.1f, .38f), new(.28f, .62f), new(.1f, .62f) });
                heads.Add(new Vector2[] { new(.22f, .5f), new(.52f, .2f), new(.52f, .8f) }); // the cone
                if (kind == 4)
                {
                    Arc(new(.5f, .5f), .18f, -45, 45);
                    Arc(new(.5f, .5f), .34f, -45, 45);
                }
                else
                {
                    Line(new(.64f, .36f), new(.9f, .64f));
                    Line(new(.64f, .64f), new(.9f, .36f));
                }
                break;
            default: // stopwatch
                Arc(new(.5f, .42f), .32f, 0, 360);
                Line(new(.5f, .42f), new(.5f, .6f));
                Line(new(.5f, .42f), new(.63f, .35f));
                Line(new(.5f, .74f), new(.5f, .84f));
                Line(new(.4f, .88f), new(.6f, .88f));
                break;
        }

        float half = kind == 3 ? .075f : .055f; // half the pen width (a stouter fuselage for the plane)
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) / size, (y + .5f) / size);
                float a = Mathf.Clamp01((half - s.Distance(p)) * size + .5f);
                if (heads.Any(h => Inside(h, p))) a = 1;
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        t.Apply();
        return t;
    }

    public static Texture2D Rounded(int size, int radius)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius), cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + .5f)));
            }
        t.Apply();
        return t;
    }
}
