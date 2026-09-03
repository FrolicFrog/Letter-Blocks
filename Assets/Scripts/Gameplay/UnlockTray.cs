using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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


            var letterContainer = BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, true);
            letterContainer.tag = "Vertical";
            var ts = letterContainer.AddComponent<TraySpliter>();
            ts.trayCells = trayCells;
            ts.trayPos =trayPos;
            ts.horizontalLock = horizontalLock;
        }
        else
        {
            var letterContainer = BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells);
            var ts = letterContainer.AddComponent<TraySpliter>();
            ts.trayCells = trayCells;
            ts.trayPos = trayPos;
            ts.horizontalLock = horizontalLock;
        }
        Instantiate(effect, transform.position+ new Vector3(0,3,0), Quaternion.identity).gameObject.transform.localScale = Vector3.one*3f;
      
        Destroy(transform.parent. gameObject);
        Taptic.Heavy();
       
    }
}
