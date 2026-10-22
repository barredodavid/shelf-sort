using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

// Rewarded ads (AdMob) behind the "+15 seconds / undo 2 moves" button of a lost level. Consent comes first: Google's
// UMP shows the GDPR form and then Apple's tracking prompt (both messages are set up in AdMob, Privacy & messaging),
// then one rewarded ad is kept loaded, reloaded after each showing. Off phones (editor, Windows player), or when no
// ad is ready, the bonus is given without an ad.
public partial class Game
{
    // ponytail: Google's test ad until the app is live on the App Store and approved by AdMob; false for that build
    const bool TestAds = true;
    const string RewardedId = TestAds ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-1022303366789247/9029642270";

    RewardedAd rewarded;

    // Ads wait for a known consent. Test builds skip that: before AdMob reviews the app, Google's consent form fails
    // and no ad would ever load, so the SDK could not be checked on a phone.
    static bool CanRequestAds => TestAds || ConsentInformation.CanRequestAds();

    void InitAds()
    {
        if (!Application.isMobilePlatform) return;
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        // offline, Update fails but a consent given before still lets ads load
        ConsentInformation.Update(new ConsentRequestParameters(), _ =>
            ConsentForm.LoadAndShowConsentFormIfRequired(_ =>
            {
                // Apple's tracking prompt, asked here too: AdMob only serves its IDFA message once it has reviewed the
                // app, and App Review wants the prompt. iOS shows it only once, so no double with Google's message.
                GameAnalyticsSDK.GameAnalytics.RequestTrackingAuthorization(new IgnoreTrackingAnswer());
                if (CanRequestAds) MobileAds.Initialize(_ => LoadRewarded());
            }));
    }

    // the answer reaches the ad SDKs through iOS itself
    class IgnoreTrackingAnswer : GameAnalyticsSDK.IGameAnalyticsATTListener
    {
        public void GameAnalyticsATTListenerNotDetermined() { }
        public void GameAnalyticsATTListenerRestricted() { }
        public void GameAnalyticsATTListenerDenied() { }
        public void GameAnalyticsATTListenerAuthorized() { }
    }

    // where the law asks for it (GDPR, US states), the pause menu lets the player reopen Google's consent choices
    static bool PrivacyOptionsRequired => Application.isMobilePlatform &&
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    void ShowPrivacyOptions() => ConsentForm.ShowPrivacyOptionsForm(_ =>
    {
        if (CanRequestAds) MobileAds.Initialize(_ => LoadRewarded()); // the new choice applies to the next ad
    });

    void LoadRewarded()
    {
        if (!CanRequestAds) return;
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
            // no ad to show (offline, no fill, consent refused...): the player keeps the bonus anyway
            TrackAd(GameAnalyticsSDK.GAAdAction.FailedShow);
            LoadRewarded();
            reward();
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
