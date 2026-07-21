public abstract class Manager<T> : Singleton<T> where T : UnityEngine.MonoBehaviour
{
    public bool IsInitialized { get; protected set; }
    
    protected override void Awake()
    {
        base.Awake();
        
        if (Instance == this && !IsInitialized)
        {
            Initialize();
        }
    }

    public virtual void Initialize()
    {
        IsInitialized = true;
    }
}
