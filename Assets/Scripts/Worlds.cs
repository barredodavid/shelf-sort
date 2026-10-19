using System.Linq;
using UnityEngine;

// Worlds of 20 levels, each with its own product theme. Levels give 1-3 stars; stars unlock the next worlds.
// Level (world w, index l) plays baked level w * 20 + l + 1, so difficulty keeps climbing from world to world.
public partial class Game
{
    const int LevelsPerWorld = 20;

    // items: 12 products per world, index = item id (null pack: built by the Products factory). need: total stars
    // to open the world.
    static readonly (string name, string pack, string[] items, int need, Color color)[] Worlds =
    {
        ("Supérette", null, null, 0, new Color(.91f, .3f, .27f)),
        ("Boulangerie", "Kenney/", new[]
        {
            "donut-sprinkles", "cookie-chocolate", "cupcake", "croissant", "cake", "pie",
            "waffle", "pancakes", "muffin", "whipped-cream", "loaf-baguette", "ginger-bread",
        }, 35, new Color(.93f, .45f, .62f)),
        ("Primeur", "Kenney/", new[]
        {
            "apple", "strawberry", "pineapple", "watermelon", "grapes", "pumpkin",
            "lemon", "pear", "broccoli", "eggplant", "mushroom", "corn",
        }, 80, new Color(.35f, .7f, .35f)),
        ("Fast-food", "Kenney/", new[]
        {
            "burger-cheese", "fries", "hot-dog", "pizza", "taco", "soda",
            "sandwich", "ice-cream", "sundae", "chinese", "frappe", "rice-ball",
        }, 130, new Color(1f, .6f, .2f)),
    };

    int world, lvl; // the level being played (or last played)
    float baseTime; // the level's own time, before any bonus
    bool usedBonus;

    // end-of-level summary
    int starsWon, gained, starsPlayed;
    bool[] criteria = new bool[3];
    string unlockedMsg;
    Texture2D star;

    // ---------- progress ----------

    static int Stars(int w, int l) => PlayerPrefs.GetInt($"stars{w}_{l}", 0);
    static int WorldStars(int w) => Enumerable.Range(0, LevelsPerWorld).Sum(l => Stars(w, l));
    static int TotalStars => Enumerable.Range(0, Worlds.Length).Sum(WorldStars);
    static bool WorldOpen(int w) => TotalStars >= Worlds[w].need;
    // finished, even with 0 stars (stars > 0 also counts: saves from before the "done" flag existed)
    static bool Done(int w, int l) => PlayerPrefs.GetInt($"done{w}_{l}", 0) == 1 || Stars(w, l) > 0;
    static bool LevelOpen(int w, int l) => l == 0 || Done(w, l - 1);
    string LevelName => $"{world + 1}-{lvl + 1}";

    // ---------- per-world products: models + one shared material each (every pack uses a single colormap) ----------

    readonly GameObject[][] worldModels = new GameObject[Worlds.Length][];
    readonly Material[] worldMats = new Material[Worlds.Length], worldGhosts = new Material[Worlds.Length],
        worldIce = new Material[Worlds.Length];

    GameObject Model(int id)
    {
        if (worldModels[world] == null && Worlds[world].pack == null)
        {
            // procedural products carry their own textured materials: only the waiting-row silhouette is shared
            // (frozen ones keep their own colors, the ice block says enough)
            worldModels[world] = Enumerable.Range(0, Products.Count).Select(i =>
            {
                var p = Products.Make(i);
                p.SetActive(false); // template, cloned by Item()
                return p;
            }).ToArray();
            worldGhosts[world] = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
            {
                color = new Color(.3f, .32f, .4f),
            };
        }
        else if (worldModels[world] == null)
        {
            var w = Worlds[world];
            worldModels[world] = w.items.Select(f => Resources.Load<GameObject>(w.pack + f)).ToArray();
            var m = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
            {
                mainTexture = worldModels[world][0].GetComponentInChildren<Renderer>().sharedMaterial.mainTexture,
                color = Color.white,
            };
            m.SetFloat("_Smoothness", .2f);
            worldMats[world] = m;
            worldGhosts[world] = new Material(m) { color = new Color(.35f, .37f, .45f) };
            worldIce[world] = new Material(m) { color = new Color(.62f, .85f, 1f) }; // frozen: icy blue tint
        }
        return worldModels[world][id];
    }

    // ---------- stars ----------

