using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TrayCubeScaler : MonoBehaviour
{
    [Header("Execution Settings")]
    public bool scaleInUpdate = true;
    [Tooltip("You MUST have 'Gizmos' turned on in your Scene View toolbar to see these lines!")]
    public bool showDebugRays = true;

    [Header("Grid & Sensor Settings (Local Space)")]
    public float cellSize = 3.55f;
    [Tooltip("World space height to elevate the sensor. Increase this if rays are buried inside the floor mesh!")]
    public float sensorElevation = 0.5f;

    [Header("Wall Padding (Thickness)")]
    public float paddingX = 0.06f;
    public float paddingZ = 0f;

    [Header("Scale Settings")]
    public float scaleMultiplierXZ = 1.0f;
    public float targetHeight = 2.65f;

    void Start()
    {
        if (!scaleInUpdate) FitCubesToChunks();
        StartCoroutine(CanceScale());
    }

    void Update()
    {
        if (scaleInUpdate)
        {
            FitCubesToChunks();
        }
    }

    private void FitCubesToChunks()
    {
        float maxLocalDist = cellSize / 2f;

        // 1. GLOBAL WALL GATHERING: Get EVERY wall in the entire tray. 
        // This is crucial because inner corners belong to neighboring chunks!
        List<Collider> allTrayWalls = new List<Collider>();
        foreach (Collider col in transform.GetComponentsInChildren<Collider>())
        {
            bool isCube = false;
            // Filter out any collider that belongs to any yellow cube in the tray
            foreach (Transform chunk in transform)
            {
                foreach (Transform cube in chunk)
                {
                    if (col.transform == cube || col.transform.IsChildOf(cube))
                    {
                        isCube = true;
                        break;
                    }
                }
                if (isCube) break;
            }
            if (!isCube) allTrayWalls.Add(col);
        }

        foreach (Transform chunk in transform)
        {
            foreach (Transform cube in chunk)
            {
                // Elevate sensor purely in World Space so it sits above the floor
                Vector3 origin = chunk.position + (Vector3.up * sensorElevation);

                // --- PASS 1: Center rays to find raw extent limits ---
                float rawRight = GetRawHit(chunk, Vector3.right, maxLocalDist, allTrayWalls, origin);
                float rawLeft = GetRawHit(chunk, Vector3.left, maxLocalDist, allTrayWalls, origin);
                float rawForward = GetRawHit(chunk, Vector3.forward, maxLocalDist, allTrayWalls, origin);
                float rawBack = GetRawHit(chunk, Vector3.back, maxLocalDist, allTrayWalls, origin);

                // --- PASS 2: 5-Ray sweeps using the raw extents to find exact safe space against inner corners ---
                float rightSpace = GetSweptSpace(chunk, Vector3.right, maxLocalDist, paddingX, allTrayWalls, origin, rawForward, rawBack);
                float leftSpace = GetSweptSpace(chunk, Vector3.left, maxLocalDist, paddingX, allTrayWalls, origin, rawForward, rawBack);
                float forwardSpace = GetSweptSpace(chunk, Vector3.forward, maxLocalDist, paddingZ, allTrayWalls, origin, rawRight, rawLeft);
                float backSpace = GetSweptSpace(chunk, Vector3.back, maxLocalDist, paddingZ, allTrayWalls, origin, rawRight, rawLeft);

                // Apply constraints and calculate offsets
                rightSpace = Mathf.Max(0.01f, rightSpace);
                leftSpace = Mathf.Max(0.01f, leftSpace);
                forwardSpace = Mathf.Max(0.01f, forwardSpace);
                backSpace = Mathf.Max(0.01f, backSpace);

                float targetSizeX = rightSpace + leftSpace;
                float targetSizeZ = forwardSpace + backSpace;
                float offsetX = (rightSpace - leftSpace) / 2f;
                float offsetZ = (forwardSpace - backSpace) / 2f;

                cube.localPosition = new Vector3(offsetX, cube.localPosition.y, offsetZ);

                MeshFilter mf = cube.GetComponentInChildren<MeshFilter>();
                Vector3 baseSize = mf != null ? mf.sharedMesh.bounds.size : Vector3.one;

                baseSize.x = Mathf.Max(0.01f, baseSize.x);
                baseSize.y = Mathf.Max(0.01f, baseSize.y);
                baseSize.z = Mathf.Max(0.01f, baseSize.z);

                cube.localScale = new Vector3(
                    (targetSizeX / baseSize.x) * scaleMultiplierXZ,
                    targetHeight / baseSize.y,
                    (targetSizeZ / baseSize.z) * scaleMultiplierXZ
                );
            }
        }
    }

    /// <summary>
    /// Shoots a single center ray to find the absolute maximum distance a face can travel.
    /// </summary>
    private float GetRawHit(Transform chunk, Vector3 localDirection, float maxLocalDist, List<Collider> walls, Vector3 origin)
    {
        Vector3 worldDir = chunk.TransformDirection(localDirection).normalized;
        float scaleInDir = chunk.TransformVector(localDirection).magnitude;
        float rayLength = maxLocalDist * scaleInDir * 1.5f;

        float closestWorldHit = rayLength;
        bool hitWall = false;

        Ray ray = new Ray(origin, worldDir);
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

        if (hitWall) return closestWorldHit / scaleInDir;
        return maxLocalDist;
    }

    /// <summary>
    /// Shoots 5 parallel rays across the width of the cube face to detect jutting corners.
    /// </summary>
    private float GetSweptSpace(Transform chunk, Vector3 localDirection, float maxLocalDist, float padding, List<Collider> walls, Vector3 origin, float crossPosBound, float crossNegBound)
    {
        Vector3 worldDir = chunk.TransformDirection(localDirection).normalized;

        Vector3 localCrossPos = (localDirection.x != 0) ? Vector3.forward : Vector3.right;
        Vector3 localCrossNeg = (localDirection.x != 0) ? Vector3.back : Vector3.left;

        Vector3 worldCrossPos = chunk.TransformDirection(localCrossPos).normalized;
        Vector3 worldCrossNeg = chunk.TransformDirection(localCrossNeg).normalized;

        float scaleInDir = chunk.TransformVector(localDirection).magnitude;
        float scaleInCrossPos = chunk.TransformVector(localCrossPos).magnitude;
        float scaleInCrossNeg = chunk.TransformVector(localCrossNeg).magnitude;

        float worldMaxDist = maxLocalDist * scaleInDir;
        float rayLength = worldMaxDist * 1.5f;

        // Inset slightly to prevent scraping parallel walls
        float inset = 0.02f;
        float safeCrossPos = Mathf.Max(0, crossPosBound - inset) * scaleInCrossPos;
        float safeCrossNeg = Mathf.Max(0, crossNegBound - inset) * scaleInCrossNeg;

        // 5-Ray Sweep: Center, 50% Edges, and 100% Edges
        Vector3[] rayOrigins = new Vector3[]
        {
            origin,
            origin + (worldCrossPos * safeCrossPos),
            origin + (worldCrossNeg * safeCrossNeg),
            origin + (worldCrossPos * (safeCrossPos * 0.5f)),
            origin + (worldCrossNeg * (safeCrossNeg * 0.5f))
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

    IEnumerator CanceScale()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        scaleInUpdate = false;
    }
}