using UnityEngine;

// A chest after each country's 5th, 10th and 15th level (the HARD ones; the 20th unveils the monument instead):
// the first time one is won, the map opens on a wobbling chest; a tap flings its lid open and pays a random sum of
// coins (now and then a jackpot). On the map a chest sits beside those shops' signs until it is opened: grey while
// the level is still to win, bouncing once it is won (a tap opens it, for chests left unopened).
public partial class Game
{
    float chestAt = -1, chestOpenedAt = -1; // chestAt > 0: the chest card is up
    int chestCoins, chestLvl;

    static bool HasChest(int l) => (l + 1) % 5 == 0 && l < LevelsPerWorld - 1;
    static bool ChestOpened(int w, int l) => PlayerPrefs.GetInt($"chest{w}_{l}", 0) == 1;

    // called by EnterMap: a chest level just won for the first time puts the chest up
    void OfferChest()
    {
        if (!Done(world, lvl) || !HasChest(lvl) || ChestOpened(world, lvl)) return;
        chestAt = Time.time;
        chestOpenedAt = -1;
        chestLvl = lvl;
    }

    void OpenChest()
    {
        float r = Random.value;
        chestCoins = r < .1f ? 200 : r < .4f ? 80 : 50;
        coins += chestCoins;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.SetInt($"chest{mapWorld}_{chestLvl}", 1);
        PlayerPrefs.Save();
        chestOpenedAt = Time.time;
        Fx.Play("win");
    }

