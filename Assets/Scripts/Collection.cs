using System.Linq;
using UnityEngine;

// Sticker album: each country's 12 products, collected one by one. Stickers come from chests (one for sure), from
// the daily challenge (one per win) and now and then from a level's first three-star win. A full album pays
// AlbumCoins once. The album is the catalog screen (Catalog.cs): stickers owned show in 3D, missing ones as dark
// silhouettes.
public partial class Game
{
    const int AlbumCoins = 300;
    const float StickerOnThreeStars = .35f;

    static readonly string[][] ProductNames =
    {
        new[] { "Baguette", "Croissant", "Macaron", "Éclair", "Vin", "Pain au chocolat", "Crème brûlée", "Canelés", "Camembert", "Saucisson", "Moutarde", "Confiture" },
        new[] { "Pizza", "Tomate", "Gelato", "Tiramisu", "Spaghetti", "Huile d'olive", "Mozzarella", "Parmesan", "Pesto", "Salami", "Panettone", "Espresso" },
        new[] { "Maki", "Nigiri", "Onigiri", "Ramen", "Dango", "Bento", "Matcha", "Takoyaki", "Tempura", "Gyoza", "Sauce soja", "Yakitori" },
        new[] { "Burger", "Frites", "Hot-dog", "Pop-corn", "Soda", "Milkshake", "Donut", "Pilon", "Ketchup", "Bagel", "Pancakes", "Beurre de cacahuète" },
    };

    static bool Owned(int w, int id) => PlayerPrefs.GetInt($"sticker{w}_{id}", 0) == 1;
    static int StickerCount(int w) => Enumerable.Range(0, Products.Count).Count(id => Owned(w, id));

    // a new sticker of country w, picked among the missing ones; the message says which (and pays a full album)
    string GiveSticker(int w)
    {
        var missing = Enumerable.Range(0, Products.Count).Where(id => !Owned(w, id)).ToList();
        if (missing.Count == 0) return null;
        int id = missing[Random.Range(0, missing.Count)];
        PlayerPrefs.SetInt($"sticker{w}_{id}", 1);
        string msg = $"Nouvel autocollant : {ProductNames[w][id]} !";
        if (missing.Count == 1 && PlayerPrefs.GetInt($"album{w}", 0) == 0) // that was the last one
        {
            PlayerPrefs.SetInt($"album{w}", 1);
            coins += AlbumCoins;
            PlayerPrefs.SetInt("coins", coins);
            msg = $"Album {Worlds[w].name} complet ! +{AlbumCoins} pièces";
        }
        PlayerPrefs.Save();
        CheckTrophies(); // a full album
        return msg;
    }
}
