// commented out save logic, will implement in future build to allow player to save and close the game and return to current run in the future.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10)]
public class GameManager : Singleton<GameManager>
{

    public delegate void PlayerSpawnDelegate(BilliardController playerInstance);
    public event PlayerSpawnDelegate OnPlayerControllerCreated;

    #region Player Controller Information
    public BilliardController playerPrefab;
    private BilliardController _playerInstance;
    public BilliardController PlayerInstance => _playerInstance;
    #endregion

    [Header("Win / Enemy Parents (ordered by level)")]
    [Tooltip("Assign Enemy Parents; Each parent is activated when the previous level is cleared.")]
    public List<Transform> enemyLevelParents = new List<Transform>();
    [Tooltip("Fallback names to search for if enemyLevelParents list is empty (e.g. 'Lvl1Enemies', 'Lvl2Enemies').")]
    public List<string> enemyLevelParentNames = new List<string> { "Lvl1Enemies", "Lvl2Enemies" };

    private int _currentLevelIndex = 0;

    #region UI Events
    // Add events for UI
    public event Action<int> OnLivesChanged;
    public event Action<int> OnShotsChanged;
    public event Action<int> OnRoundsChanged;
    public event Action<bool> OnGameEnded; // true = win, false = loss

    // Backing fields
    private int _lives = 7;
    private int _shots = 10;
    private int _rounds = 0;
    public int MaxLives { get; set; } = 7;
    private bool winCheck = false;

    public int Lives
    {
        get => _lives;
        set
        {
            if (_lives != value)
            {
                _lives = Mathf.Clamp(value, 0, 7);
                OnLivesChanged?.Invoke(_lives);
                if (_lives <= 0)
                {
                    EndGame(false);
                }
            }
        }
    }

    public int Shots
    {
        get => _shots;
        set
        {
            if (_shots != value)
            {
                _shots = value;
                OnShotsChanged?.Invoke(_shots);
            }
        }
    }

