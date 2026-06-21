using UnityEngine;
using System;

public class RoundEnemyTracker : MonoBehaviour
{
    private int aliveEnemyCount = 0;
    public int AliveEnemyCount => aliveEnemyCount;
    
    public event Action OnAllEnemiesDefeated;

    public void RegisterEnemy()
    {
        aliveEnemyCount++;
        Debug.Log($"[RoundEnemyTracker] Enemy registered. Total: {aliveEnemyCount}");
    }

    public void EnemyDefeated()
    {
        aliveEnemyCount--;
        Debug.Log($"[RoundEnemyTracker] Enemy defeated. Remaining: {aliveEnemyCount}");
        
        if (aliveEnemyCount <= 0)
        {
            Debug.Log("[RoundEnemyTracker] All enemies defeated!");
            OnAllEnemiesDefeated?.Invoke();
        }
    }
}