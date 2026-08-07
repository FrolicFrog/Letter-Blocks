using UnityEngine;
using System.Collections.Generic;

public class TrayCubeScaler : MonoBehaviour
{
    [Header("Execution Settings")]
    public bool scaleInUpdate = true;
    [Tooltip("You MUST have 'Gizmos' turned on in your Scene View toolbar to see these lines!")]
    public bool showDebugRays = true;

    [Header("Grid & Sensor Settings (Local Space)")]
    public float cellSize = 3.06f;
    [Tooltip("World space height to elevate the sensor. Increase this if rays are buried inside the floor mesh!")]
    public float sensorElevation = 0.5f;
    public float cornerSensorSpread = 0.4f;

    [Header("Wall Padding (Thickness)")]
    public float paddingX = 0.06f;
    public float paddingZ = 0;

    [Header("Scale Settings")]
    public float scaleMultiplierXZ = 1.0f;
    public float targetHeight = 2.65f;

    void Start()
    {
        if (!scaleInUpdate) FitCubesToChunks();
    }

    void Update()
    {
        if (scaleInUpdate) FitCubesToChunks();
    }

    private void FitCubesToChunks()
    {
        float localHalfCell = cellSize / 2f;
        scaleInUpdate = false;
        foreach (Transform chunk in transform)
        {
            // 1. Excludes the yellow cubes AND any of their child meshes/colliders
            List<Collider> wallColliders = new List<Collider>();
            foreach (Collider col in chunk.GetComponentsInChildren<Collider>())
            {
                bool isCubePart = false;
                foreach (Transform child in chunk)
                {
                    if (col.transform == child || col.transform.IsChildOf(child))
                    {
                        isCubePart = true;
                        break;
                    }
                }
                if (!isCubePart) wallColliders.Add(col);
            }

            foreach (Transform cube in chunk)
            {
                // 2. Elevate sensor purely in World Space so it always sits above the floor
                Vector3 origin = chunk.position + (Vector3.up * sensorElevation);

                // Measure space purely in Local Space units
                float rightSpace = GetLocalSpace(chunk, Vector3.right, localHalfCell, paddingX, wallColliders, origin);
                float leftSpace = GetLocalSpace(chunk, Vector3.left, localHalfCell, paddingX, wallColliders, origin);
                float forwardSpace = GetLocalSpace(chunk, Vector3.forward, localHalfCell, paddingZ, wallColliders, origin);
                float backSpace = GetLocalSpace(chunk, Vector3.back, localHalfCell, paddingZ, wallColliders, origin);

                float targetSizeX = rightSpace + leftSpace;
                float targetSizeZ = forwardSpace + backSpace;

                float offsetX = (rightSpace - leftSpace) / 2f;
                float offsetZ = (forwardSpace - backSpace) / 2f;

                // Apply Position directly in Local Space
                cube.localPosition = new Vector3(offsetX, cube.localPosition.y, offsetZ);

                MeshFilter mf = cube.GetComponentInChildren<MeshFilter>();
                Vector3 baseSize = mf != null ? mf.sharedMesh.bounds.size : Vector3.one;

                baseSize.x = Mathf.Max(0.01f, baseSize.x);
                baseSize.y = Mathf.Max(0.01f, baseSize.y);
                baseSize.z = Mathf.Max(0.01f, baseSize.z);

                // 3. Apply Final Scale (Now strictly Local for X, Y, and Z)
                cube.localScale = new Vector3(
                    (targetSizeX / baseSize.x) * scaleMultiplierXZ,
                    targetHeight / baseSize.y,
                    (targetSizeZ / baseSize.z) * scaleMultiplierXZ
                );
            }
        }
    }

    private float GetLocalSpace(Transform chunk, Vector3 localDirection, float maxLocalDist, float padding, List<Collider> walls, Vector3 origin)
    {
        Vector3 worldDir = chunk.TransformDirection(localDirection).normalized;
        Vector3 localCross = (localDirection.x != 0) ? Vector3.forward : Vector3.right;
        Vector3 worldCross = chunk.TransformDirection(localCross).normalized;

        float scaleInDir = chunk.TransformVector(localDirection).magnitude;
        float scaleInCross = chunk.TransformVector(localCross).magnitude;

        float worldMaxDist = maxLocalDist * scaleInDir;
        float rayLength = worldMaxDist * 2.5f;

        Vector3[] rayOrigins = new Vector3[]
        {
            origin,
            origin + (worldCross * (cornerSensorSpread * scaleInCross)),
            origin - (worldCross * (cornerSensorSpread * scaleInCross))
        };

        float closestWorldHit = rayLength;
        bool hitWall = false;

        foreach (Vector3 ro in rayOrigins)
        {
            Ray ray = new Ray(ro, worldDir);
            foreach (Collider col in walls)
            {
                if (col.Raycast(ray, out RaycastHit hit, rayLength))
                {
                    if (hit.distance < closestWorldHit)
                    {
                        closestWorldHit = hit.distance;
                        hitWall = true;
                    }
                }
            }

            if (showDebugRays)
            {
                Debug.DrawRay(ro, worldDir * (hitWall ? closestWorldHit : worldMaxDist), hitWall ? Color.red : Color.green);
            }
        }

        if (hitWall)
        {
            float localHitDist = closestWorldHit / scaleInDir;
            return Mathf.Max(0.01f, localHitDist - padding);
        }

        return maxLocalDist;
    }
}