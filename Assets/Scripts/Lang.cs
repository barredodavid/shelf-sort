using System.Collections.Generic;
using UnityEngine;

// English version. The game is written in French; T() turns a French text into English when the game speaks English.
// Label (so Button, Toast...) passes every text through T, so plain texts need nothing; texts built with numbers use
// Tf("... {0} ...", n), the French template being the key. The language follows the phone's, and can be switched on the
// title screen (PlayerPrefs "lang"). A text missing from the table stays French.
public partial class Game
{
    static bool English => PlayerPrefs.GetString("lang", Application.systemLanguage == SystemLanguage.French ? "fr" : "en") == "en";

    void ToggleLanguage()
    {
        PlayerPrefs.SetString("lang", English ? "fr" : "en");
        PlayerPrefs.Save();
        Fx.Play("pick");
        Render(); // a few words are drawn in 3D scenes' textures
    }

    static string T(string fr) => fr != null && English && En.TryGetValue(fr, out var en) ? en : fr;
    static string Tf(string fr, params object[] args) => string.Format(T(fr), args);

    // a country's name in the game's language
    static string Name(int w) => T(Worlds[w].name);

    static readonly Dictionary<string, string> En = new()
    {
        // countries, monuments
        ["France"] = "France", ["Italie"] = "Italy", ["Japon"] = "Japan", ["États-Unis"] = "USA",
        ["TOUR EIFFEL"] = "EIFFEL TOWER", ["COLISÉE"] = "COLOSSEUM", ["MONT FUJI"] = "MOUNT FUJI", ["NEW YORK"] = "NEW YORK",
        // title screen, menus
        ["Collection"] = "Collection", ["Trophées"] = "Trophies", ["LE TOUR DU MONDE DES RAYONS"] = "A WORLD TOUR OF THE AISLES",
        ["JOUER"] = "PLAY", ["Niveau {0}-{1}"] = "Level {0}-{1}", ["Défi du jour"] = "Daily challenge", ["Défi réussi"] = "Challenge done",
        ["PAUSE"] = "PAUSE", ["Reprendre"] = "Resume", ["Recommencer"] = "Restart", ["Accueil"] = "Home", ["Carte"] = "Map",
        ["Son : oui"] = "Sound: on", ["Son : non"] = "Sound: off", ["Retour"] = "Back", ["Monde"] = "World",
        // map
        ["PAYS {0}"] = "COUNTRY {0}", ["À DÉBLOQUER"] = "TO UNLOCK", ["Finis les {0} niveaux"] = "Finish the {0} levels",
        ["PAYS TERMINÉ !"] = "COUNTRY DONE!", ["+{0} pièces"] = "+{0} coins", ["DUR"] = "HARD", ["À TOI !"] = "YOUR TURN!",
        ["JOUER  {0}-{1}"] = "PLAY  {0}-{1}", ["VERS {0}"] = "TO {0}",
        // world map
        ["Choisis ta destination"] = "Choose your destination", ["{0} pour ouvrir"] = "{0} to open", ["ICI"] = "HERE",
        ["Direction : {0}"] = "Heading to {0}", ["LE MONDE"] = "THE WORLD",
        // level
        ["NIVEAU {0}"] = "LEVEL {0}", ["NIVEAU {0} · HARD"] = "LEVEL {0} · HARD", ["DÉFI DU JOUR"] = "DAILY CHALLENGE",
        ["{0} / {1} coups"] = "{0} / {1} moves", ["Annuler"] = "Undo", ["Mélanger"] = "Shuffle",
        ["{0} · OUVERT 24/24"] = "{0} · OPEN 24/7", ["NIVEAU DIFFICILE !"] = "HARD LEVEL!", ["PRODUIT DORÉ !"] = "GOLDEN PRODUCT!",
        ["Temps écoulé !"] = "Time's up!", ["Plus de place !"] = "No room left!", ["Tu y étais presque !"] = "You almost had it!",
        ["Fais un trio à côté !"] = "Make a trio next to it!", ["Glacé !"] = "Frozen!",
        ["SUPER ! x2"] = "SUPER! x2", ["GÉNIAL ! x3"] = "GREAT! x3", ["INCROYABLE ! x{0}"] = "AMAZING! x{0}", ["+{0} PIÈCES !"] = "+{0} COINS!",
        ["Pub : +15 secondes"] = "Ad: +15 seconds", ["Pub : annuler 2 coups"] = "Ad: undo 2 moves",
        ["Pas de pub disponible"] = "No ad available",
        // end of level
        ["PARFAIT !"] = "PERFECT!", ["BRAVO !"] = "WELL DONE!", ["En {0} coups ou moins ({1} coups)"] = "In {0} moves or fewer ({1} moves)",
        ["Plus de 40 % du temps restant ({0} %)"] = "Over 40% of the time left ({0}%)", ["Sans bonus ni pub"] = "No bonus, no ad",
        ["Suivant"] = "Next", ["Rejouer"] = "Replay", ["Nouveau pays débloqué : {0} !"] = "New country unlocked: {0}!",
        // daily challenge
        ["CHRONO SERRÉ"] = "TIGHT CLOCK", ["TOUT EN CARTONS"] = "ALL IN BOXES", ["SANS BONUS"] = "NO BONUS", ["TOUT EN OR"] = "ALL GOLD",
        ["Série : {0} jours de suite !"] = "Streak: {0} days in a row!", ["Reviens demain pour une série !"] = "Come back tomorrow for a streak!",
        // chest
        ["JACKPOT !"] = "JACKPOT!", ["UN COFFRE !"] = "A CHEST!", ["Pour avoir battu un niveau DUR"] = "For beating a HARD level",
        ["Touche pour l'ouvrir"] = "Tap to open it", ["Super !"] = "Great!",
        // album
        ["{0} / {1} autocollants"] = "{0} / {1} stickers", ["Album complet !"] = "Album complete!", ["Complet : +{0} pièces"] = "Complete: +{0} coins",
        ["Nouvel autocollant : {0} !"] = "New sticker: {0}!", ["Album {0} complet ! +{1} pièces"] = "{0} album complete! +{1} coins",
        ["Album"] = "Album", ["Tout"] = "All",
        // country twists
        ["RIPOSO !"] = "RIPOSO!", ["Ça va fermer !"] = "Closing soon!", ["PROMO RATÉE ! -{0} s"] = "SALE MISSED! -{0} s",
        ["PROMO FLASH !"] = "FLASH SALE!", ["Ouste ! +{0}"] = "Shoo! +{0}", ["Le pigeon l'a emporté !"] = "The pigeon took it!",
        ["Fermé pour la sieste !"] = "Closed for the siesta!", ["Cadenassé : encore {0} coup(s)"] = "Locked: {0} more move(s)",
        ["CADENAS OUVERT ! +{0} s"] = "PADLOCK OPEN! +{0} s", ["JUSTE À TEMPS ! +{0} s"] = "JUST IN TIME! +{0} s", ["PROMO ! +{0} s"] = "SALE! +{0} s",
        // bubbles
        ["NOUVEAU !"] = "NEW!", ["Touche pour continuer"] = "Tap to continue", ["Touche pour jouer"] = "Tap to play",
        ["LE FOND DE L'ÉTAGÈRE"] = "THE BACK OF THE SHELF", ["Quand la rangée de devant est vide, les produits du fond avancent."] = "When the front row is empty, the products at the back move up.",
        ["PRODUIT GELÉ"] = "FROZEN PRODUCT", ["Il ne bouge pas. Pose 2 produits identiques à côté de lui : le trio le libère."] = "It can't move. Put 2 matching products next to it: the trio frees it.",
        ["CARTON MYSTÈRE"] = "MYSTERY BOX", ["Attrape-le pour découvrir le produit caché dedans."] = "Pick it up to find out which product is inside.",
        ["CAISSE"] = "CRATE", ["Elle ne bouge pas. Fais un trio sur une étagère voisine pour la casser."] = "It can't move. Make a trio on a shelf next to it to break it.",
        ["LE PIGEON DE PARIS"] = "THE PARIS PIGEON", ["Il se pose sur un produit. Touche-le vite pour le chasser (+{0} pièces), sinon il emporte le produit ailleurs !"] = "It lands on a product. Tap it quickly to shoo it away (+{0} coins), or it flies off with the product!",
        ["LA SIESTE ITALIENNE"] = "THE ITALIAN SIESTA", ["Quand un rideau de fer descend à moitié, l'étagère va fermer {0} s. Fais vite un trio dessus : +{1} s au chrono !"] = "When a shutter comes half down, the shelf is about to close for {0} s. Quick, make a trio on it: +{1} s on the clock!",
        ["LE PRODUIT CADENASSÉ"] = "THE PADLOCKED PRODUCT", ["Un produit sous cadenas ne se prend pas. Le cadenas s'ouvre après {0} coups joués ailleurs : le chiffre dessus montre ceux qui restent."] = "A padlocked product can't be taken. The padlock opens after {0} moves played elsewhere: the number on it shows how many are left.",
        ["PROMO FLASH"] = "FLASH SALE", ["Une étiquette -50 % se pose sur un produit : fais son trio avant la fin pour +{0} s au chrono. Raté : -{1} s !"] = "A -50% tag lands on a product: make its trio before it runs out for +{0} s on the clock. Missed: -{1} s!",
        // trophies
        ["Bronze"] = "Bronze", ["Argent"] = "Silver", ["Or"] = "Gold", ["TROPHÉES"] = "TROPHIES", ["{0} / {1} médailles"] = "{0} / {1} medals",
        ["Trophée complet !"] = "Trophy complete!", ["TROPHÉE {0} !"] = "{0} TROPHY!", ["{0}  +{1} pièces"] = "{0}  +{1} coins",
        ["Chasseur de pigeons"] = "Pigeon chaser", ["Chasse {0} pigeons"] = "Shoo {0} pigeons",
        ["Juste à temps"] = "Just in time", ["Fais {0} trios avant le rideau"] = "Make {0} trios before the shutter",
        ["Serrurier"] = "Locksmith", ["Ouvre {0} cadenas"] = "Open {0} padlocks",
        ["Roi des promos"] = "Sale king", ["Fais {0} trios en promo"] = "Make {0} trios on sale",
        ["Trieur"] = "Sorter", ["Fais {0} trios"] = "Make {0} trios", ["Combo"] = "Combo", ["Enchaîne un combo x{0}"] = "Chain an x{0} combo",
        ["Chercheur d'or"] = "Gold digger", ["Fais {0} trios dorés"] = "Make {0} golden trios",
        ["Perfectionniste"] = "Perfectionist", ["Finis {0} niveaux à 3 étoiles"] = "Finish {0} levels with 3 stars",
        ["Fidèle"] = "Faithful", ["Réussis le défi {0} jours de suite"] = "Beat the challenge {0} days in a row",
        ["Collectionneur"] = "Collector", ["Complète {0} album(s)"] = "Complete {0} album(s)",
        ["Tour du monde"] = "World tour", ["Finis {0} pays"] = "Finish {0} countries",
        // products (sticker names)
        ["Baguette"] = "Baguette", ["Croissant"] = "Croissant", ["Macaron"] = "Macaron", ["Éclair"] = "Éclair", ["Vin"] = "Wine",
        ["Pain au chocolat"] = "Pain au chocolat", ["Crème brûlée"] = "Crème brûlée", ["Canelés"] = "Canelés", ["Camembert"] = "Camembert",
        ["Saucisson"] = "Saucisson", ["Moutarde"] = "Mustard", ["Confiture"] = "Jam",
        ["Tomate"] = "Tomato", ["Huile d'olive"] = "Olive oil", ["Sauce soja"] = "Soy sauce",
        ["Frites"] = "Fries", ["Pop-corn"] = "Popcorn", ["Pilon"] = "Drumstick", ["Beurre de cacahuète"] = "Peanut butter",
    };
}
