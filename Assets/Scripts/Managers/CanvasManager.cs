using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class CanvasManager : Singleton<CanvasManager>
{
    [Header("Main Menu Buttons")]
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Settings")]
    public Button backButton;

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject audioPanel;

    protected override void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            // Do NOT call DontDestroyOnLoad for UI managers
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(NewGame);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseSettings);
        }
    }

    void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }

    void CloseSettings()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    void QuitGame()
    {
        Time.timeScale = 1f; 
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void NewGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetLoadFromCheckpoint(false);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(1); 
    }
}