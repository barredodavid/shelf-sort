using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Things that happen during a level, on top of the puzzle. None of them can make a level unsolvable: they hold
// something back for a while, pay a bonus, or (the pigeon) make an ordinary move the player can undo.
//  - One twist per country, from its 6th level:
//    France, the pigeon: lands on a product, which can't be taken until the pigeon is shooed off with a tap (a few
//    coins and a second); left there too long, it flies off with the product and drops it on another shelf;
//    Italy, the siesta: a shelf's steel shutter comes half down as a warning (a trio made there then pays), then shuts
//    it for a few seconds (nothing taken from it or put on it);
//    Japan, locked products: one or two products stand tied up under a padlock that opens after 3 moves (seconds when
//    it opens);
//    USA, the flash sale: one product wears a blinking SALE tag for a few seconds, and its trio pays a bonus.
public partial class Game
{
    const float PigeonTime = 4, PigeonFlight = 1.1f, SiestaTime = 4, SaleTime = 10;
    const int BentoMoves = 3, PigeonCoins = 3, BentoSeconds = 2;
    const int SaleSeconds = 5, SaleMissed = 5, SiestaSeconds = 3; // the sale and the siesta play on the clock: won or lost seconds
    const float SiestaWarning = 1.6f;

    int twist = -1; // the country whose twist runs in this level (-1: none)
    int saleId = -1, shutS = -1, shutWarn = -1, shutUp = -1; // shut; about to shut; opening again
    float saleUntil, nextSale, shutFrom, shutUntil, shutUpAt, nextShut, pigeonUntil, nextPigeon, pigeonLandAt;
    Transform shutter3D; // the twists' models over the board, rebuilt if Render clears it
    readonly Dictionary<(int s, int i), Transform> bentos3D = new(), tags3D = new();
    (int s, int i) pigeon = (-1, -1);
    Transform pigeon3D; // its model, standing on the product (rebuilt if Render clears the board)
    readonly Dictionary<(int s, int i), int> seals = new(); // sealed bentos: moves left before they open

    // called at the end of StartLevel
    void StartEvents()
    {
        twist = !daily && lvl >= 5 ? world : -1;
        saleId = shutS = shutWarn = shutUp = -1;
        pigeon = (-1, -1);
        seals.Clear();
        bentos3D.Clear(); // last level's models went with the board
        tags3D.Clear();
        float t = Time.time;
        nextPigeon = t + 8;
        nextShut = t + 12;
        nextSale = t + 9;
        if (twist == 2) // seal one or two front products, never a frozen one or a carton
        {
            var free = FrontSlots().Where(c => LevelGen.Movable(shelves[c.s][0][c.i]) && !LevelGen.IsBoxed(shelves[c.s][0][c.i])).ToList();
            foreach (var c in free.OrderBy(_ => rng.Next()).Take(1 + rng.Next(2))) seals[c] = BentoMoves;
        }
    }

    IEnumerable<(int s, int i)> FrontSlots() => Enumerable.Range(0, shelves.Count).SelectMany(s => Enumerable.Range(0, 3).Select(i => (s, i)));

    // the products still on the board, one entry per kind with at least a trio left (any row)
    List<int> KindsLeft() => shelves.SelectMany(sh => sh).SelectMany(r => r).Where(LevelGen.IsProduct).GroupBy(LevelGen.Id)
        .Where(g => g.Count() >= 3).Select(g => g.Key).ToList();

