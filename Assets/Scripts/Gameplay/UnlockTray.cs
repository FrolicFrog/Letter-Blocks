using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

public class UnlockTray : MonoBehaviour
{
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    [HideInInspector] public bool horizontalLock = false;
    public static Material trayMat;
    public static GameObject arrow;
    public static ParticleSystem effect;
    private void OnDestroy()
    {
        
        if (horizontalLock)
        {

            BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, true).tag = "Vertical";
        }
        else
        {
            BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells);
        }
        Instantiate(effect, transform.position+ new Vector3(0,3,0), Quaternion.identity).gameObject.transform.localScale = Vector3.one*3f;
      
        Destroy(transform.parent. gameObject);
        Taptic.Heavy();
       
    }
}
