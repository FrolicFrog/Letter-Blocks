using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpLockManager : MonoBehaviour
{
    public GameObject panel;
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
                    if(LevelManager.Instance.CurLevelNumber == lvl)
                    {
                        lockedObjectDict[lvl].Key.GetComponentInParent<UIElementScaler>(true).StartScaling();
                        panel.SetActive(true);
                    }
                }
                if (lockedObjectDict[lvl].Value.GetComponentInChildren<TextMeshProUGUI>(true).text == "0")
                {
                    lockedObjectDict[lvl].Key.GetComponentInParent<Toggle>(true).interactable = false;
                   
                    lockedObjectDict[lvl].Value.SetActive(false);
                    lockedObjectDict[lvl].Value.transform.parent.GetChild(1).gameObject.SetActive(false);
                    lockedObjectDict[lvl].Value.transform.parent.GetChild(2).gameObject.SetActive(true);
                }
                else
                {

                    lockedObjectDict[lvl].Value.SetActive(true);
                    lockedObjectDict[lvl].Key.GetComponentInParent<Toggle>(true).interactable = true;
                    lockedObjectDict[lvl].Value.transform.parent.GetChild(1).gameObject.SetActive(true);
                    lockedObjectDict[lvl].Value.transform.parent.GetChild(2).gameObject.SetActive(false);
                }
            }
            
        }

    }

    public void UpdatePowerUpQuantity(int key,int quantity)
    {
        PlayerPrefs.SetInt(key.ToString(), PlayerPrefs.GetInt(key.ToString(), 2)+quantity);
        lockedObjectDict[key].Value.GetComponentInChildren<TextMeshProUGUI>().text = PlayerPrefs.GetInt(key.ToString(), 2).ToString();
    }
    public void UpdatePowerUpQuantity(int key)
    {
        PlayerPrefs.SetInt(key.ToString(), PlayerPrefs.GetInt(key.ToString(), 2) +1);
        lockedObjectDict[key].Value.GetComponentInChildren<TextMeshProUGUI>().text = PlayerPrefs.GetInt(key.ToString(), 2).ToString();
    }
}