    // called by Update while the level runs
    void UpdateEvents()
    {
        float t = Time.time;
        switch (twist)
        {
            case 0: // the pigeon lands on a front product, and flies off on its own after a while
                if (pigeon.s >= 0 && !LevelGen.IsProduct(shelves[pigeon.s][0][pigeon.i])) ShooPigeon(false);
                else if (pigeon.s >= 0 && t > pigeonUntil) StealProduct();
                else if (pigeon.s < 0 && t > nextPigeon)
                {
                    var spots = FrontSlots().Where(c => LevelGen.Movable(shelves[c.s][0][c.i]) && !locked.Contains(c.s) && c != (dragS, dragI)).ToList();
                    if (spots.Count > 0)
                    {
                        pigeon = spots[rng.Next(spots.Count)];
                        pigeonLandAt = t;
                        pigeonUntil = t + PigeonFlight + PigeonTime; // its stay counts from the landing
                        Fx.Play("drop", 1.6f);
                    }
                }
                break;
            case 1: // siesta: a shelf's shutter comes half down (a warning), then shuts it for a few seconds, then rolls up
                if (shutWarn >= 0 && t > shutFrom)
                {
                    shutS = shutWarn;
                    shutWarn = -1;
                    shutUntil = t + SiestaTime;
                    Toast("RIPOSO !");
                    Fx.Play("back", .8f);
                    Fx.Haptic(2);
                }
                else if (shutS >= 0 && t > shutUntil)
                {
                    shutUp = shutS;
                    shutUpAt = t;
                    shutS = -1;
                    nextShut = t + 14;
                    Fx.Play("pick", .8f);
                }
                else if (shutS < 0 && shutWarn < 0 && t > nextShut)
                {
                    var open = Enumerable.Range(0, shelves.Count).Where(s => !locked.Contains(s) && s != dragS && shelves[s][0].Any(x => x >= 0)).ToList();
                    if (open.Count > 0)
                    {
                        shutWarn = open[rng.Next(open.Count)];
                        shutFrom = t + SiestaWarning;
                        Toast("Ça va fermer !");
                        Fx.Play("back");
                    }
                }
                break;
            case 3: // flash sale on one product still on the board
                if (saleId >= 0 && t > saleUntil) // missed: the shoppers rush in, and the clock pays
                {
                    var from = tags3D.Values.FirstOrDefault(tag => tag)?.position ?? new Vector3(0, boardY, 0);
                    saleId = -1;
                    nextSale = t + 12;
                    time = Mathf.Max(0, time - SaleMissed);
                    Toast(Tf("PROMO RATÉE ! -{0} s", SaleMissed));
                    Fx.Play("back", .7f);
                    Fx.Haptic(2);
                    Fly(from, clockTarget, $"-{SaleMissed} s");
                }
                else if (saleId < 0 && t > nextSale)
                {
                    var kinds = KindsLeft();
                    if (kinds.Count > 0)
                    {
                        saleId = kinds[rng.Next(kinds.Count)];
                        saleUntil = t + SaleTime;
                        Toast("PROMO FLASH !");
                        Fx.Play("match", 1.5f);
                    }
                }
                break;
        }
        if (twist == 0) PlacePigeon();
        if (twist == 1) PlaceShutter();
        if (twist == 2) PlaceBentos();
        if (twist == 3) PlaceTags();
    }

    // the shutter of the shelf in siesta: half down while it warns, slammed down when it shuts, rolled up after
    void PlaceShutter()
    {
        int s = shutS >= 0 ? shutS : shutWarn >= 0 ? shutWarn : shutUp;
        float t = Time.time, down = shutS >= 0 ? Mathf.Lerp(.55f, 1, Mathf.SmoothStep(0, 1, (t - shutFrom) / .45f))
            : shutWarn >= 0 ? .55f * Mathf.SmoothStep(0, 1, (t - shutFrom + SiestaWarning) / .5f)
            : 1 - Mathf.Clamp01((t - shutUpAt) / .5f);
        if (s < 0 || down <= 0)
        {
            if (shutter3D) Destroy(shutter3D.gameObject);
            shutUp = -1;
            return;
        }
        float height = rowH - .42f; // from the plank top to just under the shelf above, the roller box tucked beneath it
        if (!shutter3D)
        {
            shutter3D = Products.Shutter(ShelfW, height).transform;
            shutter3D.SetParent(root);
        }
        shutter3D.position = ShelfPos(s) + new Vector3(0, .11f + height, -.86f);
        PoseShutter(shutter3D, height, down, t);
    }

    // a shutter `height` tall, `down` (0..1) of the way down: the curtain and its bar; the sign, hung on the curtain just
    // above the bar, only shows once there is curtain enough to carry it (growing in as it comes), swinging
    static void PoseShutter(Transform shutter, float height, float down, float t)
    {
        shutter.Find("curtain").localScale = new Vector3(1, Mathf.Max(.01f, height * down), 1);
        var bar = shutter.Find("bar");
        bar.localPosition = new Vector3(0, -height * down, 0);
        var sign = bar.Find("sign");
        float room = Mathf.Clamp01((height * down - .85f) / .25f); // the sign is .85 tall from its strings to its bottom
        sign.localScale = Vector3.one * Mathf.Max(.001f, room);
        sign.localPosition = new Vector3(0, .84f * room, -.14f);
        sign.localRotation = Quaternion.Euler(0, 0, 8 * Mathf.Sin(t * 3));
    }

