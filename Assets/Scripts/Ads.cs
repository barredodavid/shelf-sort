using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

// Rewarded ads (AdMob) behind the "+15 seconds / undo 2 moves" button of a lost level. Consent comes first: Google's
// UMP shows the GDPR form and then Apple's tracking prompt (both messages are set up in AdMob, Privacy & messaging),
// then one rewarded ad is kept loaded, reloaded after each showing. Off phones (editor, Windows player) the bonus is
// given without an ad.
public partial class Game
{
    // ponytail: Google's test ad until the app is live on the App Store and approved by AdMob; false for that build
    const bool TestAds = true;
    const string RewardedId = TestAds ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-1022303366789247/9029642270";

    RewardedAd rewarded;

    void InitAds()
    {
        if (!Application.isMobilePlatform) return;
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        // offline, Update fails but a consent given before still lets ads load
        ConsentInformation.Update(new ConsentRequestParameters(), _ =>
            ConsentForm.LoadAndShowConsentFormIfRequired(_ =>
            {
                if (ConsentInformation.CanRequestAds()) MobileAds.Initialize(_ => LoadRewarded());
            }));
    }

    // where the law asks for it (GDPR, US states), the pause menu lets the player reopen Google's consent choices
    static bool PrivacyOptionsRequired => Application.isMobilePlatform &&
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    void ShowPrivacyOptions() => ConsentForm.ShowPrivacyOptionsForm(_ =>
    {
        if (ConsentInformation.CanRequestAds()) MobileAds.Initialize(_ => LoadRewarded()); // the new choice applies to the next ad
    });

    void LoadRewarded()
    {
        if (!ConsentInformation.CanRequestAds()) return;
        RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null) return; // tried again on the next tap
            rewarded = ad;
            ad.OnAdFullScreenContentClosed += LoadRewarded;
            ad.OnAdFullScreenContentFailed += _ => LoadRewarded();
        });
    }

    void ShowRewarded(System.Action reward)
    {
        if (!Application.isMobilePlatform) { reward(); return; }
        if (rewarded == null || !rewarded.CanShowAd())
        {
            TrackAd(GameAnalyticsSDK.GAAdAction.FailedShow);
            Toast("Pas de pub disponible");
            LoadRewarded();
            return;
        }
        var ad = rewarded;
        rewarded = null;
        TrackAd(GameAnalyticsSDK.GAAdAction.Show);
        ad.Show(_ =>
        {
            TrackAd(GameAnalyticsSDK.GAAdAction.RewardReceived);
            reward();
        });
    }
}
