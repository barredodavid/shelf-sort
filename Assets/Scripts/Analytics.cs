using GameAnalyticsSDK;
using UnityEngine;

// GameAnalytics: sessions, retention and devices come by themselves; the game adds its levels (start, complete with
// the stars as score, fail), which gives the funnel of where players give up, and its rewarded ads. Phones only: the
// editor and the Windows player send nothing. Keys: Setup.SetGameAnalyticsKeys.
public partial class Game
{
    // progression names must be plain ASCII, so not the countries' displayed names
    static readonly string[] AnalyticsWorlds = { "France", "Italy", "Japan", "USA" };

    void InitAnalytics()
    {
        if (!Application.isMobilePlatform) return;
        new GameObject("GameAnalytics").AddComponent<GameAnalytics>();
        GameAnalytics.Initialize();
    }

    void TrackLevel(GAProgressionStatus status, int score = 0)
    {
        if (!GameAnalytics.Initialized) return;
        if (daily) GameAnalytics.NewProgressionEvent(status, "Daily", score);
        else GameAnalytics.NewProgressionEvent(status, AnalyticsWorlds[world], $"Level{lvl + 1:D2}", score);
    }

    static void TrackAd(GAAdAction action)
    {
        if (GameAnalytics.Initialized) GameAnalytics.NewAdEvent(action, GAAdType.RewardedVideo, "admob", "continue");
    }
}
