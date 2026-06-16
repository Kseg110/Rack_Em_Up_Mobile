using Mono.Cecil;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanel : MonoBehaviour
{
    [Header("Upgrade Buttons")]
    public Button lifeUpgradeButton;
    public Button shotUpgradeButton;
    public Button oneHitKillButton;

    private bool lifeUpgradeUsed = false;
    private bool shotUpgradeUsed = false;
    private bool oneHitKillUsed = false;

    void Start()
    {
        gameObject.SetActive(false);

        if (lifeUpgradeButton != null)
            lifeUpgradeButton.onClick.AddListener(OnLifeUpgradeSelected);

        if (shotUpgradeButton != null)
            shotUpgradeButton.onClick.AddListener(OnShotUpgradeSelected);

        if (oneHitKillButton != null)
            oneHitKillButton.onClick.AddListener(OnOneHitKillSelected);

        var roundManager = UnityEngine.Object.FindAnyObjectByType<RoundManager>();
        if(roundManager != null )
        {
            roundManager.OnRoundAdvanced += HandleRoundAdvanced;
        }
    }

    private void OnDestroy()
    {
        var roundManager = UnityEngine.Object.FindAnyObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundAdvanced -= HandleRoundAdvanced;
        }
    }

    private void HandleRoundAdvanced(int currentRound, int totalRounds)
    {
        if (currentRound > 0)
        {
            ShowUpgradePanel();
        }
    }

    public void ShowUpgradePanel()
    {
        gameObject.SetActive(true);
        UpdateButtonStates();
    }

    public void HideUpgradePanel()
    {
        gameObject.SetActive(false);
    }

    private void UpdateButtonStates()
    {
        if (lifeUpgradeButton != null)
            lifeUpgradeButton.interactable = !lifeUpgradeUsed;

        if (shotUpgradeButton != null)
            shotUpgradeButton.interactable = !shotUpgradeUsed;

        if (oneHitKillButton != null)
            oneHitKillButton.interactable = !oneHitKillUsed;
    }

    private void OnLifeUpgradeSelected()
    {
        if (lifeUpgradeUsed) return;

        var gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.Lives += 1;
            gameManager.MaxLives += 1;
            lifeUpgradeUsed = true;
        }
        HideUpgradePanel();
    }

    private void OnShotUpgradeSelected()
    {
        if (shotUpgradeUsed) return;

        var gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.Shots += 1;
            shotUpgradeUsed = true;
        }
        HideUpgradePanel();
    }

    private void OnOneHitKillSelected()
    {
        if (oneHitKillUsed) return;

        var playerInstance = GameManager.Instance?.PlayerInstance;
        if (playerInstance != null)
        {
            var BilliardController = playerInstance.GetComponent<BilliardController>();
            if (BilliardController != null)
            {
                BilliardController.EnableOneHitKill();
                oneHitKillUsed = true;
            }
        }
        HideUpgradePanel();
    }

    public void ResetUpgrades()
    {
        lifeUpgradeUsed = false;
        shotUpgradeUsed = false;
        oneHitKillUsed = false;
        UpdateButtonStates();
    }

}
