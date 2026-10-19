using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Title screen and pause menu. The title screen shows the logo over a little display shelf of hopping
// products (one per country); "JOUER" leads to the map (Map.cs).
public partial class Game
{
    enum Menu { None, Home, Pause, Map, Catalog }

    Menu menu = Menu.Home;
    float menuAt;

    // title-screen display shelf
    Transform heroRoot;
    readonly List<Transform> heroes = new();
    float heroCenterPx = -1; // GUI-canvas y where the display shelf sits, set by HomeScreen's layout
    // one product per country: croissant, pizza, maki, burger, dango
    static readonly (int w, int id)[] Heroes = { (0, 1), (1, 0), (2, 0), (3, 0), (2, 4) };

    bool Paused => shopOpen || menu != Menu.None;

    static bool SoundOn => PlayerPrefs.GetInt("sound", 1) == 1;

    void ApplySound() => AudioListener.volume = SoundOn ? 1 : 0;

    void ToggleSound()
    {
        PlayerPrefs.SetInt("sound", SoundOn ? 0 : 1);
        PlayerPrefs.Save();
        ApplySound();
        Fx.Play("pick");
    }

    void OpenMenu(Menu m)
    {
        menu = m;
        menuAt = Time.time;
        Fx.Play("pick");
        if (m == Menu.Map) EnterMap();
        if (m != Menu.Pause) Render(); // full screens replace the board (pause overlays it)
    }

    // ---------- title screen: 3D part ----------

    float HeroWorldY()
    {
        float px = heroCenterPx > 0 ? heroCenterPx : Screen.height / GuiScale * .55f;
        return OnBoard(new Vector2(Screen.width / 2f, Screen.height - px * GuiScale)).y - .7f; // .7: shelf under the products' middle
    }

    void RenderHome()
    {
        heroRoot = new GameObject("Hero").transform;
        heroRoot.SetParent(root);
        Box(new Vector3(0, -.11f, 0), new Vector3(7.6f, .22f, 1.6f), WoodC).transform.SetParent(heroRoot);
        Box(new Vector3(0, -.13f, -.81f), new Vector3(7.6f, .16f, .02f), Cream).transform.SetParent(heroRoot);
        for (int i = 0; i < Heroes.Length; i++)
        {
            var t = Wrap(Products.Make(Heroes[i].w, Heroes[i].id), new Vector3((i - 2) * 1.45f, 0, 0), 1.3f, "hero", null).transform;
            t.SetParent(heroRoot, false);
            heroes.Add(t);
        }
        AnimateHome();
    }

    // products hop one after the other and sway a little
    void AnimateHome()
    {
        if (menu != Menu.Home || !heroRoot) return;
        heroRoot.position = new Vector3(0, HeroWorldY(), 0);
        for (int i = 0; i < heroes.Count; i++)
        {
            float hop = Mathf.Max(0, Mathf.Sin(Time.time * 3f - i * .7f));
            heroes[i].localPosition = new Vector3((i - 2) * 1.45f, .35f * hop * hop, 0);
            heroes[i].localRotation = Quaternion.Euler(0, 14 * Mathf.Sin(Time.time * 1.3f + i), 0);
        }
    }

    // ---------- title screen: GUI part ----------

    void HomeScreen(float W, float H, float k, float safeTop, float bottom)
    {
        // canopy across the whole width, running up under the status bar
        const float sw = 120, ah = 110;
        Awning(0, 0, 9, sw, safeTop + ah);
        float y = safeTop + ah + sw / 2 + 30;

        if (Button(new Rect(40, y, 250, 96), SoundOn ? "Son : oui" : "Son : non", Mist, 38)) ToggleSound();
        if (Application.isEditor && Button(new Rect(40, y + 116, 250, 80), "Catalogue", Mist, 34)) OpenMenu(Menu.Catalog); // editor only: review every product
        CoinsButton(W - 290, y, true);

        // logo, tilted like a hand-painted shop sign
        float ly = y + 140;
        var keep = GUI.matrix;
        GUIUtility.RotateAroundPivot(-4, new Vector2(W / 2 * k, (ly + 170) * k));
        Logo(new Rect(0, ly, W, 190), "SHELF", 200);
        Logo(new Rect(0, ly + 170, W, 190), "SORT", 200);
        GUI.matrix = keep;
        var tag = new Rect(W / 2 - 340, ly + 390, 680, 80);
        Panel(tag, Ink);
        Label(tag, "LE TOUR DU MONDE DES RAYONS", 38, Color.white);

        // bottom block, from the bottom up: renovation progress, renovation, stars, play
        int done = Enumerable.Range(0, Upgrades.Length).Count(Owned);
        var prog = new Rect(160, bottom - 30, W - 320, 30);
        Panel(prog, new Color(0, 0, 0, .2f));
        Panel(new Rect(prog.x, prog.y, Mathf.Max(30, prog.width * done / Upgrades.Length), prog.height), AwningC);
        Label(new Rect(0, prog.y - 60, W, 50), $"Supérette rénovée {done}/{Upgrades.Length}", 36, Ink);

        var renov = new Rect(160, prog.y - 90 - 120, W - 320, 120);
        if (Button(renov, "Rénovation", Mist, 50)) OpenShop();
        if (CanBuyAny) Badge(renov.xMax - 44, renov.y - 22, "!");

        var play = new Rect(60, renov.y - 50 - 210, W - 120, 210);
        float starsY = play.y - 120;
        StarCount(W / 2 - 125, starsY, TotalStars);

        float pulse = 1 + .03f * Mathf.Sin(Time.time * 4); // gently breathing call to action
        keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * pulse, play.center * k);
        if (Button(play, "JOUER", Gold, 110)) OpenMenu(Menu.Map);
        GUI.matrix = keep;

        heroCenterPx = (tag.yMax + starsY) / 2;
    }

    // big title text: white outline, soft drop shadow, red fill
    void Logo(Rect r, string s, int size)
    {
        Label(new Rect(r.x, r.y + 18, r.width, r.height), s, size, new Color(0, 0, 0, .25f));
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6;
            Label(new Rect(r.x + Mathf.Cos(a) * 11, r.y + Mathf.Sin(a) * 11, r.width, r.height), s, size, Color.white);
        }
        Label(r, s, size, Red);
    }

    void PauseCard(float W, float H, float k)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .55f));

        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(menuAt), new Vector2(W / 2 * k, H / 2 * k));

        var card = new Rect(90, H / 2 - 450, W - 180, 900);
        Panel(new Rect(card.x, card.y + 18, card.width, card.height), Darker(Cream));
        Panel(card, Cream);
        Label(new Rect(card.x, card.y + 50, card.width, 130), "PAUSE", 100, Red, true);

        float bx = card.x + 70, bw = card.width - 140;
        if (Button(new Rect(bx, card.y + 230, bw, 140), "Reprendre", Gold, 54)) menu = Menu.None;
        if (Button(new Rect(bx, card.y + 410, bw, 130), "Recommencer", Mist, 46)) { StartLevel(); menu = Menu.None; }
        if (Button(new Rect(bx, card.y + 580, bw, 130), "Carte", Mist, 46)) OpenMenu(Menu.Map);
        if (Button(new Rect(bx, card.y + 750, bw, 110), SoundOn ? "Son : oui" : "Son : non", Mist, 40)) ToggleSound();
        GUI.matrix = keep;
    }
}
