using UnityEngine;
using UnityEngine.Advertisements;

public class UnityAdsManager : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
{
    [Header("Set these from the Dashboard")]
    public string androidGameId = "YOUR_ANDROID_GAME_ID";
    public string iosGameId = "YOUR_IOS_GAME_ID";
    public bool testMode = true;
    public string defaultPlacement = "video";

    private string _gameId;

    void Awake()
    {
        if (!Advertisement.isSupported)
            return;

        _gameId = (Application.platform == RuntimePlatform.IPhonePlayer) ? iosGameId : androidGameId;
        if (!Advertisement.isInitialized)
            Advertisement.Initialize(_gameId, testMode, this);
    }

    // Load an ad (recommended for the new API)
    public void LoadAd(string placementId = null)
    {
        placementId ??= defaultPlacement;
        Advertisement.Load(placementId, this);
    }

    // Show an ad (will try to show an already-loaded ad, or will fail otherwise)
    public void ShowAd(string placementId = null)
    {
        placementId ??= defaultPlacement;
        Advertisement.Show(placementId, this);
    }

    // Initialization callbacks
    public void OnInitializationComplete()
    {
        Debug.Log("Unity Ads initialization complete.");
        // Optionally auto-load a placement:
        LoadAd(defaultPlacement);
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.LogError($"Unity Ads Initialization Failed: {error} - {message}");
    }

    // Load callbacks
    public void OnUnityAdsAdLoaded(string placementId)
    {
        Debug.Log($"Ad loaded: {placementId}");
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.LogError($"Failed to load Ad {placementId}: {error} - {message}");
    }

    // Show callbacks
    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogError($"Failed to show Ad {placementId}: {error} - {message}");
    }

    public void OnUnityAdsShowStart(string placementId)
    {
        // Pause game audio/time if needed
    }

    public void OnUnityAdsShowClick(string placementId) { }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log("Ad completed: reward if applicable.");
            // reward player for rewarded placement
        }
    }
}