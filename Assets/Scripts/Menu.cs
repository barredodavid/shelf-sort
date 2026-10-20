using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Title screen and pause menu. The title screen shows the logo over a little shelf playing the game by itself
// (HomeDemo.cs); "JOUER" leads to the map (Map.cs).
public partial class Game
{
    enum Menu { None, Home, Pause, Map, Catalog, World, Trophies }

    Menu menu = Menu.Home;
    float menuAt;

    // title-screen demo shelf (HomeDemo.cs)
    float heroCenterPx = -1; // GUI-canvas y where the display shelf sits, set by HomeScreen's layout

    bool Paused => menu != Menu.None || intros.Count > 0; // a menu, or the bubble explaining something new

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

    // ---------- title screen: GUI part ----------

    void HomeScreen(float W, float H, float k, float safeTop, float bottom)
    {
        // canopy across the whole width, running up under the status bar
        const float sw = 120, ah = 110;
        Awning(0, 0, 9, sw, safeTop + ah);
        float y = safeTop + ah + sw / 2 + 30;

        // top bar: a round sound button, the stars, the coins
        var sound = new Rect(40, y, 96, 96);
        if (Button(sound, "", Mist)) ToggleSound();
        soundIcons ??= new[] { Fx.Icon(5), Fx.Icon(4) };
        GUI.color = Ink;
        GUI.DrawTexture(new Rect(sound.x + 16, sound.y + 14, 64, 64), soundIcons[SoundOn ? 1 : 0]);
        GUI.color = Color.white;
        StarCount(156, y, TotalStars);
        CoinsPill(W - 290, y);
        DailyButton(new Rect(W - 290, y + 116, 250, 80));
        if (Button(new Rect(40, y + 116, 250, 80), "Collection", Mist, 34))
        {
            catalogWorld = world;
            OpenMenu(Menu.Catalog); // the sticker album
        }
        if (Button(new Rect(W / 2 - 160, y + 116, 320, 80), "Trophées", Mist, 34)) OpenMenu(Menu.Trophies);

        // logo, tilted like a hand-painted shop sign: drops in with a bounce, then bobs gently
        float ly = y + 215 + 8 * Mathf.Sin(Time.time * 2); // below the second row of buttons (Collection, Défi du jour)
        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(menuAt + .1f), new Vector2(W / 2 * k, (ly + 170) * k));
        GUIUtility.RotateAroundPivot(-4, new Vector2(W / 2 * k, (ly + 170) * k));
        Logo(new Rect(0, ly, W, 190), "SHELF", 185);
        Logo(new Rect(0, ly + 160, W, 190), "SORT", 185);
        GUI.matrix = keep;
        var tag = new Rect(W / 2 - 340, y + 590, 680, 80);
        Panel(tag, Ink);
        Label(tag, "LE TOUR DU MONDE DES RAYONS", 38, Color.white);

        // bottom block: where you are (country, its stars), then the play button with the next level on it
        var play = new Rect(60, bottom - 60 - 230, W - 120, 230);
        var country = new Rect(W / 2 - 320, play.y - 200, 640, 150);
        var c = Worlds[world].color;
        Panel(new Rect(country.x, country.y + 12, country.width, country.height), Darker(c));
        Panel(country, c);
        Label(new Rect(country.x, country.y + 8, country.width, 70), Worlds[world].name.ToUpper(), 54, Color.white, true);
        var bar = new Rect(country.x + 90, country.y + 92, country.width - 260, 30);
        Panel(bar, new Color(0, 0, 0, .25f));
        float filled = WorldStars(world) / (float)(LevelsPerWorld * 3);
        if (filled > 0) Panel(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * filled), bar.height), Gold);
        Star(new Rect(bar.xMax + 16, bar.y - 9, 48, 48), true);
        Label(new Rect(bar.xMax + 66, bar.y - 15, 110, 60), $"{WorldStars(world)}/{LevelsPerWorld * 3}", 30, Color.white, false, TextAnchor.MiddleLeft);
        // a little plane: tapping the card flies to the globe to change country
        planeIcon ??= Fx.Icon(3);
        GUI.DrawTexture(new Rect(country.x + 26, country.y + 22, 60, 60), planeIcon);
        if (GUI.Button(country, GUIContent.none, GUIStyle.none))
        {
            mapWorld = world;
            OpenWorldMap(-1);
        }

        float pulse = 1 + .03f * Mathf.Sin(Time.time * 4); // gently breathing call to action
        keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * pulse, play.center * k);
        if (Button(play, "", Gold)) OpenMenu(Menu.Map);
        Label(new Rect(play.x, play.y + 18, play.width, 130), "JOUER", 110, Ink);
        Label(new Rect(play.x, play.y + 142, play.width, 60), $"Niveau {world + 1}-{FirstToPlay(world) + 1}", 40, new Color(Ink.r, Ink.g, Ink.b, .7f));
        // a shine sweeping across every few seconds
        float t = Mathf.Repeat(Time.time, 3f);
        if (t < .7f)
        {
            GUI.BeginGroup(play);
            Panel(new Rect(Mathf.Lerp(-140, play.width + 40, t / .7f), 0, 70, play.height), new Color(1, 1, 1, .35f));
            GUI.EndGroup();
        }
        GUI.matrix = keep;

        heroCenterPx = (tag.yMax + country.y) / 2 + 40; // a little low: the flag and the monument stand on the shelf
    }

    Texture2D[] soundIcons; // muted, on
    Texture2D planeIcon;

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
        if (Button(new Rect(bx, card.y + 580, bw, 130), daily ? "Accueil" : "Carte", Mist, 46)) LeaveLevel();
        if (Button(new Rect(bx, card.y + 750, bw, 110), SoundOn ? "Son : oui" : "Son : non", Mist, 40)) ToggleSound();
        GUI.matrix = keep;
    }
}
