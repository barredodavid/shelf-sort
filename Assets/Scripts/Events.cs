using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Things that happen during a level, on top of the puzzle. None of them can make a level unsolvable: they hold
// something back for a while, pay a bonus, or (the pigeon) make an ordinary move the player can undo.
//  - A hurried customer (levels 11-20 of each country): now and then wants one product; the matching products are
//    circled, and their trio within the time pays coins and seconds.
//  - One twist per country, from its 6th level:
//    France, the pigeon: lands on a product, which can't be taken until the pigeon is shooed off with a tap (a few
//    coins and a second); left there too long, it flies off with the product and drops it on another shelf;
//    Italy, the siesta: a shelf pulls down its shutter for a few seconds (nothing taken from it or put on it);
//    Japan, sealed bentos: one or two products only open after 3 moves;
//    USA, the flash sale: one product is half price for a few seconds, and its trio pays a bonus.
public partial class Game
{
    const float CustomerTime = 15, PigeonTime = 4, PigeonFlight = 1.1f, SiestaTime = 4, SaleTime = 8;
    const int CustomerCoins = 10, SaleCoins = 10, BentoMoves = 3, PigeonCoins = 3;

    bool customers;
    int twist = -1; // the country whose twist runs in this level (-1: none)
    int customerId = -1, saleId = -1, shutS = -1;
    float customerUntil, nextCustomer, saleUntil, nextSale, shutUntil, nextShut, pigeonUntil, nextPigeon, pigeonLandAt;
    (int s, int i) pigeon = (-1, -1);
    Transform pigeon3D; // its model, standing on the product (rebuilt if Render clears the board)
    readonly Dictionary<(int s, int i), int> seals = new(); // sealed bentos: moves left before they open

    // called at the end of StartLevel
    void StartEvents()
    {
        customers = !daily && lvl >= 10;
        twist = !daily && lvl >= 5 ? world : -1;
        customerId = saleId = shutS = -1;
        pigeon = (-1, -1);
        seals.Clear();
        float t = Time.time;
        nextCustomer = t + 10;
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
        if (customers)
        {
            if (customerId >= 0 && t > customerUntil)
            {
                customerId = -1;
                nextCustomer = t + 12;
                Toast("Le client est parti...");
            }
            else if (customerId < 0 && t > nextCustomer)
            {
                var kinds = KindsLeft();
                if (kinds.Count > 0)
                {
                    customerId = kinds[rng.Next(kinds.Count)];
                    customerUntil = t + CustomerTime;
                    Fx.Play("pick", 1.3f);
                }
                else nextCustomer = t + 5;
            }
        }
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
            case 1: // siesta: a shelf shuts for a few seconds
                if (shutS >= 0 && t > shutUntil)
                {
                    shutS = -1;
                    nextShut = t + 14;
                }
                else if (shutS < 0 && t > nextShut)
                {
                    var open = Enumerable.Range(0, shelves.Count).Where(s => !locked.Contains(s) && s != dragS && shelves[s][0].Any(x => x >= 0)).ToList();
                    if (open.Count > 0)
                    {
                        shutS = open[rng.Next(open.Count)];
                        shutUntil = t + SiestaTime;
                        Toast("RIPOSO !");
                        Fx.Play("back");
                    }
                }
                break;
            case 3: // flash sale on one product still on the board
                if (saleId >= 0 && t > saleUntil)
                {
                    saleId = -1;
                    nextSale = t + 12;
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
            Reward(s, PigeonCoins, 1, $"Ouste ! +{PigeonCoins}");
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
        string why = s == shutS ? "Fermé pour la sieste !" : seals.TryGetValue((s, i), out int n) ? $"Bento scellé : encore {n} coup{(n > 1 ? "s" : "")}" : null;
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
        }
    }

