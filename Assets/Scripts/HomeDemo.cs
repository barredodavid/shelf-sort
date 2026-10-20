using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Title screen demo: a little shelf of three compartments playing the game by itself, forever, so a new player
// sees the rule at once. Each round has the same shape, the compartments' roles rotating:
//   p = [x, x, _]   q = [y, x, y]   r = [z, z, y]
// q's x goes to p (trio of x pops), then r's y goes to q (trio of y pops), leaving r = [z, z, _]. The two emptied
// compartments are restocked as q' = [y', z, y'] and r' = [w, w, y'], and r becomes the next p.
// Products are drawn from all four countries.
public partial class Game
{
    Transform heroRoot;
    Coroutine homeDemo;
    readonly GameObject[,] demo = new GameObject[3, 3];
    readonly Dictionary<(int, int), GameObject> demoTemplates = new(); // built once, cloned after

    static readonly float[] DemoX = { -2.45f, 0, 2.45f };
    const float DemoScale = .72f;

    static Vector3 DemoSlot(int c, int i) => new(DemoX[c] + (i - 1) * .76f, 0, 0);

    float HeroWorldY()
    {
        float px = heroCenterPx > 0 ? heroCenterPx : Screen.height / GuiScale * .55f;
        return OnBoard(new Vector2(Screen.width / 2f, Screen.height - px * GuiScale)).y - .55f; // shelf under the products' middle
    }

    void RenderHome()
    {
        heroRoot = new GameObject("Demo").transform;
        heroRoot.SetParent(root);
        // the same shelving as in a level: back panel, plank with its price strip and tags, posts, top plank
        void Part(Vector3 pos, Vector3 size, Color c) => Box(pos, size, c).transform.SetParent(heroRoot);
        Part(new Vector3(0, .62f, .7f), new Vector3(7.6f, 1.5f, .1f), BackboardC);
        Part(new Vector3(0, -.11f, 0), new Vector3(7.6f, .22f, 1.4f), WoodC);
        Part(new Vector3(0, -.13f, -.71f), new Vector3(7.6f, .16f, .02f), Cream);
        for (int c = 0; c < 3; c++)
            for (int i = 0; i < 3; i++)
                Part(DemoSlot(c, i) + new Vector3(0, -.13f, -.73f), new Vector3(.3f, .1f, .02f), TagC);
        foreach (float x in new[] { -3.7f, -1.225f, 1.225f, 3.7f }) // posts between the compartments
            Part(new Vector3(x, .62f, 0), new Vector3(.16f, 1.5f, 1.4f), WoodC);
        Part(new Vector3(0, 1.42f, 0), new Vector3(7.8f, .2f, 1.5f), WoodC * .85f);
        AnimateHome();
        homeDemo = StartCoroutine(HomeDemo());
    }

    void AnimateHome()
    {
        if (menu != Menu.Home || !heroRoot) return;
        heroRoot.position = new Vector3(0, HeroWorldY(), 0);
    }

    (int, int) DemoProduct(params (int, int)[] not)
    {
        while (true)
        {
            var p = (Random.Range(0, Worlds.Length), Random.Range(0, Products.Count));
            if (System.Array.IndexOf(not, p) < 0) return p;
        }
    }

    GameObject DemoItem((int w, int id) p, Vector3 local)
    {
        if (!demoTemplates.TryGetValue(p, out var t))
        {
            t = Products.Make(p.w, p.id);
            t.SetActive(false);
            demoTemplates[p] = t;
        }
        var m = Instantiate(t);
        m.SetActive(true);
        var go = Wrap(m, Vector3.zero, DemoScale, "demo", null);
        go.transform.SetParent(heroRoot, false);
        go.transform.localPosition = local;
        return go;
    }