    // the chest card over the map: closed it wobbles and waits for a tap, open it shines and shows the coins
    void ChestCard(float W, float H, float k)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .6f));
        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(chestAt), new Vector2(W / 2 * k, H / 2 * k));
        bool open = chestOpenedAt > 0;
        Label(new Rect(0, H / 2 - 520, W, 130), open ? (chestCoins >= 200 ? "JACKPOT !" : "BRAVO !") : "UN COFFRE !", 100, Gold, true);
        if (!open) Label(new Rect(0, H / 2 - 390, W, 60), "Pour avoir battu un niveau DUR", 40, Color.white);

        var body = new Rect(W / 2 - 180, H / 2 - 40, 360, 220);
        if (open) // rays turning slowly behind the open chest
        {
            star ??= Fx.Star(128);
            var spin = GUI.matrix;
            GUIUtility.RotateAroundPivot(Time.time * 30, new Vector2(body.center.x * k, (body.y - 40) * k));
            GUI.color = new Color(1, .85f, .3f, .45f);
            GUI.DrawTexture(new Rect(body.center.x - 330, body.y - 370, 660, 660), star);
            GUI.color = Color.white;
            GUI.matrix = spin;
        }
        var wobble = GUI.matrix;
        if (!open && Mathf.Repeat(Time.time, 1.4f) < .5f) // a shake every second or so, like something's inside
            GUIUtility.RotateAroundPivot(5 * Mathf.Sin(Time.time * 40), new Vector2(body.center.x * k, body.yMax * k));
        DrawChest(body, open ? Mathf.Clamp01((Time.time - chestOpenedAt) / .25f) : 0);
        GUI.matrix = wobble;

        if (!open)
        {
            Label(new Rect(0, body.yMax + 60, W, 60), "Touche pour l'ouvrir", 44, Color.white);
            if (GUI.Button(new Rect(body.x - 60, body.y - 160, body.width + 120, body.height + 240), GUIContent.none, GUIStyle.none)) OpenChest();
        }
        else
        {
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(chestOpenedAt + .15f), new Vector2(W / 2 * k, (body.yMax + 110) * k));
            Label(new Rect(0, body.yMax + 60, W, 110), $"+{chestCoins} pièces", 84, Color.white, true);
            GUI.matrix = m;
            if (Time.time > chestOpenedAt + .6f && Button(new Rect(W / 2 - 220, body.yMax + 230, 440, 130), "Super !", Gold, 56)) chestAt = -1;
        }
        GUI.matrix = keep;
    }

    // a treasure chest: dark outline, wooden body with planks, domed lid, gold bands with rivets, a lock. lid: 0 closed,
    // 1 thrown back (its dark inside showing) over a heap of shining coins
    void DrawChest(Rect body, float lid)
    {
        Color wood = new(.62f, .36f, .17f), dark = new(.27f, .15f, .07f), inner = new(.36f, .2f, .1f);
        void Disc(float cx, float cy, float r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(cx - r, cy - r, 2 * r, 2 * r), circle);
            GUI.color = Color.white;
        }
        void Bands(float y, float h)
        {
            foreach (float bx in new[] { body.x + 34, body.xMax - 66 })
            {
                Fill(new Rect(bx, y, 32, h), Gold);
                Fill(new Rect(bx + 24, y, 8, h), Darker(Gold));
                Disc(bx + 16, y + 16, 6, Darker(Gold)); // rivets
                Disc(bx + 16, y + h - 16, 6, Darker(Gold));
            }
        }
        float lidH = 110;
        if (lid > 0) // the lid thrown back: its inside rises behind the chest, then the coins heaped over the rim
        {
            float h = lidH * lid;
            Fill(new Rect(body.x - 8, body.y - h - 8, body.width + 16, h + 8), dark);
            Fill(new Rect(body.x, body.y - h, body.width, h), inner);
            for (int i = 0; i < 14; i++) // coins, the middle ones piled higher
            {
                float u = (i + .5f) / 14, x = body.x + 24 + u * (body.width - 48);
                float y = body.y + 6 - 34 * Mathf.Sin(Mathf.PI * u) - (i % 3) * 8;
                Disc(x, y, 26, Darker(Gold));
                Disc(x, y - 3, 23, Gold);
                Disc(x - 7, y - 10, 6, new Color(1, 1, 1, .7f)); // shine
            }
        }
        Fill(new Rect(body.x - 8, body.y - 8, body.width + 16, body.height + 16), dark); // outline
        Fill(body, wood);
        foreach (float f in new[] { .36f, .68f }) Fill(new Rect(body.x, body.y + body.height * f, body.width, 6), Darker(wood)); // planks
        Bands(body.y, body.height);
        if (lid <= 0) // closed: a domed lid in three steps, its own bands, the lock across the seam
        {
            for (int s = 0; s < 3; s++)
            {
                float inset = s * s * 10;
                var r = new Rect(body.x + inset, body.y - lidH * (1 - s * .12f), body.width - 2 * inset, lidH * (1 - s * .12f));
                Fill(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 8), dark);
            }
            for (int s = 0; s < 3; s++)
            {
                float inset = s * s * 10;
                Fill(new Rect(body.x + inset, body.y - lidH * (1 - s * .12f), body.width - 2 * inset, lidH * (1 - s * .12f)), s == 2 ? wood * 1.1f : wood);
            }
            Fill(new Rect(body.x, body.y - 8, body.width, 8), dark); // the seam
            Bands(body.y - lidH, lidH);
        }
        Fill(new Rect(body.center.x - 34, body.y - 30, 68, 76), dark); // the lock
        Fill(new Rect(body.center.x - 28, body.y - 24, 56, 64), Gold);
        Disc(body.center.x, body.y + 2, 9, dark); // keyhole
        Fill(new Rect(body.center.x - 4, body.y + 4, 8, 20), dark);
    }

    // the chest beside a chest level's sign on the map: grey and still until the level is won, then golden and
    // bouncing, and a tap on it opens it
    void MapChest(Rect r, int l)
    {
        bool ready = Done(mapWorld, l);
        if (ready) r.y -= 12 * Mathf.Abs(Mathf.Sin(Time.time * 4));
        Color Tone(Color c) => ready ? c : Color.Lerp(c, new Color(.6f, .63f, .68f), .7f);
        var wood = Tone(new Color(.6f, .35f, .17f));
        var gold = Tone(Gold);
        Fill(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Tone(new Color(.25f, .14f, .07f))); // dark outline
        Fill(new Rect(r.x, r.y + r.height * .38f, r.width, r.height * .62f), wood); // body
        Fill(new Rect(r.x, r.y, r.width, r.height * .38f), Darker(wood)); // lid
        Fill(new Rect(r.x, r.y + r.height * .36f, r.width, 5), Tone(new Color(.25f, .14f, .07f))); // the seam
        foreach (float bx in new[] { r.x + r.width * .18f, r.xMax - r.width * .18f - 10 }) Fill(new Rect(bx, r.y, 10, r.height), gold);
        Fill(new Rect(r.center.x - 11, r.y + r.height * .26f, 22, 26), gold); // the lock
        if (ready && GUI.Button(new Rect(r.x - 20, r.y - 20, r.width + 40, r.height + 40), GUIContent.none, GUIStyle.none))
        {
            chestAt = Time.time;
            chestOpenedAt = -1;
            chestLvl = l;
            Fx.Play("pick");
        }
    }

    // a flat, square-cornered block of colour (Panel's rounded corners need at least 60 px)
    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
