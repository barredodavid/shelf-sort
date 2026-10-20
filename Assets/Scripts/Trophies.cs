using System.Linq;
using UnityEngine;

// Trophies: long-run goals, each with three tiers (bronze, silver, gold) paying coins. Some count events (Count:
// pigeons shooed, padlocks opened...), some keep a best (Best: the longest combo), others read the progress already
// saved (three-star levels, albums, countries done, the daily streak). Every change checks them all; a tier newly
// reached pays and shows a banner. The trophy screen lists them with their progress.
public partial class Game
{
    static readonly int[] TierCoins = { 25, 75, 200 };
    static readonly string[] TierNames = { "Bronze", "Argent", "Or" };
    static readonly Color[] TierColors = { new(.8f, .5f, .25f), new(.75f, .78f, .82f), new(1f, .76f, .24f) };

    static int Stat(string key) => PlayerPrefs.GetInt("stat_" + key, 0);

    // name, what to do ({0}: the next tier's target), tiers, progress
    static readonly (string key, string name, string goal, int[] tiers, System.Func<int> value)[] TrophyList =
    {
        ("pigeons", "Chasseur de pigeons", "Chasse {0} pigeons", new[] { 10, 50, 200 }, () => Stat("pigeons")),
        ("siesta", "Juste à temps", "Fais {0} trios avant le rideau", new[] { 5, 25, 100 }, () => Stat("siesta")),
        ("bento", "Serrurier", "Ouvre {0} cadenas", new[] { 10, 50, 200 }, () => Stat("bento")),
        ("promo", "Roi des promos", "Fais {0} trios en promo", new[] { 10, 50, 200 }, () => Stat("promo")),
        ("trios", "Trieur", "Fais {0} trios", new[] { 100, 1000, 10000 }, () => Stat("trios")),
        ("combo", "Combo", "Enchaîne un combo x{0}", new[] { 3, 5, 8 }, () => Stat("combo")),
        ("golden", "Chercheur d'or", "Fais {0} trios dorés", new[] { 10, 50, 200 }, () => Stat("golden")),
        ("stars", "Perfectionniste", "Finis {0} niveaux à 3 étoiles", new[] { 10, 40, 80 }, ThreeStarLevels),
        ("streak", "Fidèle", "Réussis le défi {0} jours de suite", new[] { 3, 7, 30 }, () => Streak),
        ("albums", "Collectionneur", "Complète {0} album(s)", new[] { 1, 2, 4 }, () => CountWorlds(w => StickerCount(w) == Products.Count)),
        ("world", "Tour du monde", "Finis {0} pays", new[] { 1, 2, 4 }, () => CountWorlds(Completed)),
    };

    static int CountWorlds(System.Func<int, bool> done)
    {
        int n = 0;
        for (int w = 0; w < Worlds.Length; w++) if (done(w)) n++;
        return n;
    }

    static int ThreeStarLevels()
    {
        int n = 0;
        for (int w = 0; w < Worlds.Length; w++)
            for (int l = 0; l < LevelsPerWorld; l++) if (Stars(w, l) == 3) n++;
        return n;
    }

    static int TierOf(int t) => PlayerPrefs.GetInt("trophy_" + TrophyList[t].key, 0); // tiers reached, 0..3

    void Count(string key, int add = 1)
    {
        PlayerPrefs.SetInt("stat_" + key, Stat(key) + add);
        CheckTrophies();
    }

    void Best(string key, int value)
    {
        if (value <= Stat(key)) return;
        PlayerPrefs.SetInt("stat_" + key, value);
        CheckTrophies();
    }

    string trophyMsg;
    int trophyTier, trophyIndex;
    float trophyAt = -10;

