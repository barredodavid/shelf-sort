using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The level introducing an obstacle or a country's twist opens, every time, on a bubble per new thing: its name, a live 3D
// scene showing it (rendered off to the side by a second camera into a texture), what to do and what it pays. The
// level waits, clock stopped, until the last bubble is tapped away.
public partial class Game
{
    static readonly Vector3 StageO = new(3000, 0, 0); // where the bubble's scene is built, far from everything
    readonly List<(string key, string title, string text)> intros = new();
    float introAt; // when the current bubble opened: the events' clocks are pushed back by the time spent reading
    Camera stageCam;
    RenderTexture stageTex;
    Transform stage;

    // the level (country * LevelsPerWorld + level) that introduces each thing: a country's twist on its 6th level, the
    // obstacles on the first baked level holding them; found once by reading the levels through
    static Dictionary<string, int> introLevels;
    static int IntroLevel(string key)
    {
        if (introLevels == null)
        {
            introLevels = new() { ["pigeon"] = 5, ["riposo"] = LevelsPerWorld + 5, ["bento"] = 2 * LevelsPerWorld + 5, ["promo"] = 3 * LevelsPerWorld + 5 };
            for (int g = 0; g < Worlds.Length * LevelsPerWorld; g++)
            {
                var lv = LevelGen.Make(g + 1, new System.Random(0));
                var all = lv.Shelves.SelectMany(sh => sh).SelectMany(r => r).ToList();
                if (lv.Shelves.Any(sh => sh.Count > 1)) introLevels.TryAdd("back", g);
                if (all.Any(LevelGen.IsFrozen)) introLevels.TryAdd("ice", g);
                if (all.Any(LevelGen.IsBoxed)) introLevels.TryAdd("box", g);
                if (all.Contains(LevelGen.Crate)) introLevels.TryAdd("crate", g);
            }
        }
        return introLevels.TryGetValue(key, out int at) ? at : -1;
    }

    // called at the end of StartLevel: queues a bubble for each thing this level introduces (every time it is played)
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
                     ("pigeon", twist == 0, "LE PIGEON DE PARIS", Tf("Il se pose sur un produit. Touche-le vite pour le chasser (+{0} pièces), sinon il emporte le produit ailleurs !", PigeonCoins)),
                     ("riposo", twist == 1, "LA SIESTE ITALIENNE", Tf("Quand un rideau de fer descend à moitié, l'étagère va fermer {0} s. Fais vite un trio dessus : +{1} s au chrono !", SiestaTime, SiestaSeconds)),
                     ("bento", twist == 2, "LE PRODUIT CADENASSÉ", Tf("Un produit sous cadenas ne se prend pas. Le cadenas s'ouvre après {0} coups joués ailleurs : le chiffre dessus montre ceux qui restent.", BentoMoves)),
                     ("promo", twist == 3, "PROMO FLASH", Tf("Une étiquette -50 % se pose sur un produit : fais son trio avant la fin pour +{0} s au chrono. Raté : -{1} s !", SaleSeconds, SaleMissed)),
                 })
            if (has && !daily && world * LevelsPerWorld + lvl == IntroLevel(key))
                intros.Add((key, title, text));
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
        stage.GetChild(0).GetComponent<Renderer>().material = new Material(Fx.Lit) { color = WoodC };
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
                var tied = Put(product, 0);
                var wrapped = Add(Products.Padlock(), new Vector3(0, .11f, -.05f));
                wrapped.name = "bento";
                Products.TieLock(wrapped, Mathf.Max(.4f, TopOf(tied) - StageO.y - .11f + .06f));
                Put(other, -1.1f);
                Put(other, 1.1f);
                break;
            case "promo":
                var onSale = Put(product, 0);
                Add(Products.SaleTag(), new Vector3(.25f, TopOf(onSale) - StageO.y + .45f, -.45f)).name = "tag";
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
                    float p = Mathf.Repeat(t, 4.5f), down = p < 1.5f ? .55f * Mathf.SmoothStep(0, 1, p / .6f) // half down (the warning)
                        : p < 3.3f ? Mathf.Lerp(.55f, 1, Mathf.SmoothStep(0, 1, (p - 1.5f) / .45f)) // then shut
                        : 1 - Mathf.SmoothStep(0, 1, (p - 3.3f) / .6f); // then up again
                    PoseShutter(c, 1.9f, down, t);
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
        var tag = stage ? stage.Find("tag") : null;
        if (tag) // the sale tag's price cut, as in the level
        {
            var v = stageCam.WorldToViewportPoint(tag.Find("card").position);
            Label(new Rect(pic.x + v.x * pic.width - 80, pic.yMax - v.y * pic.height - 26, 160, 52), "-50%", 34, Red);
        }
        var bento = stage ? stage.Find("bento") : null;
        if (bento) // the moves left written on its padlock, counting down as moves are played
        {
            var v = stageCam.WorldToViewportPoint(bento.Find("lock").TransformPoint(new Vector3(0, .01f, -.08f)));
            var at = new Rect(pic.x + v.x * pic.width - 34, pic.yMax - v.y * pic.height - 26, 68, 52);
            Label(at, (BentoMoves - (int)Mathf.Repeat((Time.time - introAt) / 1.2f, BentoMoves)).ToString(), 30, Ink);
        }
        var shutter = stage ? stage.Find("shutter") : null;
        if (shutter && shutter.Find("bar/sign").localScale.x > .7f) // the shutter's sign, written on as in the level
        {
            var v = stageCam.WorldToViewportPoint(shutter.Find("bar/sign").TransformPoint(new Vector3(0, -.5f, -.03f)));
            var at = new Vector2(pic.x + v.x * pic.width, pic.yMax - v.y * pic.height);
            Label(new Rect(at.x - 150, at.y - 34, 300, 36), "TORNO SUBITO", 28, Ink);
            Label(new Rect(at.x - 150, at.y + 2, 300, 34), "4 s", 28, Red);
        }
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
