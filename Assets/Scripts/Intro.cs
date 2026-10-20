using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The first level with an obstacle or a country's twist opens on a bubble per new thing: its name, a little live 3D
// scene showing it (rendered off to the side by a second camera into a texture), what to do and what it pays. The
// level waits, clock stopped, until the last bubble is tapped away. Each is shown once per device.
public partial class Game
{
    static readonly Vector3 StageO = new(3000, 0, 0); // where the bubble's scene is built, far from everything
    readonly List<(string key, string title, string text)> intros = new();
    float introAt; // when the current bubble opened: the events' clocks are pushed back by the time spent reading
    Camera stageCam;
    RenderTexture stageTex;
    Transform stage;

    // called at the end of StartLevel: queues a bubble for each new thing in this level
    void StartIntros()
    {
        intros.Clear();
        var all = shelves.SelectMany(sh => sh).SelectMany(r => r).ToList();
        foreach (var (key, has, title, text) in new[]
                 {
                     ("back", shelves.Any(sh => sh.Count > 1), "LE FOND DE L'ÉTAGÈRE", "Quand la rangée de devant est vide, les produits du fond avancent."),
                     ("ice", all.Any(LevelGen.IsFrozen), "PRODUIT GELÉ", "Il ne bouge pas. Pose 2 produits identiques à côté de lui : le trio le libère."),
                     ("box", all.Any(LevelGen.IsBoxed), "CARTON MYSTÈRE", "Attrape-le pour découvrir le produit caché dedans."),
                     ("crate", all.Contains(LevelGen.Crate), "CAISSE", "Elle ne bouge pas. Fais un trio sur une étagère voisine pour la casser."),
                     ("pigeon", twist == 0, "LE PIGEON DE PARIS", $"Il se pose sur un produit. Touche-le vite pour le chasser (+{PigeonCoins} pièces), sinon il emporte le produit ailleurs !"),
                     ("riposo", twist == 1, "LA SIESTE ITALIENNE", $"Quand un rideau de fer descend à moitié, l'étagère va fermer {SiestaTime:0} s. Fais vite un trio dessus (+{SiestaCoins} pièces) !"),
                     ("bento", twist == 2, "LE BENTO SCELLÉ", $"Un produit noué dans un tissu ne se prend pas : il s'ouvre après {BentoMoves} coups joués ailleurs (+{BentoCoins} pièces)."),
                     ("promo", twist == 3, "PROMO FLASH", $"Une étiquette -50 % se pose sur un produit : fais son trio avant qu'elle disparaisse (+{SaleCoins} pièces)."),
                 })
            if (has && PlayerPrefs.GetInt("bubble" + key, 0) == 0)
            {
                PlayerPrefs.SetInt("bubble" + key, 1);
                intros.Add((key, title, text));
            }
        introAt = Time.time;
        if (intros.Count > 0) BuildStage();
    }

    // the scene for the first bubble in the queue: a bit of shelf and products showing the thing at work
    void BuildStage()
    {
        if (stage) Destroy(stage.gameObject);
        stage = new GameObject("IntroStage").transform;
        stage.SetParent(root);
        stage.position = StageO;
        var all = shelves.SelectMany(sh => sh).SelectMany(r => r).ToList();
        var plain = all.Where(x => LevelGen.IsProduct(x) && !LevelGen.IsBoxed(x) && !LevelGen.IsFrozen(x)).ToList();
        int product = plain[0], other = plain.Where(x => LevelGen.Id(x) != LevelGen.Id(product)).DefaultIfEmpty(product).First();
        Transform Put(int id, float x, float scale = 1, bool ghost = false, float z = 0)
        {
            var t = Item(id, StageO + new Vector3(x, .11f, z), scale, ghost, "intro").transform;
            t.SetParent(stage, true);
            return t;
        }
        Transform Add(GameObject model, Vector3 at)
        {
            model.transform.SetParent(stage, false);
            model.transform.position = StageO + at;
            return model.transform;
        }
        Add(GameObject.CreatePrimitive(PrimitiveType.Cube), new Vector3(0, 0, 0)).localScale = new Vector3(3.4f, .22f, 1.6f); // the plank
        stage.GetChild(0).GetComponent<Renderer>().material.color = WoodC;
        Destroy(stage.GetChild(0).GetComponent<Collider>());
        switch (intros[0].key)
        {
            case "back":
                Put(product, -.55f);
                Put(other, -.55f, BackScale, true, .55f).position += new Vector3(0, BackLift, 0);
                Put(product, .55f, BackScale, true, .55f).position += new Vector3(0, BackLift, 0);
                break;
            case "ice":
                IceFrame(Put(all.First(LevelGen.IsFrozen), 0));
                int same = all.First(LevelGen.IsFrozen);
                Put(LevelGen.Id(same), -1.1f);
                Put(LevelGen.Id(same), 1.1f);
                break;
            case "box":
                Put(all.First(LevelGen.IsBoxed), 0);
                Put(product, -1.1f);
                Put(other, 1.1f);
                break;
            case "crate":
                Put(LevelGen.Crate, 0);
                Put(product, -1.1f);
                Put(other, 1.1f);
                break;
            case "pigeon":
                Put(product, 0);
                Add(Products.Pigeon(), new Vector3(0, 1.3f, 0)).name = "bird";
                Put(other, -1.1f);
                Put(other, 1.1f);
                break;
            case "riposo":
                for (int i = -1; i <= 1; i++) Put(i == 0 ? other : product, i * 1.1f);
                Add(Products.Shutter(3.35f, 1.9f), new Vector3(0, 2.01f, -.86f)).name = "shutter";
                break;
            case "bento":
                Put(product, 0);
                Add(Products.BentoWrap(), new Vector3(0, .11f, -.05f));
                Put(other, -1.1f);
                Put(other, 1.1f);
                break;
            case "promo":
                Put(product, 0);
                Add(Products.SaleTag(), new Vector3(.25f, 1.66f, -.45f)).name = "tag";
                Put(other, -1.1f);
                Put(other, 1.1f);
                break;
        }
        if (!stageCam)
        {
            stageTex = new RenderTexture(640, 400, 24) { antiAliasing = 4 };
            stageCam = new GameObject("StageCam").AddComponent<Camera>();
            stageCam.orthographic = true;
            stageCam.orthographicSize = 1.25f;
            stageCam.clearFlags = CameraClearFlags.SolidColor;
            stageCam.targetTexture = stageTex;
        }
        stageCam.backgroundColor = BackboardC;
        stageCam.transform.SetPositionAndRotation(StageO + Quaternion.Euler(CamTilt, 0, 0) * new Vector3(0, 0, -10) + new Vector3(0, .95f, 0), Quaternion.Euler(CamTilt, 0, 0));
        stageCam.enabled = true;
    }

