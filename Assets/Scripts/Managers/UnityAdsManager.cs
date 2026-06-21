using UnityEngine;
using UnityEngine.Advertisements;

public class UnityAdsManager : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
{
    public static UnityAdsManager Instance { get; private set; }

    [Header("Set these from the Dashboard")]
    public string androidGameId = "YOUR_ANDROID_GAME_ID";
    public string iosGameId = "YOUR_IOS_GAME_ID";
    public bool testMode = true;
    public string defaultPlacement = "video";
    [SerializeField] private GameObject AdsPannel;

    private string _gameId;
    private string _pendingShowPlacement;

    void Awake()
    {
        // simple singleton so other managers can call it directly
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // hide panel by default
        if (AdsPannel != null)
            AdsPannel.SetActive(false);

        Debug.Log($"UnityAdsManager Awake. Instance set. gameObject active={gameObject.activeInHierarchy}");

        if (!Advertisement.isSupported)
        {
            Debug.LogWarning("Unity Ads not supported on this platform.");
            return;
        }

        _gameId = (Application.platform == RuntimePlatform.IPhonePlayer) ? iosGameId : androidGameId;
        if (!Advertisement.isInitialized)
            Advertisement.Initialize(_gameId, testMode, this);

        Debug.Log($"UnityAdsManager Awake. gameId={_gameId}, placement='{defaultPlacement}', testMode={testMode}");
    }

    void OnEnable()
    {
        Debug.Log("UnityAdsManager OnEnable");
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameEnded += HandleGameEnded;
    }

    void OnDisable()
    {
        Debug.Log("UnityAdsManager OnDisable");
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameEnded -= HandleGameEnded;
    }

    // Public entrypoint you can call directly when the win screen shows.
    public void RequestShowOnWin()
    {
        Debug.Log("UnityAdsManager.RequestShowOnWin called");
        HandleGameEnded(true);
    }

    private void HandleGameEnded(bool isWin)
    {
        Debug.Log($"UnityAdsManager.HandleGameEnded invoked. isWin={isWin}");
        if (!isWin)
            return;

        // ensure we have a panel reference
        if (AdsPannel == null)
        {
            AdsPannel = GameObject.Find("AdsPannel") ?? GameObject.Find("AdsPanel") ?? GameObject.Find("AdsOverlay");
            if (AdsPannel == null)
                Debug.LogWarning("UnityAdsManager: AdsPannel not assigned and not found by common names.");
        }

        if (AdsPannel != null)
        {
            AdsPannel.SetActive(true);
            AdsPannel.transform.SetAsLastSibling();
        }

        var placement = defaultPlacement;
        Debug.Log($"UnityAdsManager: Requesting load for placement '{placement}' (gameId {_gameId})");
        _pendingShowPlacement = placement;
        LoadAd(placement);
        // wait for OnUnityAdsAdLoaded to call Show
    }

    public void LoadAd(string placementId = null)
    {
        placementId ??= defaultPlacement;
        Debug.Log($"UnityAdsManager: Loading placement '{placementId}'");
        Advertisement.Load(placementId, this);
    }

    public void ShowAd(string placementId = null)
    {
        placementId ??= defaultPlacement;
        Debug.Log($"UnityAdsManager: Showing placement '{placementId}'");
        Advertisement.Show(placementId, this);
    }

    // Initialization callbacks
    public void OnInitializationComplete()
    {
        Debug.Log("Unity Ads initialization complete.");
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
        if (!string.IsNullOrEmpty(_pendingShowPlacement) && placementId == _pendingShowPlacement)
        {
            ShowAd(placementId);
        }
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.LogError($"Failed to load Ad {placementId}: {error} - {message}");
        if (error == UnityAdsLoadError.INVALID_ARGUMENT)
        {
            Debug.LogError($"Placement '{placementId}' does not exist for gameId '{_gameId}'. Create it in the Unity Dashboard or set defaultPlacement to a valid id.");
        }

        if (AdsPannel != null)
            AdsPannel.SetActive(false);
        _pendingShowPlacement = null;
        Time.timeScale = 1f;
    }

    // Show callbacks
    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogError($"Failed to show Ad {placementId}: {error} - {message}");
        if (AdsPannel != null)
            AdsPannel.SetActive(false);
        _pendingShowPlacement = null;
        Time.timeScale = 1f;
    }

    public void OnUnityAdsShowStart(string placementId)
    {
        Debug.Log($"Ad show started: {placementId}");
        Time.timeScale = 0f;
    }

    public void OnUnityAdsShowClick(string placementId) { }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log($"Ad show complete: {placementId} state={showCompletionState}");
        if (AdsPannel != null)
            AdsPannel.SetActive(false);

        Time.timeScale = 1f;

        if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log("Ad completed: reward if applicable.");
        }

        _pendingShowPlacement = null;
        LoadAd(defaultPlacement);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameEnded -= HandleGameEnded;
        if (Instance == this) Instance = null;
    }
}