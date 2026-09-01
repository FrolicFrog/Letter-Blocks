using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpLockManager : MonoBehaviour
{
    public List<KeyValueGroup<int, KeyValueGroup<GameObject,GameObject>>> lockObject;
    private Dictionary<int, KeyValueGroup<GameObject, GameObject>> lockedObjectDict =  new();
    public static PowerUpLockManager Instance;
    void Start()
    {
        Instance = this;
     lockedObjectDict = lockObject.ToDictionary(item=>item.Key, item=>item.Value);
        foreach(var lvl in lockedObjectDict.Keys)
        {
            lockedObjectDict[lvl].Value.GetComponentInChildren<TextMeshProUGUI>(true).text = PlayerPrefs.GetInt(lvl.ToString(), 2).ToString();
        }
    }

    // Update is called once per frame
    void Update()
    {
        foreach(var lvl in lockedObjectDict.Keys)
        {
            if(LevelManager.Instance.CurLevelNumber >= lvl)
            {
                if (lockedObjectDict[lvl].Key.activeSelf)
                {
                    lockedObjectDict[lvl].Key.SetActive(false);
                    lockedObjectDict[lvl].Key.GetComponentInParent<Toggle>().interactable = true;
                    lockedObjectDict[lvl].Value.SetActive(true);
                }
                if (lockedObjectDict[lvl].Value.GetComponentInChildren<TextMeshProUGUI>().text == "0")
                {
                    lockedObjectDict[lvl].Key.GetComponentInParent<Toggle>().interactable = false;
                }
                else
                {
                    lockedObjectDict[lvl].Key.GetComponentInParent<Toggle>().interactable = true;
                }
            }
            
        }

    }

    public void UpdatePowerUpQuantity(int key,int quantity)
    {
        PlayerPrefs.SetInt(key.ToString(), PlayerPrefs.GetInt(key.ToString(), 2)+quantity);
        lockedObjectDict[key].Value.GetComponentInChildren<TextMeshProUGUI>().text = PlayerPrefs.GetInt(key.ToString(), 2).ToString();
    }
}