    // any tier newly reached pays its coins and puts up the banner
    void CheckTrophies()
    {
        for (int t = 0; t < TrophyList.Length; t++)
        {
            int had = TierOf(t), v = TrophyList[t].value(), now = had;
            while (now < 3 && v >= TrophyList[t].tiers[now]) now++;
            if (now == had) continue;
            for (int k = had; k < now; k++) coins += TierCoins[k];
            PlayerPrefs.SetInt("coins", coins);
            PlayerPrefs.SetInt("trophy_" + TrophyList[t].key, now);
            trophyMsg = TrophyList[t].name;
            trophyIndex = t;
            trophyTier = now - 1;
            trophyAt = Time.time;
            Fx.Play("win", 1.2f);
        }
        PlayerPrefs.Save();
    }

    // the banner sliding down from the top for a few seconds: medal, "TROPHÉE !", its name and tier, the coins
    void TrophyBanner(float W, float top)
    {
        float age = Time.time - trophyAt;
        if (age > 3.2f) return;
        float slide = Mathf.Min(Mathf.SmoothStep(0, 1, age / .3f), Mathf.SmoothStep(0, 1, (3.2f - age) / .3f));
        var r = new Rect(60, top - 260 + 300 * slide, W - 120, 170);
        Panel(new Rect(r.x, r.y + 12, r.width, r.height), Darker(Ink));
        Panel(r, Ink);
        Medal(new Rect(r.x + 30, r.y + 25, 120, 120), trophyTier, trophyIndex);
        Label(new Rect(r.x + 180, r.y + 18, r.width - 200, 60), $"TROPHÉE {TierNames[trophyTier].ToUpper()} !", 40, TierColors[trophyTier], false, TextAnchor.MiddleLeft);
        Label(new Rect(r.x + 180, r.y + 78, r.width - 200, 70), $"{trophyMsg}  +{TierCoins[trophyTier]} pièces", 36, Color.white, false, TextAnchor.MiddleLeft);
    }