    // a padlock hanging before each locked product, rattling a little more as the moves run out
    void PlaceBentos()
    {
        foreach (var c in bentos3D.Keys.Where(c => !seals.ContainsKey(c)).ToList()) // opened, or popped with a trio
        {
            if (bentos3D[c]) StartCoroutine(OpenBento(bentos3D[c]));
            bentos3D.Remove(c);
        }
        foreach (var (c, n) in seals)
        {
            if (!bentos3D.TryGetValue(c, out var box) || !box)
            {
                bentos3D[c] = box = Products.Padlock().transform;
                box.SetParent(root);
                box.SetPositionAndRotation(SlotPos(c.s, c.i) + new Vector3(0, 0, -.05f), Quaternion.identity);
            }
            float tall = views.TryGetValue(c, out var v) ? Mathf.Max(.4f, TopOf(v.transform) - SlotPos(c.s, c.i).y + .06f) : 1.2f;
            Products.TieLock(box, tall); // hung at the product's middle
            box.Find("lock").localRotation = Quaternion.Euler(0, 0, 4 * (BentoMoves - n) * Mathf.Sin(Time.time * 9)); // rattling
        }
    }

    // the top of a model, in world space
    static float TopOf(Transform t) => t.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y);

    // opened: the shackle springs up, then the padlock drops off, spinning
    IEnumerator OpenBento(Transform box)
    {
        var lockT = box.Find("lock");
        for (float t = 0; t < .6f && box; t += Time.deltaTime)
        {
            lockT.Find("shackle").localPosition = new Vector3(0, Mathf.Min(t, .1f), 0); // the shackle springs open
            if (t > .12f) lockT.localPosition += new Vector3(0, -4 * t, -1.5f) * Time.deltaTime; // then it drops off, spinning
            if (t > .12f) lockT.localRotation = Quaternion.Euler(0, 0, 500 * (t - .12f));
            yield return null;
        }
        if (box) Destroy(box.gameObject);
    }

    // a SALE tag hanging over each front product on sale, swaying, its card blinking
    void PlaceTags()
    {
        var want = FrontSlots().Where(c => LevelGen.IsProduct(shelves[c.s][0][c.i]) && !LevelGen.IsBoxed(shelves[c.s][0][c.i])
            && LevelGen.Id(shelves[c.s][0][c.i]) == saleId).ToList();
        foreach (var c in tags3D.Keys.Where(c => !want.Contains(c)).ToList())
        {
            if (tags3D[c]) Destroy(tags3D[c].gameObject);
            tags3D.Remove(c);
        }
        float blink = Mathf.Repeat(Time.time * 2.5f, 1) < .5f ? .9f : .2f;
        foreach (var c in want)
        {
            if (!tags3D.TryGetValue(c, out var tag) || !tag)
            {
                tags3D[c] = tag = Products.SaleTag().transform;
                tag.SetParent(root);
            }
            float top = views.TryGetValue(c, out var v) ? TopOf(v.transform) : SlotPos(c.s, c.i).y + 1.2f; // hung just over the product
            tag.SetPositionAndRotation(new Vector3(SlotPos(c.s, c.i).x + .25f, top + .45f, SlotPos(c.s, c.i).z - .45f), Quaternion.Euler(0, 0, 10 * Mathf.Sin(Time.time * 2.4f + c.i)));
            tag.Find("card").GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", new Color(1, .8f, .1f) * blink);
        }
    }

    // the pigeon's model on top of its product, pecking now and then
    void PlacePigeon()
    {
        if (pigeon.s < 0 || !views.TryGetValue(pigeon, out var view)) return;
        if (!pigeon3D)
        {
            pigeon3D = Products.Pigeon().transform;
            pigeon3D.SetParent(root);
            pigeon3D.localScale = Vector3.one * .9f;
        }
        var b = view.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        var perch = new Vector3(b.center.x, b.max.y - .05f, b.center.z);
        float flying = PigeonFlight - (Time.time - pigeonLandAt);
        if (flying > 0) // still coming in: the way it leaves, played backwards
        {
            var side = FlightSide(perch);
            pigeon3D.SetPositionAndRotation(Flight(perch, side, flying), Quaternion.LookRotation(Vector3.ProjectOnPlane(side - cam.transform.forward, Vector3.up))); // head (-z) the way it flies
            pigeon3D.localScale = Vector3.one * .9f * (1 + flying);
            Flap(pigeon3D);
            return;
        }
        pigeon3D.localScale = Vector3.one * .9f;
        pigeon3D.SetPositionAndRotation(perch, Quaternion.Euler(0, 25 + 30 * Mathf.Sin(Time.time * .7f), 0)); // three-quarter view, its head (-z) to the camera
        var head = pigeon3D.Find("head");
        if (head) head.localRotation = Quaternion.Euler(-35 * Mathf.Max(0, Mathf.Sin(Time.time * 7)), 0, 0); // pecking: beak (-z) down
        if (pigeonUntil - Time.time < 1.5f) // about to take off with it: it flaps and hops, a warning
        {
            Flap(pigeon3D);
            pigeon3D.position += Vector3.up * .08f * Mathf.Abs(Mathf.Sin(Time.time * 14));
        }
        else foreach (Transform part in pigeon3D)
                if (part.name == "wing") part.localRotation = Quaternion.Euler(0, 0, Mathf.Sign(part.localPosition.x) * 12);
    }

    // shooed: it flies off along Flight until it leaves the screen, flapping, growing as it nears the lens
    IEnumerator FlyAway(Transform bird)
    {
        var start = bird.position;
        var side = FlightSide(start);
        bird.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.transform.forward - side, Vector3.up)); // its head (-z) towards the camera and the edge
        var size = bird.localScale;
        for (float t = 0; t < 2.5f && bird; t += Time.deltaTime)
        {
            bird.position = Flight(start, side, t);
            bird.localScale = size * (1 + t);
            Flap(bird);
            var v = cam.WorldToViewportPoint(bird.position);
            if (t > .3f && (v.x < -.15f || v.x > 1.15f || v.y > 1.15f)) break; // off screen
            yield return null;
        }
        if (bird) Destroy(bird.gameObject);
    }

    // its flight path, t seconds from its perch: out towards the camera (clear of the shelves), then up and off to
    // the side, faster and faster. Leaving it runs forwards, landing backwards.
    Vector3 Flight(Vector3 perch, Vector3 side, float t) => perch - cam.transform.forward * Mathf.Min(t * 6, 2.5f) + (side * 5 + Vector3.up * 3) * t * t;
    Vector3 FlightSide(Vector3 perch) => Mathf.Sign(perch.x - cam.transform.position.x + .01f) * cam.transform.right; // the nearest screen edge

    void ShooPigeon(bool tapped)
    {
        int s = pigeon.s;
        pigeon = (-1, -1);
        if (pigeon3D) StartCoroutine(FlyAway(pigeon3D));
        pigeon3D = null;
        nextPigeon = Time.time + (tapped ? 7 : 10);
        if (tapped)
        {
            Fx.Play("drop", 2f);
            Reward(s, PigeonCoins, 1, Tf("Ouste ! +{0}", PigeonCoins));
            Count("pigeons");
        }
    }

    // left too long, the pigeon flies off with its product and drops it in an empty slot of another shelf: an
    // ordinary move (undo takes it back, it doesn't count in the moves). No room anywhere: it just flies off.
    void StealProduct()
    {
        var (s, i) = pigeon;
        var spots = FrontSlots().Where(c => c.s != s && shelves[c.s][0][c.i] < 0 && !locked.Contains(c.s) && c.s != shutS).ToList();
        if (spots.Count == 0 || locked.Contains(s) || !views.TryGetValue((s, i), out var view))
        {
            ShooPigeon(false);
            return;
        }
        var (b, j) = spots[rng.Next(spots.Count)];
        history.Push(Clone(shelves));
        shelves[b][0][j] = shelves[s][0][i];
        shelves[s][0][i] = -1;
        views.Remove((s, i));
        views[(b, j)] = view;
        pigeon = (-1, -1);
        nextPigeon = Time.time + 10;
        Toast("Le pigeon l'a emporté !");
        Fx.Play("back");
        Fx.Haptic(2);
        StartCoroutine(Carry(pigeon3D, view.transform, SlotPos(b, j)));
        pigeon3D = null;
        if (!busy) StartCoroutine(Resolve());
    }

    // the pigeon flaps over to the drop slot with the product hanging below it, lets go, and flies away
    IEnumerator Carry(Transform bird, Transform product, Vector3 to)
    {
        var start = product.position;
        for (float t = 0; t < .6f && product; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / .6f);
            float arc = Mathf.Sin(Mathf.PI * k); // up and out towards the camera, clear of the shelves above
            product.position = Vector3.Lerp(start, to, k) + (Vector3.up * .8f - cam.transform.forward * 1.5f) * arc;
            if (bird)
            {
                bird.position = product.position + Vector3.up * .9f;
                Flap(bird);
            }
            yield return null;
        }
        if (product)
        {
            product.position = to;
            StartCoroutine(Fx.Squash(product));
        }
        if (bird) StartCoroutine(FlyAway(bird));
    }

    static void Flap(Transform bird)
    {
        foreach (Transform part in bird)
            if (part.name == "wing") part.localRotation = Quaternion.Euler(0, 0, Mathf.Sign(part.localPosition.x) * (12 + 60 * Mathf.Sin(Time.time * 40)));
    }

    // a tap on a product the twists hold back: says why (or shoos the pigeon), and takes nothing
    bool HeldBack(int s, int i)
    {
        if (pigeon == (s, i))
        {
            ShooPigeon(true);
            return true;
        }
        string why = s == shutS ? "Fermé pour la sieste !" : seals.TryGetValue((s, i), out int n) ? Tf("Cadenassé : encore {0} coup(s)", n) : null;
        if (why == null) return false;
        Toast(why);
        Fx.Play("back");
        StartCoroutine(Fx.Squash(views[(s, i)].transform));
        return true;
    }

    // each move brings the sealed bentos one step closer to opening
    void TickSeals()
    {
        foreach (var c in seals.Keys.ToList())
        {
            if (--seals[c] > 0) continue;
            seals.Remove(c);
            Fx.Burst(SlotAim(c.s, c.i) + new Vector3(0, 0, -1), 15);
            Fx.Play("match", 1.6f);
            Reward(c.s, 0, BentoSeconds, Tf("CADENAS OUVERT ! +{0} s", BentoSeconds));
            Count("bento");
        }
    }

    // a trio just made: a bento popped with it, the siesta beaten, the sale's product paid
    void TrioEvents(int s, int id)
    {
        seals.Keys.Where(c => c.s == s).ToList().ForEach(c => seals.Remove(c));
        if (s == shutWarn) // emptied a trio there before the shutter fell
        {
            Reward(s, 0, SiestaSeconds, Tf("JUSTE À TEMPS ! +{0} s", SiestaSeconds));
            Count("siesta");
        }
        if (id == saleId)
        {
            saleId = -1;
            nextSale = Time.time + 12;
            Reward(s, 0, SaleSeconds, Tf("PROMO ! +{0} s", SaleSeconds));
            Fx.Play("till");
            Count("promo");
        }
    }

    void Reward(int s, int coinsWon, int seconds, string praise)
    {
        coins += coinsWon;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.Save();
        time = Mathf.Min(total, time + seconds);
        Toast(praise);
        Fx.Haptic(3);
        if (coinsWon > 0) for (int c = 0; c < 4; c++) Fly(ShelfPos(s) + new Vector3((c - 1.5f) * .3f, .7f, 0), coinTarget, null, .1f + c * .06f);
        Fly(ShelfPos(s) + new Vector3(0, 1f, 0), clockTarget, $"+{seconds} s", .2f);
    }

    // ---------- drawn over the board ----------

    void DrawEvents(float k)
    {
        // the words on the twists' 3D models: the sale tags' price cut, the moves left on each padlock, the shutter's board
        foreach (var tag in tags3D.Values)
            if (tag)
            {
                var p = ToGui(tag.Find("card").position);
                Label(new Rect(p.x - 80, p.y - 38, 160, 50), "-50%", 34, Red);
                Label(new Rect(p.x - 80, p.y + 2, 160, 36), $"{Mathf.CeilToInt(saleUntil - Time.time)} s", 26, Ink); // the time left to make its trio
            }
        foreach (var (c, n) in seals)
            if (bentos3D.TryGetValue(c, out var box) && box)
            {
                var p = ToGui(box.Find("lock").TransformPoint(new Vector3(0, .01f, -.08f))); // the moves left, written on the padlock
                Label(new Rect(p.x - 40, p.y - 26, 80, 52), n.ToString(), 38, Ink);
            }
        if (shutter3D && shutter3D.Find("bar/sign").localScale.x > .7f) // its words, once the sign has room to show
        {
            var sign = shutter3D.Find("bar/sign");
            var p = ToGui(sign.TransformPoint(new Vector3(0, -.5f, -.03f)));
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(-sign.localEulerAngles.z, new Vector2(p.x * k, p.y * k)); // swinging with its board
            Label(new Rect(p.x - 150, p.y - 40, 300, 42), "TORNO SUBITO", 30, Ink);
            Label(new Rect(p.x - 150, p.y + 2, 300, 40), shutS >= 0 ? $"{Mathf.CeilToInt(shutUntil - Time.time)} s" : "Chiude...", 30, Red);
            GUI.matrix = m;
        }
    }
}
