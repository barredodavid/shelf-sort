using UnityEngine;

// The sticker album (Collection.cs): a country's 12 products on 4 shelves, seen at the same angle as in a level.
// Stickers owned stand in full colour with their name; missing ones are dark silhouettes marked "?". In the editor
// a "Tout" button shows every product, to review them one by one.
public partial class Game
{
    int catalogWorld;
    bool catalogAll; // editor: every product, owned or not
    Material silhouette;
    readonly Vector3[] catalogPos = new Vector3[Products.Count];

    const float CatalogTop = 330; // GUI pixels under the safe area taken by the header and the album's progress

    bool Shown(int id) => catalogAll || Owned(catalogWorld, id);

    void RenderCatalog()
    {
        silhouette ??= new Material(Fx.Lit) { color = new Color(.22f, .24f, .3f) };
        float H = Screen.height / GuiScale, top = SafeTopPx() + CatalogTop, rowPx = (H - top - 60) / 4;
        for (int r = 0; r < 4; r++)
        {
            float gy = top + (r + .82f) * rowPx; // GUI y of the shelf's top
            float y = OnBoard(new Vector2(Screen.width / 2f, Screen.height - gy * GuiScale)).y;
            Box(new Vector3(0, y - .11f, 0), new Vector3(7.4f, .22f, 1.6f), WoodC);
            for (int c = 0; c < 3; c++)
            {
                int id = r * 3 + c;
                catalogPos[id] = new Vector3((c - 1) * 2.4f, y, 0);
                Wrap(Products.Make(catalogWorld, id), catalogPos[id], 1.15f, "catalog", Shown(id) ? null : silhouette);
            }
        }
    }

    void CatalogScreen(float W, float H, float safeTop)
    {
        float y = safeTop + 40;
        if (Button(new Rect(40, y, 250, 96), "Retour", Mist, 40)) OpenMenu(Menu.Home);

        // country switcher
        var title = new Rect(310, y, 460, 96);
        Panel(title, Worlds[catalogWorld].color);
        Label(title, Name(catalogWorld).ToUpper(), 50, Color.white, true);
        for (int d = -1; d <= 1; d += 2)
            if (Button(new Rect(d < 0 ? 790 : 920, y, 120, 96), d < 0 ? "<" : ">", Gold, 56))
            {
                catalogWorld = (catalogWorld + d + Worlds.Length) % Worlds.Length;
                Render();
            }

        // the album's progress, and what a full one pays
        int n = StickerCount(catalogWorld);
        bool full = n == Products.Count;
        var bar = new Rect(40, y + 120, W - 80, 90);
        Panel(bar, full ? Gold : Cream);
        Label(new Rect(bar.x + 30, bar.y, 400, bar.height), Tf("{0} / {1} autocollants", n, Products.Count), 38, Ink, false, TextAnchor.MiddleLeft);
        Label(new Rect(bar.center.x, bar.y, bar.width / 2 - 30, bar.height), full ? "Album complet !" : Tf("Complet : +{0} pièces", AlbumCoins), 34,
            full ? Red : new Color(.4f, .45f, .55f), false, TextAnchor.MiddleRight);
        if (Application.isEditor && Button(new Rect(W - 200, y + 230, 160, 70), catalogAll ? "Album" : "Tout", Mist, 30))
        {
            catalogAll = !catalogAll;
            Render();
        }

        // name under each sticker owned, "?" under the missing ones
        for (int id = 0; id < Products.Count; id++)
        {
            var p = ToGui(catalogPos[id] + new Vector3(0, -.2f, -.9f));
            var tag = new Rect(p.x - 150, p.y - 4, 300, 52);
            Panel(tag, Ink);
            Label(tag, Shown(id) ? ProductNames[catalogWorld][id] : "?", 30, Color.white); // names translated by Label
        }
    }
}
