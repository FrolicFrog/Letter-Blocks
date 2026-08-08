using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class FreezeManager : MonoBehaviour
{
    [HideInInspector] public int totalCount;
    public static int subtractCount;
    public static Material tray, box;
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
                foreach (var tmp in tmps.ToList())
                {
                    Destroy(tmp);

                }
                for (int i = 0; i < instance.transform.childCount; i++)
                {
                    instance.transform.GetChild(i).GetComponent<MeshRenderer>().material = tray;
                    instance.transform.GetChild(i).GetChild(0).GetComponent<MeshRenderer>().material = box;
                    instance.transform.GetChild(i).GetChild(0).GetChild(1).gameObject.SetActive(true);
                    instance.transform.GetChild(i).gameObject.layer = LayerMask.NameToLayer("Tray");
                }

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
