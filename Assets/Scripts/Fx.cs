using System.Collections;
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
        em.SetBurst(0, new ParticleSystem.Burst(0, count));
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = .3f;
        go.GetComponent<ParticleSystemRenderer>().material = confettiMat;
        ps.Play();
    }

    // ---------- tweens (run with StartCoroutine; stop quietly if the object was destroyed) ----------

    public static IEnumerator Squash(Transform t, float dur = .3f)
    {
        var s0 = t.localScale;
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

    // White rounded square; used as a 9-sliced IMGUI background, tinted with GUI.backgroundColor
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
