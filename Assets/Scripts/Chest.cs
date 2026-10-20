using System.Linq;
using UnityEngine;

// A chest after each country's 5th, 10th and 15th level (the HARD ones; the 20th unveils the monument instead):
// winning one sends the player back to a map where that shop's 3D chest (Products.Chest) swells to centre stage and
// shakes; a tap throws its lid back and pays a random sum of coins (now and then a jackpot). Until opened, the chest
// sits beside its shop: grey while the level is still to win, bouncing once it is won (a tap opens it).
public partial class Game
{
    float chestAt = -1, chestOpenedAt = -1; // chestAt > 0: the chest card is up
    int chestCoins, chestLvl;
    string chestSticker; // the sticker found in it (Collection.cs), if any were missing
    Vector3 chestHome; // where the chest on show stood on the map
    bool chestStage; // chestHome is set
    bool chestWon; // set by ScoreLevel on a win of a chest level: the next map visit offers it (not a mere quit)

    static bool HasChest(int l) => (l + 1) % 5 == 0 && l < LevelsPerWorld - 1;
    static bool ChestOpened(int w, int l) => PlayerPrefs.GetInt($"chest{w}_{l}", 0) == 1;

    // called by EnterMap: a chest level just won puts the chest up
    void OfferChest()
    {
        bool won = chestWon;
        chestWon = false;
        if (won && HasChest(lvl) && !ChestOpened(world, lvl)) ShowChest(lvl);
    }

    void OpenChest()
    {
        float r = Random.value;
        chestCoins = r < .1f ? 200 : r < .4f ? 80 : 50;
        coins += chestCoins;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.SetInt($"chest{mapWorld}_{chestLvl}", 1);
        PlayerPrefs.Save();
        chestSticker = GiveSticker(mapWorld);
        chestOpenedAt = Time.time;
        foreach (var (chest, l) in chests3D) // a burst of sparkles out of it
            if (l == chestLvl) Fx.Burst(chest.position + Vector3.up * 1.5f - cam.transform.forward, 30);
        Fx.Play("win");
    }

    // the chest card over the map: the 3D chest itself grows by its shop and wobbles (UpdateChest); here only the
    // words, the tap that opens it and, once open, the coins won and the button that puts it away
    void ChestCard(float W, float H, float k)
    {
        bool open = chestOpenedAt > 0;
        var keep = GUI.matrix;
        // the title on a dark ribbon, readable over the busy map
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(chestAt), new Vector2(W / 2 * k, H * .25f * k));
        var ribbon = new Rect(W / 2 - 380, H * .25f - 110, 760, open ? 170 : 230);
        Panel(new Rect(ribbon.x, ribbon.y + 14, ribbon.width, ribbon.height), Darker(Ink));
        Panel(ribbon, Ink);
        Label(new Rect(ribbon.x, ribbon.y + 18, ribbon.width, 130), open ? (chestCoins >= 200 ? "JACKPOT !" : "BRAVO !") : "UN COFFRE !", 96, Gold, true);
        if (!open) Label(new Rect(ribbon.x, ribbon.y + 145, ribbon.width, 60), "Pour avoir battu un niveau DUR", 38, Color.white);
        GUI.matrix = keep;
        if (!open)
        {
            var hint = new Rect(W / 2 - 280, H * .72f, 560, 90);
            Panel(hint, new Color(Ink.r, Ink.g, Ink.b, .85f));
            Label(hint, "Touche pour l'ouvrir", 42, new Color(1, 1, 1, .7f + .3f * Mathf.Sin(Time.time * 4)));
            if (GUI.Button(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none) && Time.time > chestAt + .4f) OpenChest(); // not the very tap that brought it up
            return;
        }
        // what was in it, on a card under the chest
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(chestOpenedAt + .3f), new Vector2(W / 2 * k, H * .72f * k));
        var loot = new Rect(W / 2 - 400, H * .66f, 800, chestSticker != null ? 190 : 140);
        Panel(new Rect(loot.x, loot.y + 14, loot.width, loot.height), Darker(Ink));
        Panel(loot, Ink);
        Label(new Rect(loot.x, loot.y + 14, loot.width, 110), $"+{chestCoins} pièces", 80, Gold, true);
        if (chestSticker != null) Label(new Rect(loot.x, loot.y + 118, loot.width, 56), chestSticker, 36, Color.white);
        GUI.matrix = keep;
        if (Time.time > chestOpenedAt + .8f && Button(new Rect(W / 2 - 220, H * .66f + 230, 440, 130), "Super !", Gold, 56))
        {
            chestAt = -1;
            foreach (var (chest, l) in chests3D.Where(c => c.l == chestLvl).ToList()) // its model on the map goes
            {
                Destroy(chest.gameObject);
                chests3D.Remove((chest, l));
            }
        }
    }

    // called by UpdateMap while the card is up: the chest swells to centre stage, shakes now and then like something's
    // inside, and once tapped throws its lid back
    void UpdateChest()
    {
        foreach (var (chest, l) in chests3D)
        {
            if (l != chestLvl) continue;
            var home = new Vector3(chest.position.x, .02f, chest.position.z); // its spot by the shop
            if (chestStage) home = chestHome;
            else { chestHome = home; chestStage = true; }
            // it rises from there to the middle of the screen, in front of everything (an ortho camera: nearer, same size)
            var stage = cam.ViewportToWorldPoint(new Vector3(.5f, .45f, Vector3.Dot(home - cam.transform.position, cam.transform.forward) - 4));
            chest.position = Vector3.Lerp(home, stage, Mathf.SmoothStep(0, 1, (Time.time - chestAt) / .4f));
            chest.localScale = Vector3.one * .5f * Mathf.Lerp(1, 5, Mathf.Clamp01(PopIn(chestAt)));
            bool shake = chestOpenedAt < 0 && Mathf.Repeat(Time.time - chestAt, 1.4f) > .9f;
            chest.localRotation = Quaternion.Euler(-18, -28, shake ? 6 * Mathf.Sin(Time.time * 40) : 0); // three-quarters, tipped to show inside
            var lid = chest.Find("lid");
            if (lid) lid.localRotation = Quaternion.Euler(chestOpenedAt < 0 ? 0 : 115 * Mathf.Clamp01((Time.time - chestOpenedAt) / .25f), 0, 0);
        }
    }

    // a tapped or just-won chest takes the stage: the map scrolls to it
    void ShowChest(int l)
    {
        chestAt = Time.time;
        chestOpenedAt = -1;
        chestLvl = l;
        scrollZ = CenterZ(mapWorld * LevelsPerWorld + l);
        chestStage = false;
    }

    // a flat, square-cornered block of colour (Panel's rounded corners need at least 60 px)
    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
