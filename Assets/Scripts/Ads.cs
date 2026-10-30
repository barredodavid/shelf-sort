using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

// Rewarded ads (AdMob) behind the "+15 seconds / undo 2 moves" button of a lost level. Consent comes first: Google's
// UMP shows the GDPR form and then Apple's tracking prompt (both messages are set up in AdMob, Privacy & messaging),
// then one rewarded ad is kept loaded, reloaded after each showing. Off phones (editor, Windows player), or when no
// ad is ready, the bonus is given without an ad.
public partial class Game
{
    // true: Google's test ad, loaded even without a consent answer, to check the ad SDK on a TestFlight build
    const bool TestAds = false;
    const string RewardedId = TestAds ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-1022303366789247/9029642270";

    RewardedAd rewarded;

    // Ads wait for a known consent. Test builds skip that: before AdMob reviews the app, Google's consent form fails
    // and no ad would ever load, so the SDK could not be checked on a phone.
    static bool CanRequestAds => TestAds || ConsentInformation.CanRequestAds();

    // the phone app, not the web build opened in a phone's browser (Application.isMobilePlatform is true there too):
    // the ad and analytics SDKs only run in the app
    static bool OnPhone => Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;

    void InitAds()
    {
        if (!OnPhone) return;
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        // that queues every callback, consent ones included, for an executor that only MobileAds.Initialize creates,
        // which we call from the consent callback: create it first, or the consent answer never arrives
        GoogleMobileAds.Common.MobileAdsEventExecutor.Initialize();
        // offline, Update fails but a consent given before still lets ads load
        ConsentInformation.Update(new ConsentRequestParameters(), _ =>
            ConsentForm.LoadAndShowConsentFormIfRequired(_ =>
            {
                askTracking = true; // see AskTracking
                if (CanRequestAds) MobileAds.Initialize(_ => LoadRewarded());
            }));
    }

    // Apple's tracking prompt, asked here too: AdMob only serves its IDFA message once it has reviewed the app, and
    // App Review wants the prompt. iOS shows it only once, so no double with Google's message. iOS also silently drops
    // a request made while the app is still launching (the consent step fails fast before review), hence Update
    // waits for the app to be in front for a couple of seconds.
    bool askTracking;

    void AskTracking()
    {
        if (!askTracking || !Application.isFocused || Time.realtimeSinceStartup < 2) return;
        askTracking = false;
        GameAnalyticsSDK.GameAnalytics.RequestTrackingAuthorization(new IgnoreTrackingAnswer());
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
    static bool PrivacyOptionsRequired => OnPhone &&
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
        if (!OnPhone) { reward(); return; }
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
