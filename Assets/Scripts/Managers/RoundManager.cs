using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;


public class RoundManager : MonoBehaviour
{
    public List<GameObject> roundPrefabs; // contains each rounds enemies
    public List<Transform> playerSpawnPoints;
    private GameObject currentRoundInstance;
    private int currentRoundIndex = 0;

    // Cache GameManager reference
    private GameManager cachedGameManager;

    //private PlayerControls inputControls;
    public int CurrentRoundIndex => currentRoundIndex;

    public event Action<int, int> OnRoundAdvanced;
    public event Action OnAllRoundsCleared;

    private void Awake()
    {
        // Cache GameManager reference at startup
        cachedGameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (cachedGameManager == null)
        {
            Debug.LogError("[RoundManager] GameManager not found in scene!");
        }
    }

    public void StartFirstRound()
    {
        currentRoundIndex = 0;
        SpawnRound(currentRoundIndex);
        OnRoundAdvanced?.Invoke(currentRoundIndex, roundPrefabs.Count);
    }

    public void NextRound()
    {
        DestroyCurrentRound();
        currentRoundIndex++;
        if (currentRoundIndex < roundPrefabs.Count)
        {
            SpawnRound(currentRoundIndex);
            if (cachedGameManager != null)
            {
                Vector3 spawnPos = GetCurrentPlayerSpawnPosition();
                cachedGameManager.RespawnPlayer(spawnPos);
            }
            OnRoundAdvanced?.Invoke(currentRoundIndex, roundPrefabs.Count);
        }
        else
        {
            OnAllRoundsCleared?.Invoke();
        }
    }

    public void OnCurrentRoundCleared()
    {
        if (cachedGameManager == null)
        {
            Debug.LogError("[RoundManager] Cannot clear round - GameManager is null!");
            return;
        }

        cachedGameManager.StopPlayerMovement();

        // Disable one-hit kill for next round
        if (cachedGameManager.PlayerInstance != null)
        {
            var billiardController = cachedGameManager.PlayerInstance.GetComponent<BilliardController>();
            if (billiardController != null)
            {
                billiardController.DisableOneHitKill();
            }
        }

        if (currentRoundIndex == 3)
        {
            cachedGameManager.WinGame();
            return;
        }

        cachedGameManager.Shots = 10;
        NextRound();
    }

    private void SpawnRound(int index)
    {
        if (index < roundPrefabs.Count)
        {
            currentRoundInstance = Instantiate(roundPrefabs[index], Vector3.zero, Quaternion.identity);
            Debug.Log($"Spawned round prefab: {currentRoundInstance.name}");
        }
    }

    private void DestroyCurrentRound()
    {
        if (currentRoundInstance != null)
        {
            Debug.Log($"Destroying round prefab: {currentRoundInstance.name}");
            Destroy(currentRoundInstance);
            currentRoundInstance = null;
        }
    }
    
    public bool IsLastRound => currentRoundIndex == roundPrefabs.Count - 1;

    // Returns current spawn location for the round
    public Vector3 GetCurrentPlayerSpawnPosition()
    {
        if (currentRoundIndex < playerSpawnPoints.Count && playerSpawnPoints[currentRoundIndex] != null)
            return playerSpawnPoints[currentRoundIndex].position;
        return Vector3.zero;
    }

    public bool AreAllEnemiesCleared()
    {
        if (currentRoundInstance == null) 
        {
            Debug.LogWarning("[RoundManager] AreAllEnemiesCleared: currentRoundInstance is null");
            return false;
        }

        // find all enemies in game scene for the round
        var allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        int count = 0;
        foreach(var enemy in allEnemies)
        {
            if (enemy != null && enemy.transform.IsChildOf(currentRoundInstance.transform))
                count++;
        }
        
        Debug.Log($"[RoundManager] Round {currentRoundIndex}: {count} enemies remaining");
        return count == 0;
    }
}