    // a medal: a rim in its tier's colour round a pale disc, trophy t's picture on it (tier -1: not won yet, greyed)
    void Medal(Rect r, int tier, int t = -1)
    {
        var c = tier < 0 ? new Color(.74f, .76f, .8f) : TierColors[tier];
        GUI.color = Darker(c);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height * .05f, r.width, r.height), circle);
        GUI.color = c;
        GUI.DrawTexture(r, circle);
        GUI.color = Color.Lerp(c, Color.white, .6f);
        float inset = r.width * .1f;
        GUI.DrawTexture(new Rect(r.x + inset, r.y + inset, r.width - 2 * inset, r.height - 2 * inset), circle);
        var tint = tier < 0 ? new Color(.55f, .57f, .62f, .7f) : Color.white;
        float side = r.width * .58f; // the square that fits inside the pale disc (its diameter / sqrt 2): nothing pokes out
        var pic = new Rect(r.center.x - side / 2, r.center.y - side / 2, side, side);
        string key = t >= 0 ? TrophyList[t].key : "";
        var icon = t >= 0 && trophyIcons.TryGetValue(key, out var ic) ? ic : null;
        GUI.color = tint;
        if (icon) GUI.DrawTexture(pic, icon);
        else if (key == "combo") Label(pic, "x" + TrophyList[t].tiers[Mathf.Max(0, tier)], (int)(r.width * .34f), tier < 0 ? tint : Red, true);
        else if (key == "streak") // a little calendar page
        {
            var page = new Rect(pic.x + pic.width * .08f, pic.y + pic.height * .08f, pic.width * .84f, pic.height * .84f);
            Fill(page, Color.white * tint);
            Fill(new Rect(page.x, page.y, page.width, page.height * .28f), Red * tint);
            Label(new Rect(page.x, page.y + page.height * .25f, page.width, page.height * .75f), TrophyList[t].tiers[Mathf.Max(0, tier)].ToString(), (int)(r.width * .24f), Ink * tint);
        }
        else if (key == "siesta") // just in time: a stopwatch
        {
            timerIcon ??= Fx.Icon(2);
            GUI.color = tier < 0 ? tint : Ink;
            GUI.DrawTexture(pic, timerIcon);
        }
        else if (key == "stars") // three stars, the middle one big, each with a dark outline to stand out on any rim
        {
            void Star3(float cx, float cy, float d)
            {
                GUI.color = tier < 0 ? tint * .8f : new Color(.55f, .3f, .08f);
                GUI.DrawTexture(new Rect(cx - d * .58f, cy - d * .55f, d * 1.16f, d * 1.16f), star);
                GUI.color = tier < 0 ? tint : Gold;
                GUI.DrawTexture(new Rect(cx - d / 2, cy - d / 2, d, d), star);
            }
            star ??= Fx.Star(128);
            Star3(pic.center.x - pic.width * .34f, pic.center.y + pic.height * .1f, pic.width * .44f);
            Star3(pic.center.x + pic.width * .34f, pic.center.y + pic.height * .1f, pic.width * .44f);
            Star3(pic.center.x, pic.center.y - pic.height * .06f, pic.width * .62f);
        }
        else // a star (the tier medals)
        {
            star ??= Fx.Star(128);
            GUI.color = tier < 0 ? tint : Darker(c); // dark on the pale disc
            GUI.DrawTexture(pic, star);
        }
        GUI.color = Color.white;
    }

    // a rounded bar: two round caps and the band between them
    void Pill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.height, r.height), circle);
        GUI.DrawTexture(new Rect(r.xMax - r.height, r.y, r.height, r.height), circle);
        GUI.color = Color.white;
        if (r.width > r.height) Fill(new Rect(r.x + r.height / 2, r.y, r.width - r.height, r.height), c);
    }

    // each trophy's picture, rendered once from the game's own 3D models into a texture with a clear background;
    // called by Update when the trophy screen or the banner needs them (not from OnGUI, which mustn't render cameras)
    static readonly System.Collections.Generic.Dictionary<string, RenderTexture> trophyIcons = new();
    Camera iconCam;
    Texture2D timerIcon;
    void TrophyIcons()
    {
        if (menu != Menu.Trophies && Time.time - trophyAt > 3.2f) return;
        for (int t = 0; t < TrophyList.Length; t++) TrophyIcon(t);
    }

    void TrophyIcon(int t)
    {
        string key = TrophyList[t].key;
        if (trophyIcons.TryGetValue(key, out var have) && have) return;
        GameObject model = key switch
        {
            "pigeons" => Products.Pigeon(),
            "bento" => Products.Padlock(),
            "promo" => Products.SaleTag(),
            "albums" => Products.Chest(true),
            "world" => Products.Globe(.8f),
            "trios" or "golden" => new GameObject("Trio"),
            _ => null,
        };
        if (!model) return;
        model.transform.position = StageO + new Vector3(0, 50, 0);
        var at = model.transform.position;
        Transform Product(int id, Vector3 offset, float scale = 1, bool gold = false) // one of the country's products, in the picture
        {
            var p = Item(id, at + offset, scale, false, "icon");
            p.transform.SetParent(model.transform, true);
            if (gold) foreach (var r in p.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(Goldify).ToArray();
            return p.transform;
        }
        switch (key)
        {
            case "trios": // a trio of a tall product, the middle one in front
            case "golden": // the same, gilded
                int id = key == "trios" ? 4 : 1;
                Product(id, new Vector3(-.8f, 0, .3f), .9f, key == "golden");
                Product(id, new Vector3(.8f, 0, .3f), .9f, key == "golden");
                Product(id, new Vector3(0, 0, -.2f), 1, key == "golden");
                break;
            case "bento": // a product tied up under its padlock
                var tied = Product(2, new Vector3(0, 0, .05f));
                Products.TieLock(model.transform, Mathf.Max(.4f, TopOf(tied) - at.y + .06f));
                break;
            case "promo": // the tag hanging over a product
                var onSale = Product(3, new Vector3(-.25f, -1.2f, .45f));
                onSale.position += Vector3.up * (at.y - .45f - TopOf(onSale)); // its top just under the tag
                break;
            case "world":
                model.transform.rotation = Quaternion.Euler(-20, 160, 0);
                break;
            case "pigeons" or "albums":
                model.transform.rotation = Quaternion.Euler(0, 30, 0);
                break;
        }
        trophyIcons[key] = Snapshot(model);
    }

    // a picture of a model (already placed out of sight), framed on its bounds, seen a little from above, on a clear
    // background (standing: its base on the picture's bottom edge, not centred); the model is destroyed
    RenderTexture Snapshot(GameObject model, int size = 256, bool standing = false)
    {
        var rs = model.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        if (!iconCam)
        {
            iconCam = new GameObject("IconCam").AddComponent<Camera>();
            iconCam.orthographic = true;
            iconCam.clearFlags = CameraClearFlags.SolidColor;
            iconCam.backgroundColor = new Color(0, 0, 0, 0);
            iconCam.enabled = false; // renders on demand
        }
        var tex = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        iconCam.targetTexture = tex;
        iconCam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.1f;
        var aim = standing ? new Vector3(b.center.x, b.min.y + iconCam.orthographicSize * .97f, b.center.z) : b.center;
        iconCam.transform.SetPositionAndRotation(aim + Quaternion.Euler(12, 0, 0) * new Vector3(0, 0, -10), Quaternion.Euler(12, 0, 0));
        iconCam.Render();
        model.SetActive(false);
        Destroy(model);
        return tex;
    }

    // the trophy screen: each one on its card, its medal, what to do next, a rounded gauge and the three tiers as
    // little medals; a finished one's card turns gold
    void TrophyScreen(float W, float H, float safeTop)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), Cream);
        float y = safeTop + 40;
        if (Button(new Rect(40, y, 250, 96), "Retour", Mist, 40)) OpenMenu(Menu.Home);
        int won = 0;
        for (int t = 0; t < TrophyList.Length; t++) won += TierOf(t);
        Label(new Rect(300, y - 8, W - 340, 72), "TROPHÉES", 60, Red, true);
        Label(new Rect(300, y + 58, W - 340, 40), $"{won} / {TrophyList.Length * 3} médailles", 30, new Color(.4f, .45f, .55f));
        float row = Mathf.Min(170, (H - y - 170) / TrophyList.Length);
        for (int t = 0; t < TrophyList.Length; t++)
        {
            var (_, name, goal, tiers, value) = TrophyList[t];
            int tier = TierOf(t), v = value();
            var card = new Rect(40, y + 140 + t * row, W - 80, row - 16);
            Panel(new Rect(card.x, card.y + 8, card.width, card.height), new Color(0, 0, 0, .06f));
            Panel(card, tier == 3 ? new Color(1f, .94f, .74f) : Color.white);
            float m = card.height + 10;
            Medal(new Rect(card.x + 6, card.y - 5, m, m), tier - 1, t);
            float x = card.x + 28 + m;
            Label(new Rect(x, card.y + 6, card.width - m - 330, card.height * .48f), name, 40, Ink, false, TextAnchor.MiddleLeft);
            string what = tier == 3 ? "Trophée complet !" : string.Format(goal, tiers[tier]);
            Label(new Rect(x, card.y + card.height * .5f, card.width - m - 330, card.height * .36f), what, 28, new Color(.42f, .46f, .56f), false, TextAnchor.MiddleLeft);
            float to = tiers[Mathf.Min(tier, 2)], fill = tier == 3 ? 1 : Mathf.Clamp01(v / to);
            var bar = new Rect(card.xMax - 300, card.y + card.height * .16f, 270, 42);
            Pill(bar, new Color(.91f, .91f, .89f)); // opaque: the caps overlap the band
            if (fill > 0) Pill(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * fill), bar.height), tier == 3 ? TierColors[2] : Gold);
            Label(bar, tier == 3 ? $"{v}" : $"{Mathf.Min(v, (int)to)} / {to}", 26, Ink);
            for (int k = 0; k < 3; k++)
                Medal(new Rect(bar.x + 45 + 66 * k, bar.yMax + 10, 46, 46), k < tier ? k : -1);
        }
    }
}