    // Fills the summary and saves progress. Called by Win().
    void ScoreLevel()
    {
        int before = TotalStars;
        criteria[0] = moves <= par;
        criteria[1] = time >= baseTime * .4f;
        criteria[2] = !usedBonus && !adUsed;
        starsWon = criteria.Count(c => c);
        starsPlayed = 0;
        PlayerPrefs.SetInt($"done{world}_{lvl}", 1);
        if (starsWon > Stars(world, lvl)) PlayerPrefs.SetInt($"stars{world}_{lvl}", starsWon);

        gained = 10 * starsWon + (hard ? 15 : 0);
        coins += gained;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.SetInt("world", world);
        PlayerPrefs.Save();

        int after = TotalStars;
        int opened = Enumerable.Range(1, Worlds.Length - 1).FirstOrDefault(w => before < Worlds[w].need && after >= Worlds[w].need);
        unlockedMsg = opened > 0 ? $"Monde {opened + 1} débloqué : {Worlds[opened].name} !" : null;
    }

    // stars pop in one after another on the summary, each with a rising note
    void TickStars()
    {
        if (!over || !won || starsPlayed >= starsWon) return;
        if (Time.time < overAt + .45f + starsPlayed * .35f) return;
        Fx.Play("pick", 1 + .25f * starsPlayed);
        starsPlayed++;
    }

    void PlayLevel(int w, int l)
    {
        world = w;
        lvl = l;
        menu = Menu.None;
        StartLevel();
        Fx.Play("match");
    }

    // ---------- GUI pieces ----------

    void Star(Rect r, bool lit)
    {
        star ??= Fx.Star(128);
        GUI.color = lit ? Gold : new Color(0, 0, 0, .18f);
        GUI.DrawTexture(r, star);
        GUI.color = Color.white;
    }

    // "★ 23" pill
    void StarCount(float x, float y, int n)
    {
        Panel(new Rect(x, y, 250, 96), Cream);
        Star(new Rect(x + 20, y + 16, 64, 64), true);
        Label(new Rect(x + 85, y, 150, 96), n.ToString(), 50, Ink);
    }

    // top of the map screens: awning, back button, star count, title
    float MapHeader(float W, float safeTop, string backLabel, Menu back, string title, string sub)
    {
        Awning(0, 0, 9, 120, safeTop + 90);
        float y = safeTop + 90 + 60 + 30;
        if (Button(new Rect(40, y, 250, 96), backLabel, Mist, 40)) OpenMenu(back);
        StarCount(W - 290, y, TotalStars);
        Label(new Rect(0, y + 120, W, 130), title, 100, Red, true);
        Label(new Rect(0, y + 245, W, 60), sub, 40, Ink);
        return y + 330;
    }

    void WorldsScreen(float W, float H, float safeTop, float bottom)
    {
        float y = MapHeader(W, safeTop, "Retour", Menu.Home, "MONDES", $"{TotalStars} étoiles gagnées");
        float cardH = Mathf.Min(260, (bottom - y) / Worlds.Length - 30);
        for (int w = 0; w < Worlds.Length; w++)
        {
            var r = new Rect(60, y + w * (cardH + 30), W - 120, cardH);
            bool open = WorldOpen(w);
            GUI.enabled = open;
            if (Button(r, "", open ? Cream : Mist))
            {
                world = w;
                OpenMenu(Menu.Levels);
            }
            GUI.enabled = true;

            // number badge in the world's color, name, stars or the lock condition
            var badge = new Rect(r.x + 25, r.y + (cardH - 150) / 2, 150, 150);
            Panel(badge, open ? Worlds[w].color : new Color(.6f, .64f, .7f));
            Label(badge, (w + 1).ToString(), 90, Color.white, true);
            Label(new Rect(r.x + 210, r.y + cardH / 2 - 80, r.width - 230, 80), Worlds[w].name, 64, open ? Ink : new Color(.4f, .45f, .55f), false, TextAnchor.MiddleLeft);
            var line = new Rect(r.x + 210, r.y + cardH / 2 + 5, r.width - 230, 64);
            if (open)
            {
                Star(new Rect(line.x, line.y + 4, 56, 56), true);
                Label(new Rect(line.x + 70, line.y, 400, 64), $"{WorldStars(w)} / {LevelsPerWorld * 3}", 44, Ink, false, TextAnchor.MiddleLeft);
            }
            else
            {
                Label(line, $"Verrouillé : {Worlds[w].need} étoiles", 40, new Color(.4f, .45f, .55f), false, TextAnchor.MiddleLeft);
            }
        }
    }

