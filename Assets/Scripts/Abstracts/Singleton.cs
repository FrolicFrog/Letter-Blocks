using UnityEngine;
using UnityEngine.UI;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                
                if (_instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent<T>();
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}
[System.Serializable]
public struct DictonaryPair<TKey,TValue>
{
    TKey Key;
    TValue Value;
    public DictonaryPair(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }
}
