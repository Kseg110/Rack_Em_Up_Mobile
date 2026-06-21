using UnityEngine;
using UnityEngine.UI;

// Attach to a UI Button. Assign the button's OnClick to CallShowAd()
public class TestAdButton : MonoBehaviour
{
    public Button testButton;

    void Start()
    {
        if (testButton != null)
        {
            testButton.onClick.RemoveAllListeners();
            testButton.onClick.AddListener(CallShowAd);
        }
    }

    public void CallShowAd()
    {
        if (UnityAdsManager.Instance != null)
        {
            Debug.Log("TestAdButton: Requesting ad via UnityAdsManager.");
            UnityAdsManager.Instance.RequestShowOnWin();
        }
        else
        {
            Debug.LogWarning("UnityAdsManager.Instance is null. Ensure UnityAdsManager exists in the initial scene and is active.");
        }
    }
}