    // a trio just made: the customer's or the sale's product pays
    void TrioEvents(int s, int id)
    {
        seals.Keys.Where(c => c.s == s).ToList().ForEach(c => seals.Remove(c));
        if (id == customerId)
        {
            customerId = -1;
            nextCustomer = Time.time + 10;
            Reward(s, CustomerCoins, 5, "MERCI !");
        }
        if (id == saleId)
        {
            saleId = -1;
            nextSale = Time.time + 12;
            Reward(s, SaleCoins, 3, "PROMO ! x2");
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
        for (int c = 0; c < 4; c++) Fly(ShelfPos(s) + new Vector3((c - 1.5f) * .3f, .7f, 0), coinTarget, null, .1f + c * .06f);
        Fly(ShelfPos(s) + new Vector3(0, 1f, 0), clockTarget, $"+{seconds} s", .2f);
    }

    // ---------- drawn over the board ----------

    void DrawEvents(float k)
    {
        // products the customer wants, or on sale: a pulsing ring under them, a "-50%" badge
        float pulse = 1 + .08f * Mathf.Sin(Time.time * 6);
        foreach (var (s, i) in FrontSlots())
        {
            int x = shelves[s][0][i];
            if (!LevelGen.IsProduct(x) || LevelGen.IsBoxed(x)) continue;
            var p = ToGui(SlotAim(s, i));
            if (LevelGen.Id(x) == customerId)
            {
                GUI.color = new Color(1, .8f, .2f, .55f);
                float r = 95 * pulse;
                GUI.DrawTexture(new Rect(p.x - r, p.y - r, 2 * r, 2 * r), circle);
                GUI.color = Color.white;
            }
            if (LevelGen.Id(x) == saleId)
            {
                var b = new Rect(p.x + 20, p.y - 110, 96, 96);
                GUI.color = Red;
                GUI.DrawTexture(b, star ??= Fx.Star(128));
                GUI.color = Color.white;
                Label(b, "-50%", 28, Gold);
            }
        }
        // sealed bentos: a red lid tied over the product, the moves left on it
        foreach (var ((s, i), n) in seals)
        {
            var p = ToGui(SlotAim(s, i));
            var lid = new Rect(p.x - 70, p.y - 60, 140, 120);
            Panel(lid, new Color(.78f, .12f, .12f, .85f));
            Fill(new Rect(lid.center.x - 6, lid.y, 12, lid.height), Gold);
            Label(lid, n.ToString(), 70, Color.white, true);
        }
        // the siesta: a metal shutter down over the shelf's front, the seconds left
        if (shutS >= 0)
        {
            Vector2 a = ToGui(SlotPos(shutS, 0) + new Vector3(-SlotW / 2, 1.35f, -.6f)), b = ToGui(SlotPos(shutS, 2) + new Vector3(SlotW / 2, 0, -.6f));
            var r = new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
            Fill(r, new Color(.62f, .66f, .72f, .95f));
            for (float y = r.y + 14; y < r.yMax; y += 22) Fill(new Rect(r.x, y, r.width, 4), new Color(.45f, .48f, .54f));
            Label(new Rect(r.x, r.center.y - 50, r.width, 60), "RIPOSO", 44, Ink);
            Label(new Rect(r.x, r.center.y + 4, r.width, 50), $"{Mathf.CeilToInt(shutUntil - Time.time)} s", 36, Ink);
        }
    }

    // in the sign's place while a customer waits: who, what to do, the time left
    void CustomerCard(float from, float to)
    {
        float h = Mathf.Clamp(to - from, 160, 240);
        var card = new Rect(40, from + (to - from - h) / 2, 1000, h);
        Panel(card, Cream);
        Label(new Rect(card.x, card.y + 14, card.width, 60), "UN CLIENT PRESSÉ !", 48, Red, true);
        Label(new Rect(card.x, card.y + 72, card.width, 50), "Fais le trio des produits entourés", 36, Ink);
        var bar = new Rect(card.x + 60, card.yMax - 52, card.width - 120, 30);
        Fill(bar, new Color(0, 0, 0, .15f));
        Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01((customerUntil - Time.time) / CustomerTime), bar.height), Gold);
    }
}
