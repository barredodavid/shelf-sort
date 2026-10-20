using System.Linq;
using UnityEngine;

// Daily challenge: one special level a day, the same for everyone on that day, played from the title screen. It
// takes a baked level with obstacles, in one of the open countries, and gives it the day's twist (a tight clock,
// everything in "?" cartons, no bonuses, or every product golden). Winning it the first time that day pays
// DailyCoins, plus a bonus for each day of the streak (days won in a row).
public partial class Game
{
    const int DailyCoins = 100, StreakBonus = 20, StreakBonusMax = 100, AllGolden = -2;
    static readonly string[] DailyTwists = { "CHRONO SERRÉ", "TOUT EN CARTONS", "SANS BONUS", "TOUT EN OR" };

    bool daily;
    (int world, int lvl) beforeDaily; // the level to go back to when the challenge is over

    static int Today => (int)(System.DateTime.Today - new System.DateTime(2026, 1, 1)).TotalDays;
    static int DailyTwist => Today % DailyTwists.Length;
    static bool DailyDone => PlayerPrefs.GetInt("dailyDone", -1) == Today;
    // the streak still counts while yesterday's challenge was won
    static int Streak => PlayerPrefs.GetInt("dailyDone", -1) >= Today - 1 ? PlayerPrefs.GetInt("streak", 0) : 0;

    string LevelTitle => daily ? "DÉFI DU JOUR" : $"NIVEAU {LevelName}";

    void PlayDaily()
    {
        if (!daily) beforeDaily = (world, lvl);
        daily = true;
        var open = Enumerable.Range(0, Worlds.Length).Where(WorldOpen).ToList();
        world = open[Today % open.Count];
        lvl = 0;
        menu = Menu.None;
        StartLevel();
        Fx.Play("match");
    }

    // back to the title screen, the regular level as it was
    void ExitDaily()
    {
        daily = false;
        (world, lvl) = beforeDaily;
        OpenMenu(Menu.Home);
    }

    // the "Carte" buttons of the end and pause cards: the map, or home after the challenge
    void LeaveLevel()
    {
        if (daily) ExitDaily();
        else OpenMenu(Menu.Map);
    }

    // the day's level, the same for everyone (seeded by the date), with its twist applied
    LevelGen.Level DailyLevel()
    {
        var lv = LevelGen.Make(25 + Today % 150, new System.Random(Today));
        if (DailyTwist == 0) lv.Time = Mathf.Round(lv.Time * .6f);
        if (DailyTwist == 1)
            foreach (var row in lv.Shelves.Select(sh => sh[0]))
                for (int i = 0; i < 3; i++)
                    if (LevelGen.Movable(row[i]) && !LevelGen.IsBoxed(row[i])) row[i] += LevelGen.Boxed;
        return lv;
    }

    // a won challenge: the reward the first time today, the streak grows
    void ScoreDaily()
    {
        unlockedMsg = null;
        gained = 0;
        if (DailyDone) return;
        int streak = Streak + 1;
        PlayerPrefs.SetInt("streak", streak);
        PlayerPrefs.SetInt("dailyDone", Today);
        gained = DailyCoins + Mathf.Min(StreakBonus * (streak - 1), StreakBonusMax);
        coins += gained;
        PlayerPrefs.SetInt("coins", coins);
        PlayerPrefs.Save();
        unlockedMsg = streak > 1 ? $"Série : {streak} jours de suite !" : "Reviens demain pour une série !";
    }

    // the title screen's button: today's twist, done or not, the streak in a badge
    void DailyButton(Rect r)
    {
        if (Button(r, DailyDone ? "Défi réussi" : "Défi du jour", DailyDone ? Mist : Gold, 34)) PlayDaily();
        if (Streak > 0) Badge(r.xMax - 44, r.y - 22, Streak.ToString());
    }
}
