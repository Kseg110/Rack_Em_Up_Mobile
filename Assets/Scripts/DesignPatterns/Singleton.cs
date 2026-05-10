using UnityEngine;

[DefaultExecutionOrder(-1000)]
public abstract class Singleton<T> : MonoBehaviour where T : Component
{
    protected static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance) return _instance;
            _instance = FindFirstObjectByType<T>();

            if (!_instance)
            {
                throw new UnassignedReferenceException($"{typeof(T).Name} singleton is unassigned in the scene. Please ensure an instance exists.");
            }

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (!_instance)
        {
            _instance = this as T;
            DontDestroyOnLoad(_instance);
            return;
        }

        Destroy(gameObject);
    }
}
