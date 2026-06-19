using System;
using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public List<GameObject> roundPrefabs;
    public List<Transform> playerSpawnPoints;
    private GameObject currentRoundInstance;
    private int currentRoundIndex = 0;
    private RoundEnemyTracker currentTracker;

    public int CurrentRoundIndex => currentRoundIndex;

    public event Action<int, int> OnRoundAdvanced;
    public event Action OnAllRoundsCleared;

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
            
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                Vector3 spawnPos = GetCurrentPlayerSpawnPosition();
                gameManager.RespawnPlayer(spawnPos);
            }
            
            OnRoundAdvanced?.Invoke(currentRoundIndex, roundPrefabs.Count);
        }
        else
        {
            OnAllRoundsCleared?.Invoke();
        }
    }

    private void SpawnRound(int index)
    {
        if (index < roundPrefabs.Count && roundPrefabs[index] != null)
        {
            currentRoundInstance = Instantiate(roundPrefabs[index], Vector3.zero, Quaternion.identity);
            
            // Get the tracker component
            currentTracker = currentRoundInstance.GetComponent<RoundEnemyTracker>();
            
            if (currentTracker != null)
            {
                currentTracker.OnAllEnemiesDefeated += OnCurrentRoundCleared;
                Debug.Log($"[RoundManager] Round {index} tracker registered");
            }
            else
            {
                Debug.LogError($"[RoundManager] Round {index} prefab missing RoundEnemyTracker component!");
            }
        }
    }

    private void DestroyCurrentRound()
    {
        if (currentTracker != null)
        {
            currentTracker.OnAllEnemiesDefeated -= OnCurrentRoundCleared;
            currentTracker = null;
        }
        
        if (currentRoundInstance != null)
        {
            Destroy(currentRoundInstance);
            currentRoundInstance = null;
        }
    }

    public void OnCurrentRoundCleared()
    {
        Debug.Log($"[RoundManager] Round {currentRoundIndex} cleared!");
        
        var gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.StopPlayerMovement();

            if (gameManager.PlayerInstance != null)
            {
                var billiardController = gameManager.PlayerInstance.GetComponent<BilliardController>();
                if (billiardController != null)
                {
                    billiardController.DisableOneHitKill();
                }
            }

    
            if (currentRoundIndex == roundPrefabs.Count - 1)
            {
                Debug.Log("[RoundManager] Final round cleared - Player wins!");
                gameManager.WinGame();
                return; // Don't call NextRound
            }
            
            gameManager.Shots = 10;
        }
        
        NextRound();
    }

    public bool IsLastRound => currentRoundIndex == roundPrefabs.Count - 1;

    public Vector3 GetCurrentPlayerSpawnPosition()
    {
        if (currentRoundIndex < playerSpawnPoints.Count && playerSpawnPoints[currentRoundIndex] != null)
            return playerSpawnPoints[currentRoundIndex].position;
        return Vector3.zero;
    }
}
