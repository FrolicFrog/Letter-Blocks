using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TraySpliter : MonoBehaviour
{
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    [HideInInspector] public bool horizontalLock = false;
    public static Material trayMat;
    public void Split()
    {
        if (horizontalLock)
        {

            BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, true,true).tag = "Vertical";
        }
        else
        {
            BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells,false,true);
        }
        Destroy(gameObject);
    }
}
