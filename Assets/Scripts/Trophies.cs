using UnityEngine;

// Trophies: long-run goals, each with three tiers (bronze, silver, gold) paying coins. Some count events (Count:
// pigeons shooed, bentos opened...), some keep a best (Best: the longest combo), others read the progress already
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
        ("bento", "Maître du bento", "Ouvre {0} bentos", new[] { 10, 50, 200 }, () => Stat("bento")),
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
    int trophyTier;
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
        Medal(new Rect(r.x + 30, r.y + 25, 120, 120), trophyTier);
        Label(new Rect(r.x + 180, r.y + 18, r.width - 200, 60), $"TROPHÉE {TierNames[trophyTier].ToUpper()} !", 40, TierColors[trophyTier], false, TextAnchor.MiddleLeft);
        Label(new Rect(r.x + 180, r.y + 78, r.width - 200, 70), $"{trophyMsg}  +{TierCoins[trophyTier]} pièces", 36, Color.white, false, TextAnchor.MiddleLeft);
    }

    // a round medal in its tier's colour with a star on it (tier -1: not won yet, grey)
    void Medal(Rect r, int tier)
    {
        var c = tier < 0 ? new Color(.78f, .8f, .84f) : TierColors[tier];
        GUI.color = Darker(c);
        GUI.DrawTexture(new Rect(r.x, r.y + 6, r.width, r.height), circle);
        GUI.color = c;
        GUI.DrawTexture(r, circle);
        GUI.color = Color.white;
        star ??= Fx.Star(128);
        GUI.color = tier < 0 ? new Color(1, 1, 1, .6f) : Color.white;
        GUI.DrawTexture(new Rect(r.x + r.width * .2f, r.y + r.height * .18f, r.width * .6f, r.height * .6f), star);
        GUI.color = Color.white;
    }

    // the trophy screen: each one with its medal, what to do next, a progress bar and the three tiers' marks
    void TrophyScreen(float W, float H, float safeTop)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), Cream);
        float y = safeTop + 40;
        if (Button(new Rect(40, y, 250, 96), "Retour", Mist, 40)) OpenMenu(Menu.Home);
        Label(new Rect(300, y, W - 340, 96), "TROPHÉES", 64, Red, true);
        float row = Mathf.Min(150, (H - y - 180) / TrophyList.Length);
        for (int t = 0; t < TrophyList.Length; t++)
        {
            var (_, name, goal, tiers, value) = TrophyList[t];
            int tier = TierOf(t), v = value();
            var card = new Rect(40, y + 130 + t * row, W - 80, row - 14);
            Panel(card, Color.white);
            float m = card.height - 24;
            Medal(new Rect(card.x + 16, card.y + 12, m, m), tier - 1);
            float x = card.x + 32 + m;
            Label(new Rect(x, card.y + 4, card.width - m - 300, card.height * .45f), name, 38, Ink, false, TextAnchor.MiddleLeft);
            string what = tier == 3 ? "Terminé !" : string.Format(goal, tiers[tier]);
            Label(new Rect(x, card.y + card.height * .42f, card.width - m - 300, card.height * .3f), what, 28, new Color(.4f, .45f, .55f), false, TextAnchor.MiddleLeft);
            // progress towards the next tier, the tiers already won as little dots
            var bar = new Rect(card.xMax - 260, card.y + card.height / 2 - 14, 220, 28);
            Fill(bar, new Color(0, 0, 0, .08f));
            float to = tiers[Mathf.Min(tier, 2)];
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v / to), bar.height), tier == 3 ? TierColors[2] : Gold);
            Label(new Rect(bar.x, bar.y - 44, bar.width, 40), tier == 3 ? $"{v}" : $"{Mathf.Min(v, (int)to)} / {to}", 26, Ink);
            for (int k = 0; k < 3; k++)
            {
                GUI.color = k < tier ? TierColors[k] : new Color(0, 0, 0, .12f);
                GUI.DrawTexture(new Rect(bar.x + 70 * k + 50, bar.yMax + 8, 26, 26), circle);
            }
            GUI.color = Color.white;
        }
    }
}
