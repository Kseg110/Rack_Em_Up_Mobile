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

    //private PlayerControls inputControls;
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
            var gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
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

    public void OnCurrentRoundCleared()
    {
        var gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.StopPlayerMovement();

            // Disable one-hit kill for next round
            if (gameManager.PlayerInstance != null)
            {
                var billiardController = gameManager.PlayerInstance.GetComponent<BilliardController>();
                if (billiardController != null)
                {
                    billiardController.DisableOneHitKill();
                }
            }

            if (currentRoundIndex == 3)
            {
                gameManager.WinGame();
                return;
            }
        }

        gameManager.Shots = 10;
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
        if (currentRoundInstance == null) return false;

        // find all enemies in game scene for the round
        var allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        int count = 0;
        foreach(var enemy in allEnemies)
        {
            if (enemy.transform.IsChildOf(currentRoundInstance.transform))
                count++;
        }
        //Debug.Log($"[RoundManager] Enemies found in current round: {count}");
        return count == 0;
    }

    //public void SkipRound()
    //{
    //    if (currentRoundInstance == null)
    //    {
    //        Debug.LogWarning("[RoundManager] SkipRound called but no current round instance.");
    //        return;
    //    }

    //    var allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
    //    int destroyed = 0;
    //    foreach (var enemy in allEnemies)
    //    {
    //        if (enemy != null && enemy.transform.IsChildOf(currentRoundInstance.transform))
    //        {
    //            Destroy(enemy);
    //            destroyed++;
    //        }
    //    }

    //    Debug.Log($"[RoundManager] SkipRound destroyed {destroyed} enemies in round {currentRoundIndex}.");

    //    // Immediately advance the round (preserves existing OnCurrentRoundCleared behavior)
    //    OnCurrentRoundCleared();
    //}


}
