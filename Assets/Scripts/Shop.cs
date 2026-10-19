using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Meta progression: coins renovate the shop. Each upgrade is a one-off purchase that restyles part of the scene,
// so progress is visible every time the player opens the game.
public partial class Game
{
    static readonly (string name, int cost)[] Upgrades =
    {
        ("Carrelage neuf", 30),
        ("Étagères en noyer", 60),
        ("Fond pastel", 100),
        ("Étiquettes fluo", 150),
        ("Store menthe", 220),
        ("Éclairage doré", 300),
    };

    Light sun;
    ColorAdjustments colorAdj;
    bool shopOpen;
    float shopAt;

    static bool Owned(int u) => PlayerPrefs.GetInt("up" + u, 0) == 1;
    bool CanBuyAny => Enumerable.Range(0, Upgrades.Length).Any(u => !Owned(u) && coins >= Upgrades[u].cost);

    // ---------- what each upgrade changes (read by the scene builders) ----------

    (Color low, Color high) WallColors => Owned(0)
        ? (new Color(.92f, .66f, .55f), new Color(1f, .93f, .84f))
        : (new Color(.36f, .62f, .7f), new Color(.66f, .87f, .9f));
    Color WoodC => Owned(1) ? new Color(.47f, .29f, .19f) : Wood;
    Color BackboardC => Owned(2) ? new Color(1f, .82f, .85f) : Backboard;
    Color TagC => Owned(3) ? new Color(1f, .25f, .7f) : Gold;
    Color AwningC => Owned(4) ? new Color(.3f, .78f, .62f) : Red;

    void ApplyLighting()
    {
        bool golden = Owned(5);
        sun.color = golden ? new Color(1f, .87f, .68f) : new Color(1f, .96f, .9f);
        sun.intensity = golden ? 1.25f : 1.1f;
        RenderSettings.ambientLight = golden ? new Color(.68f, .62f, .56f) : new Color(.58f, .6f, .64f);
        colorAdj.postExposure.value = golden ? .25f : .15f;
    }

    // ---------- buying ----------

    void OpenShop()
    {
        shopOpen = true;
        shopAt = Time.time;
        Fx.Play("pick");
    }

    void Buy(int u)
    {
        coins -= Upgrades[u].cost;
        PlayerPrefs.SetInt("up" + u, 1);
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.Save();
        BakeWall();
        ApplyLighting();
        Render();
        Fx.Play("win");
        Fx.Burst(new Vector3(0, 0, -2), 50);
    }

    // ---------- UI ----------

    void ShopCard(float W, float H, float k)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .55f));

        float a = Mathf.Clamp01((Time.time - shopAt) / .25f) - 1; // same pop-in as the end card
        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * (1 + 2.7f * a * a * a + 1.7f * a * a), new Vector2(W / 2 * k, H / 2 * k));

        int done = Enumerable.Range(0, Upgrades.Length).Count(Owned);
        const float rowH = 130, rowGap = 16;
        float cardH = 300 + Upgrades.Length * (rowH + rowGap) + 190;
        var card = new Rect(60, H / 2 - cardH / 2, W - 120, cardH);
        Panel(new Rect(card.x, card.y + 18, card.width, card.height), Darker(Cream));
        Panel(card, Cream);
        Label(new Rect(card.x, card.y + 40, card.width, 110), "RÉNOVATION", 84, Red, true);
        Label(new Rect(card.x, card.y + 150, card.width, 60), $"Supérette rénovée {done}/{Upgrades.Length}", 44, Ink);

        // coins left
        Panel(new Rect(card.x + card.width / 2 - 110, card.y + 215, 220, 70), Mist);
        Panel(new Rect(card.x + card.width / 2 - 95, card.y + 227, 46, 46), Gold);
        Label(new Rect(card.x + card.width / 2 - 40, card.y + 215, 140, 70), coins.ToString(), 42, Ink);

        for (int u = 0; u < Upgrades.Length; u++)
        {
            var row = new Rect(card.x + 40, card.y + 300 + u * (rowH + rowGap), card.width - 80, rowH);
            bool owned = Owned(u), afford = coins >= Upgrades[u].cost;
            Panel(row, owned ? new Color(.82f, .93f, .82f) : Mist);
            Label(new Rect(row.x + 36, row.y, row.width - 330, rowH), Upgrades[u].name, 46, Ink, false, TextAnchor.MiddleLeft);

            var btn = new Rect(row.xMax - 270, row.y + 18, 250, rowH - 40);
            if (owned)
                Label(btn, "Fait", 46, new Color(.25f, .55f, .3f));
            else
            {
                GUI.enabled = afford;
                if (Button(btn, $"{Upgrades[u].cost}", Gold, 46)) Buy(u);
                GUI.enabled = true;
                Panel(new Rect(btn.x + 22, btn.y + (btn.height - 40) / 2, 40, 40), Darker(Gold)); // coin glyph
            }
        }

        if (Button(new Rect(card.x + 80, card.yMax - 170, card.width - 160, 130), "Fermer", Gold, 50))
            shopOpen = false;
        GUI.matrix = keep;
    }
}