    // called by Update while a bubble is up: the scene comes alive
    void AnimateStage()
    {
        if (!stage)
        {
            if (stageCam) stageCam.enabled = false; // nothing to film
            return;
        }
        float t = Time.time - introAt;
        foreach (Transform c in stage)
            switch (c.name)
            {
                case "bird": // pecking, then flapping as if about to take off
                    var head = c.Find("head");
                    if (head) head.localRotation = Quaternion.Euler(-35 * Mathf.Max(0, Mathf.Sin(t * 7)), 0, 0);
                    c.rotation = Quaternion.Euler(0, 25 + 25 * Mathf.Sin(t * .8f), 0);
                    if (Mathf.Repeat(t, 3) > 2) Flap(c);
                    break;
                case "shutter": // coming down to half, then all the way, then up again
                    float p = Mathf.Repeat(t, 4), down = p < 1.5f ? .45f * Mathf.SmoothStep(0, 1, p / .6f) : p < 3 ? 1 : 1 - (p - 3);
                    c.Find("curtain").localScale = new Vector3(1, 1.9f * Mathf.Max(.01f, down), 1);
                    c.Find("bar").localPosition = new Vector3(0, -1.9f * down, 0);
                    c.Find("bar/sign").localRotation = Quaternion.Euler(0, 0, 8 * Mathf.Sin(t * 3));
                    break;
                case "tag":
                    c.localRotation = Quaternion.Euler(0, 0, 10 * Mathf.Sin(t * 2.4f));
                    c.Find("card").GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", new Color(1, .8f, .1f) * (Mathf.Repeat(t * 2.5f, 1) < .5f ? .9f : .2f));
                    break;
            }
    }

    // the bubble: NOUVEAU, the name, the live scene, what to do; a tap anywhere goes to the next, or starts the level
    void IntroCard(float W, float H, float k)
    {
        Panel(new Rect(-100, -100, W + 200, H + 200), new Color(0, 0, 0, .55f));
        var keep = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * PopIn(introAt), new Vector2(W / 2 * k, H / 2 * k));
        var (_, title, body) = intros[0];
        var card = new Rect(80, H / 2 - 520, W - 160, 1000);
        Panel(new Rect(card.x, card.y + 18, card.width, card.height), Darker(Cream));
        Panel(card, Cream);
        var badge = new Rect(card.center.x - 150, card.y - 42, 300, 84);
        Panel(badge, Red);
        Label(badge, "NOUVEAU !", 44, Color.white, true);
        Label(new Rect(card.x, card.y + 70, card.width, 90), title, 60, Red, true);
        var pic = new Rect(card.x + 60, card.y + 180, card.width - 120, (card.width - 120) * 400 / 640);
        Panel(new Rect(pic.x - 10, pic.y - 10, pic.width + 20, pic.height + 20), Darker(Cream));
        if (stageTex) GUI.DrawTexture(pic, stageTex);
        Label(new Rect(card.x + 60, pic.yMax + 30, card.width - 120, card.yMax - pic.yMax - 60), body, 40, Ink);
        GUI.matrix = keep;
        Label(new Rect(0, card.yMax + 50, W, 70), intros.Count > 1 ? "Touche pour continuer" : "Touche pour jouer", 44,
            new Color(1, 1, 1, .6f + .4f * Mathf.Sin(Time.time * 4)), true);
        if (Time.time > introAt + .5f && GUI.Button(new Rect(-100, -100, W + 200, H + 200), GUIContent.none, GUIStyle.none))
        {
            intros.RemoveAt(0);
            float read = Time.time - introAt; // the twists' clocks start from now, not from while the player read
            nextPigeon += read;
            nextShut += read;
            nextSale += read;
            introAt = Time.time;
            Fx.Play("pick");
            if (intros.Count > 0) BuildStage();
            else
            {
                Destroy(stage.gameObject);
                stageCam.enabled = false;
            }
        }
    }
}