    void LevelsScreen(float W, float H, float safeTop, float bottom)
    {
        float y = MapHeader(W, safeTop, "Mondes", Menu.Worlds, Worlds[world].name.ToUpper(),
            $"Monde {world + 1} · {WorldStars(world)} / {LevelsPerWorld * 3} étoiles");

        const int cols = 4;
        int rows = LevelsPerWorld / cols;
        float cell = Mathf.Min(220, (bottom - y) / rows - 20), gap = (W - cols * cell) / (cols + 1);
        for (int l = 0; l < LevelsPerWorld; l++)
        {
            var r = new Rect(gap + (l % cols) * (cell + gap), y + (l / cols) * (cell + 20), cell, cell - 50);
            bool open = LevelOpen(world, l);
            GUI.enabled = open;
            if (Button(r, open ? (l + 1).ToString() : "", open ? (Done(world, l) ? Gold : Cream) : Mist, 70))
                PlayLevel(world, l);
            GUI.enabled = true;
            if (!open) Label(r, (l + 1).ToString(), 70, new Color(.6f, .64f, .7f));
            for (int s = 0; s < 3; s++)
                Star(new Rect(r.center.x - 75 + s * 50, r.yMax + 18, 50, 50), s < Stars(world, l));
        }
    }

    // Win summary: stars (animated), why each one was earned, reward, what's next
    void SummaryCard(float W, float H, float k)
    {
        float cardH = unlockedMsg != null ? 1300 : 1220;
        var card = new Rect(70, H / 2 - cardH / 2, W - 140, cardH);
        Panel(new Rect(card.x, card.y + 18, card.width, card.height), Darker(Cream));
        Panel(card, Cream);
        Label(new Rect(card.x, card.y + 30, card.width, 60), $"NIVEAU {LevelName}", 44, Ink);
        Label(new Rect(card.x, card.y + 90, card.width, 130), starsWon == 3 ? "PARFAIT !" : "BRAVO !", 110, Gold, true);

        // big stars, popping in as TickStars plays them
        for (int s = 0; s < 3; s++)
        {
            float size = s == 1 ? 200 : 160;
            bool lit = s < starsPlayed;
            float pop = lit ? Mathf.Clamp01((Time.time - (overAt + .45f + s * .35f)) / .2f) : 1;
            size *= lit ? 1 + .4f * (1 - pop) : 1;
            float cx = card.center.x + (s - 1) * 230, cy = card.y + 330 + (s == 1 ? -15 : 10);
            Star(new Rect(cx - size / 2, cy - size / 2, size, size), lit);
        }

        // why
        string[] why =
        {
            $"En {par} coups ou moins ({moves} coups)",
            $"Plus de 40 % du temps restant ({Mathf.RoundToInt(100 * time / baseTime)} %)",
            "Sans bonus ni pub",
        };
        for (int i = 0; i < 3; i++)
        {
            var row = new Rect(card.x + 50, card.y + 470 + i * 105, card.width - 100, 90);
            Panel(row, criteria[i] ? new Color(.85f, .95f, .85f) : Mist);
            Star(new Rect(row.x + 20, row.y + 15, 60, 60), criteria[i]);
            Label(new Rect(row.x + 100, row.y, row.width - 120, row.height), why[i], 38, criteria[i] ? Ink : new Color(.45f, .5f, .6f), false, TextAnchor.MiddleLeft);
        }

        float y = card.y + 800;
        Panel(new Rect(card.center.x - 160, y, 320, 80), Mist);
        Panel(new Rect(card.center.x - 140, y + 15, 50, 50), Gold);
        Label(new Rect(card.center.x - 80, y, 220, 80), $"+{gained}", 48, Ink);
        if (unlockedMsg != null)
        {
            y += 95;
            Label(new Rect(card.x, y, card.width, 60), unlockedMsg, 40, Red, true);
        }

        var next = new Rect(card.x + 60, card.yMax - 330, card.width - 120, 150);
        bool last = lvl == LevelsPerWorld - 1;
        if (Button(next, last ? "Mondes" : "Suivant", Gold, 56))
        {
            if (last) OpenMenu(Menu.Worlds);
            else PlayLevel(world, lvl + 1);
        }
        float half = (card.width - 150) / 2;
        if (Button(new Rect(card.x + 60, card.yMax - 150, half, 110), "Rejouer", Mist, 42)) PlayLevel(world, lvl);
        if (Button(new Rect(card.x + 90 + half, card.yMax - 150, half, 110), "Niveaux", Mist, 42)) OpenMenu(Menu.Levels);
    }
}
