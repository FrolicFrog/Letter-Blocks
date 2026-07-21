using UnityEngine;

public class GameManager : Manager<GameManager>
{
    [SerializeField] private ENV_MODE EnvironmentMode = ENV_MODE.TEST;
    public bool IsInputAllowed => _InputAllowed;
    public bool IsTestMode => EnvironmentMode == ENV_MODE.TEST;
    private bool _InputAllowed = false;
    
    public override void Initialize()
    {
#if !UNITY_EDITOR
        EnvironmentMode = ENV_MODE.PRODUCTION;
#endif
        _InputAllowed = true;

        base.Initialize();
    }
}
