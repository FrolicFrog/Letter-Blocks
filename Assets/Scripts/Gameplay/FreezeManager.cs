using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class FreezeManager : MonoBehaviour
{
    [HideInInspector] public int totalCount;
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    [HideInInspector] public bool horizontalLock = false;
    public static Material trayMat;
    public static GameObject arrow;
    public static List<FreezeManager> instances = new List<FreezeManager>();
    void Start()
    {
        instances.Add(this);
    }
    [ContextMenu("Debug Now")]
    public void Print()
    {
        DecreaseFreezeCount();
    }
    public static void DecreaseFreezeCount()
    {
        foreach (FreezeManager instance in instances.ToList())
        {
           var tmps = instance.GetComponentsInChildren<TextMeshPro>();
            instance.totalCount--;
            if (instance.totalCount <= 0)
            {
               
                if (instance.horizontalLock)
                {
                
                    BottomGridManager.Instance.CreateTray(instance.trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, instance.trayCells,true).tag = "Vertical";
                }
                else
                {
                    BottomGridManager.Instance.CreateTray(instance.trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, instance.trayCells);
                }
               Destroy(instance.gameObject);

                instances.Remove(instance);
            }
            else
            {
                foreach (var tmp in tmps)
                {
                 
                    tmp.text =instance.totalCount.ToString();

                }
            }
        }
    }
}