    public int Rounds
    {
        get => _rounds;
        set
        {
            if (_rounds != value)
            {
                _rounds = value;
                OnRoundsChanged?.Invoke(_rounds);
            }
        }
    }
    #endregion
    private new void Awake()
    {
        // Subscribe to scene loading to spawn player que ball
        SceneManager.sceneLoaded += OnSceneLoaded;

        var roundManager = UnityEngine.Object.FindAnyObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundAdvanced += HandleRoundAdvanced;
            roundManager.OnAllRoundsCleared += HandleAllRoundsCleared;
        }
    }

    private void HandleRoundAdvanced(int currentRound, int totalRounds)
    {
        Rounds = currentRound + 1;
    }

    private void HandleAllRoundsCleared()
    {
        if (Lives > 0 && Shots > 0)
        {
            EndGame(true);
        }
        else
        {
            EndGame(false);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == 1 || scene.name.Contains("Game"))
        {
            SpawnPlayer();

            // Resolve enemy level parents if not assigned in the inspector
            ResolveLevelParents();

            // Start from the first level
            _currentLevelIndex = 0;
            ActivateCurrentLevel();

            // Reset win flag for a new playthrough
            winCheck = false;

            // Ask GameCanvasManager to hide the end scene UI if present
            var canvasMgr = UnityEngine.Object.FindAnyObjectByType<GameCanvasManager>();
            if (canvasMgr != null)
                canvasMgr.HideEndScene();
        }
    }

    // Attempts to populate enemyLevelParents from enemyLevelParentNames if the list is empty.
    private void ResolveLevelParents()
    {
        if (enemyLevelParents == null)
            enemyLevelParents = new List<Transform>();

        // Only auto-resolve if nothing was assigned in the inspector
        if (enemyLevelParents.Count == 0 && enemyLevelParentNames != null)
        {
            foreach (var parentName in enemyLevelParentNames)
            {
                if (string.IsNullOrEmpty(parentName)) continue;
                var found = GameObject.Find(parentName);
                if (found != null)
                    enemyLevelParents.Add(found.transform);
            }
        }
    }

    // Deactivates all level parents then activates only the current one.
    private void ActivateCurrentLevel()
    {
        for (int i = 0; i < enemyLevelParents.Count; i++)
        {
            if (enemyLevelParents[i] != null)
                enemyLevelParents[i].gameObject.SetActive(i == _currentLevelIndex);
        }
    }

    private void OnEnable()
    {
        // Subscribe to mobile input events
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnTouchBegin += HandleTouchInput;
            InputManager.Instance.OnTouchEnd += HandleTouchRelease;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from mobile input events
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnTouchBegin -= HandleTouchInput;
            InputManager.Instance.OnTouchEnd -= HandleTouchRelease;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #region Mobile Input Handlers
    private void HandleTouchInput()
    {
        // Reserved for gameplay usage; logging removed.
    }

    private void HandleTouchRelease()
    {
        // Reserved for gameplay usage; logging removed.
    }

    #endregion

    private bool isPaused = false;

    void Update()
    {
        // Keep only essential Update logic
        // For mobile, you might want to handle back button presses instead of Escape
        HandleMobileBackButton();

        // Only perform win checks while in the actual game scene and if win not already detected
        if (!winCheck && (SceneManager.GetActiveScene().buildIndex == 1 || SceneManager.GetActiveScene().name.Contains("Game")))
        {
            CheckForWin();
        }
    }

    private void HandleMobileBackButton()
    {
        // Handle Android back button or iOS equivalent
        if (Application.platform == RuntimePlatform.Android)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackButton();
            }
        }
    }

    private void HandleBackButton()
    {
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            // On main menu, quit application or minimize
            Application.Quit();
        }
        else
        {
            // In game, return to menu
            SceneManager.LoadScene(0);
        }
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    private void GameOver()
    {
        EndGame(false);
    }

    public Transform spawnPoint;

    private void SpawnPlayer()
    {
        Vector3 spawnPos;
        var roundManager = UnityEngine.Object.FindAnyObjectByType<RoundManager>();
        if (roundManager != null)
        {
            spawnPos = roundManager.GetCurrentPlayerSpawnPosition();
        }
        else
        {

            // Find spawn point if not assigned
            if (spawnPoint == null)
            {
                GameObject spawnObj = GameObject.Find("PlayerSpawnPoint");
                if (spawnObj != null)
                {
                    spawnPoint = spawnObj.transform;
                }
                else
                {
                    spawnPoint = null;
                }
            }
            spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        }
        // Find spawn point if not assigned in inspector
        if (_playerInstance != null)
        {
            return;
        }

        if (playerPrefab == null)
        {
            return;
        }

        // Instantiate player
        _playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        _playerInstance.gameObject.name = "Player";

        // Notify listeners
        OnPlayerControllerCreated?.Invoke(_playerInstance);
    }

    public void RespawnPlayer(Vector3 position)
    {
        if (_playerInstance != null)
        {
            _playerInstance.transform.position = position;

            // Reset rigidbody (belt-and-suspenders for any non-kinematic forces)
            Rigidbody rb = _playerInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Reset kinematic ball state (custom physics — this is the true velocity store)
            BilliardBall ball = _playerInstance.GetComponent<BilliardBall>();
            if (ball != null)
            {
                ball.ResetState();
            }
        }
        else
        {
            SpawnPlayer();
        }
    }

    // Checks whether all enemies in the current level are cleared.
    // If so, advances to the next level or triggers the win if it was the last.
    private void CheckForWin()
    {
        var roundManager = UnityEngine.Object.FindAnyObjectByType<RoundManager>();
        if (roundManager == null) return;

        // Check if all enemies in the current round are cleared
        Transform currentParent = enemyLevelParents[_currentLevelIndex];
        if (currentParent == null || currentParent.childCount > 0) return;

        // Notify RoundManager to advance
        roundManager.OnCurrentRoundCleared();
    }


    public void SetLoadFromCheckpoint(bool value)
    {
        PlayerPrefs.SetInt("LoadFromCheckpoint", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void WinGame()
    {
        winCheck = true;
        SceneManager.sceneLoaded += OnMenuSceneLoaded;
        SceneManager.LoadScene("Menu");
    }

    private void OnMenuSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Menu")
        {
            GameObject mainMenu = GameObject.Find("Menu");

            GameObject endScene = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "EndScene" && go.scene.name == "Menu")
                {
                    endScene = go;
                    break;
                }
            }

            if (mainMenu != null)
                mainMenu.SetActive(false);
            if (endScene != null)
                endScene.SetActive(true);

            SceneManager.sceneLoaded -= OnMenuSceneLoaded;
        }
    }

    public void PlayerDied()
    {
        // Clean up player instance
        if (_playerInstance != null)
        {
            Destroy(_playerInstance.gameObject);
            _playerInstance = null;
        }
    }

    private void EndGame(bool isWin)
    {
        winCheck = true;
        OnGameEnded?.Invoke(isWin);
    }
}