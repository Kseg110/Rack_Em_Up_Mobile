using UnityEngine;

public class EnemyRegistration : MonoBehaviour
{
    private void Start()
    {
        RoundEnemyTracker tracker = GetComponentInParent<RoundEnemyTracker>();
        if (tracker != null)
        {
            tracker.RegisterEnemy();
        }
    }
}