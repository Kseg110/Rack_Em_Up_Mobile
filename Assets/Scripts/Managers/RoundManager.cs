using System;
using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public List<GameObject> roundPrefabs; // contains each rounds enemies
    public List<Transform> playerSpawnPoints;
    private GameObject currentRoundInstance;
    private int currentRoundIndex = 0;

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
            OnRoundAdvanced?.Invoke(currentRoundIndex, roundPrefabs.Count);
        }
        else
        {
            OnAllRoundsCleared?.Invoke();
        }
    }

    public void OnCurrentRoundCleared()
    {
        NextRound();
    }

    private void SpawnRound(int index)
    {
        if (index < roundPrefabs.Count)
        {
            currentRoundInstance = Instantiate(roundPrefabs[index]);
        }
    }

    private void DestroyCurrentRound()
    {
        if (currentRoundInstance != null)
        {
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

}