    // a compartment restocked: its three products slide forward from the back
    IEnumerator DemoFill(int c, params (int, int)[] items)
    {
        for (int i = 0; i < 3; i++) demo[c, i] = DemoItem(items[i], DemoSlot(c, i) + new Vector3(0, .2f, .7f));
        for (float t = 0; t < 1; t += Time.deltaTime / .3f)
        {
            for (int i = 0; i < 3; i++)
            {
                demo[c, i].transform.localPosition = Vector3.Lerp(DemoSlot(c, i) + new Vector3(0, .2f, .7f), DemoSlot(c, i), t);
                demo[c, i].transform.localScale = Vector3.one * DemoScale * Mathf.Lerp(.7f, 1, t);
            }
            yield return null;
        }
        for (int i = 0; i < 3; i++)
        {
            demo[c, i].transform.localPosition = DemoSlot(c, i);
            demo[c, i].transform.localScale = Vector3.one * DemoScale;
        }
    }

    // a product lifted and carried in an arc to an empty slot
    IEnumerator DemoMove(int fc, int fi, int tc, int ti)
    {
        var go = demo[fc, fi];
        demo[tc, ti] = go;
        demo[fc, fi] = null;
        Vector3 a = DemoSlot(fc, fi), b = DemoSlot(tc, ti);
        for (float t = 0; t < 1; t += Time.deltaTime / .6f)
        {
            float e = Mathf.SmoothStep(0, 1, t);
            go.transform.localPosition = Vector3.Lerp(a, b, e) + new Vector3(0, .4f * Mathf.Sin(Mathf.PI * e), -.8f * Mathf.Sin(Mathf.PI * e)); // low and in front: under the top plank
            go.transform.localScale = Vector3.one * DemoScale * (1 + .15f * Mathf.Sin(Mathf.PI * e));
            yield return null;
        }
        go.transform.localPosition = b;
        go.transform.localScale = Vector3.one * DemoScale;
    }

    // a full trio swells and vanishes in confetti
    IEnumerator DemoPop(int c)
    {
        yield return new WaitForSeconds(.12f);
        for (float t = 0; t < .3f; t += Time.deltaTime)
        {
            float k = t < .12f ? Mathf.Lerp(1, 1.3f, t / .12f) : Mathf.Lerp(1.3f, 0, (t - .12f) / .18f);
            for (int i = 0; i < 3; i++) demo[c, i].transform.localScale = Vector3.one * DemoScale * k;
            yield return null;
        }
        Fx.Burst(heroRoot.TransformPoint(DemoSlot(c, 1) + new Vector3(0, .5f, -1)), 18);
        for (int i = 0; i < 3; i++)
        {
            Destroy(demo[c, i]);
            demo[c, i] = null;
        }
    }

    IEnumerator HomeDemo()
    {
        var x = DemoProduct();
        var y = DemoProduct(x);
        var z = DemoProduct(x, y);
        int p = 0, q = 1, r = 2;
        for (int i = 0; i < 3; i++) // the first round appears in place
        {
            demo[p, i] = i < 2 ? DemoItem(x, DemoSlot(p, i)) : null;
            demo[q, i] = DemoItem(i == 1 ? x : y, DemoSlot(q, i));
            demo[r, i] = DemoItem(i < 2 ? z : y, DemoSlot(r, i));
        }
        while (true)
        {
            yield return new WaitForSeconds(.8f);
            yield return DemoMove(q, 1, p, 2);
            yield return DemoPop(p);
            yield return new WaitForSeconds(.4f);
            yield return DemoMove(r, 2, q, 1);
            yield return DemoPop(q);
            yield return new WaitForSeconds(.4f);

            // restock: r = [z, z, _] becomes the next p
            var y2 = DemoProduct(z);
            var w2 = DemoProduct(z, y2);
            int np = r, nq = p, nr = q;
            yield return DemoFill(nq, y2, z, y2);
            yield return DemoFill(nr, w2, w2, y2);
            (p, q, r) = (np, nq, nr);
            z = w2;
        }
    }
}
