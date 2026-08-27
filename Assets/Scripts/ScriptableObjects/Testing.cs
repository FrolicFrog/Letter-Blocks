using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Testing : MonoBehaviour
{
    public RectTransform uiObject;
    public Camera uiCamera;      // The Overlay camera assigned to Canvas
    public Camera mainCamera;    // The Base/Main camera
    public Transform target;     // Where the object should finally go
    public GameObject prefab;
    [ContextMenu("Create")]
    public void Spawn()
    {
        // 1. UI position → screen position
        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                uiObject.position
            );

        // 2. Screen position → ray from Main Camera
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        // 3. Pick a point along that ray
        Vector3 startPosition = ray.GetPoint(2f);

        // 4. Spawn there
        GameObject obj = Instantiate(
            prefab,
            startPosition,
            Quaternion.identity
        );
    }
}
