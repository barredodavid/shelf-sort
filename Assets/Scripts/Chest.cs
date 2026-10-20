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
        Fx.Play("win");
    }

    // the chest card over the map: the 3D chest itself grows by its shop and wobbles (UpdateChest); here only the
    // words, the tap that opens it and, once open, the coins won and the button that puts it away
    void ChestCard(float W, float H, float k)
    {
        bool open = chestOpenedAt > 0;
        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(chestAt), new Vector2(W / 2 * k, H * .25f * k));
        Label(new Rect(0, H * .25f - 90, W, 130), open ? (chestCoins >= 200 ? "JACKPOT !" : "BRAVO !") : "UN COFFRE !", 100, Gold, true);
        if (!open) Label(new Rect(0, H * .25f + 30, W, 60), "Pour avoir battu un niveau DUR", 40, Color.white, true);
        GUI.matrix = keep;
        if (!open)
        {
            Label(new Rect(0, H * .72f, W, 60), "Touche pour l'ouvrir", 44, Color.white, true);
            if (GUI.Button(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none) && Time.time > chestAt + .4f) OpenChest(); // not the very tap that brought it up
            return;
        }
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(chestOpenedAt + .3f), new Vector2(W / 2 * k, H * .72f * k));
        Label(new Rect(0, H * .68f, W, 110), $"+{chestCoins} pièces", 84, Color.white, true);
        if (chestSticker != null) Label(new Rect(0, H * .68f + 100, W, 60), chestSticker, 38, Gold, true);
        GUI.matrix = keep;
        if (Time.time > chestOpenedAt + .8f && Button(new Rect(W / 2 - 220, H * .68f + 190, 440, 130), "Super !", Gold, 56))
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
            chest.localRotation = Quaternion.Euler(0, 0, shake ? 6 * Mathf.Sin(Time.time * 40) : 0);
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
