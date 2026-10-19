using UnityEngine;

// Editor-only catalog: a country's 12 products on 4 shelves, numbered, seen at the same angle as in a level,
// to review them one by one. Opened from the title screen's "Catalogue" button.
public partial class Game
{
    int catalogWorld;
    readonly Vector3[] catalogPos = new Vector3[Products.Count];
    readonly string[] catalogNames = new string[Products.Count];

    const float CatalogTop = 260; // GUI pixels under the safe area taken by the header

    void RenderCatalog()
    {
        float H = Screen.height / GuiScale, top = SafeTopPx() + CatalogTop, rowPx = (H - top - 60) / 4;
        for (int r = 0; r < 4; r++)
        {
            float gy = top + (r + .82f) * rowPx; // GUI y of the shelf's top
            float y = OnBoard(new Vector2(Screen.width / 2f, Screen.height - gy * GuiScale)).y;
            Box(new Vector3(0, y - .11f, 0), new Vector3(7.4f, .22f, 1.6f), WoodC);
            for (int c = 0; c < 3; c++)
            {
                int id = r * 3 + c;
                var model = Products.Make(catalogWorld, id);
                catalogNames[id] = model.name;
                catalogPos[id] = new Vector3((c - 1) * 2.4f, y, 0);
                Wrap(model, catalogPos[id], 1.15f, "catalog", null);
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
        Label(title, Worlds[catalogWorld].name.ToUpper(), 50, Color.white, true);
        for (int d = -1; d <= 1; d += 2)
            if (Button(new Rect(d < 0 ? 790 : 920, y, 120, 96), d < 0 ? "<" : ">", Gold, 56))
            {
                catalogWorld = (catalogWorld + d + Worlds.Length) % Worlds.Length;
                Render();
            }

        // number and name under each product
        for (int id = 0; id < Products.Count; id++)
        {
            var p = ToGui(catalogPos[id] + new Vector3(0, -.2f, -.9f));
            var tag = new Rect(p.x - 150, p.y - 4, 300, 52);
            Panel(tag, Ink);
            Label(tag, $"{id + 1}. {catalogNames[id]}", 30, Color.white);
        }
    }
}
