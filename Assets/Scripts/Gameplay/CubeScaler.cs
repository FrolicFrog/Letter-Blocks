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

    // Helper class to store data between Pass 1 and Pass 2
    private class CubeSpaceData
    {
        public Transform chunk;
        public Transform cube;
        public float rightSpace, leftSpace, forwardSpace, backSpace;
        public Vector3 baseSize;
    }

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

        // 1. GLOBAL WALL GATHERING
        List<Collider> allTrayWalls = new List<Collider>();
        foreach (Collider col in transform.GetComponentsInChildren<Collider>())
        {
            bool isCube = false;
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

        // --- PASS 1: Calculate Maximum Wall Bounds ---
        List<CubeSpaceData> cubeDatas = new List<CubeSpaceData>();

        foreach (Transform chunk in transform)
        {
            foreach (Transform cube in chunk)
            {
                Vector3 origin = chunk.position + (Vector3.up * sensorElevation);

                // Raw Extents
                float rawRight = GetRawHit(chunk, Vector3.right, maxLocalDist, allTrayWalls, origin);
                float rawLeft = GetRawHit(chunk, Vector3.left, maxLocalDist, allTrayWalls, origin);
                float rawForward = GetRawHit(chunk, Vector3.forward, maxLocalDist, allTrayWalls, origin);
                float rawBack = GetRawHit(chunk, Vector3.back, maxLocalDist, allTrayWalls, origin);

                // Sweeps
                float rightSpace = GetSweptSpace(chunk, Vector3.right, maxLocalDist, paddingX, allTrayWalls, origin, rawForward, rawBack);
                float leftSpace = GetSweptSpace(chunk, Vector3.left, maxLocalDist, paddingX, allTrayWalls, origin, rawForward, rawBack);
                float forwardSpace = GetSweptSpace(chunk, Vector3.forward, maxLocalDist, paddingZ, allTrayWalls, origin, rawRight, rawLeft);
                float backSpace = GetSweptSpace(chunk, Vector3.back, maxLocalDist, paddingZ, allTrayWalls, origin, rawRight, rawLeft);

                MeshFilter mf = cube.GetComponentInChildren<MeshFilter>();
                Vector3 baseSize = mf != null ? mf.sharedMesh.bounds.size : Vector3.one;

                // Store provisional sizes
                cubeDatas.Add(new CubeSpaceData
                {
                    chunk = chunk,
                    cube = cube,
                    rightSpace = rightSpace,
                    leftSpace = leftSpace,
                    forwardSpace = forwardSpace,
                    backSpace = backSpace,
                    baseSize = new Vector3(Mathf.Max(0.01f, baseSize.x), Mathf.Max(0.01f, baseSize.y), Mathf.Max(0.01f, baseSize.z))
                });
            }
        }

        // --- PASS 2: Check & Resolve Intersections (Overlaps) ---
        float orthoTolerance = maxLocalDist * 0.8f;

        for (int i = 0; i < cubeDatas.Count; i++)
        {
            for (int j = i + 1; j < cubeDatas.Count; j++)
            {
                CubeSpaceData A = cubeDatas[i];
                CubeSpaceData B = cubeDatas[j];

                // Get distance between chunks in local space
                Vector3 toB = A.chunk.InverseTransformPoint(B.chunk.position);

                // X-Axis Check (Right / Left Neighbors)
                if (Mathf.Abs(toB.z) <= orthoTolerance)
                {
                    if (toB.x > 0.1f) // B is to the Right of A
                    {
                        float dist = toB.x;
                        float overlap = (A.rightSpace + B.leftSpace) - dist;
                        if (overlap > 0)
                        {
                            A.rightSpace -= overlap / 2f;
                            B.leftSpace -= overlap / 2f;
                        }
                    }
                    else if (toB.x < -0.1f) // B is to the Left of A
                    {
                        float dist = Mathf.Abs(toB.x);
                        float overlap = (A.leftSpace + B.rightSpace) - dist;
                        if (overlap > 0)
                        {
                            A.leftSpace -= overlap / 2f;
                            B.rightSpace -= overlap / 2f;
                        }
                    }
                }

                // Z-Axis Check (Forward / Back Neighbors)
                if (Mathf.Abs(toB.x) <= orthoTolerance)
                {
                    if (toB.z > 0.1f) // B is Forward of A
                    {
                        float dist = toB.z;
                        float overlap = (A.forwardSpace + B.backSpace) - dist;
                        if (overlap > 0)
                        {
                            A.forwardSpace -= overlap / 2f;
                            B.backSpace -= overlap / 2f;
                        }
                    }
                    else if (toB.z < -0.1f) // B is Back of A
                    {
                        float dist = Mathf.Abs(toB.z);
                        float overlap = (A.backSpace + B.forwardSpace) - dist;
                        if (overlap > 0)
                        {
                            A.backSpace -= overlap / 2f;
                            B.forwardSpace -= overlap / 2f;
                        }
                    }
                }
            }
        }

        // --- PASS 3: Apply Final Adjusted Sizes & Positions ---
        foreach (var data in cubeDatas)
        {
            data.rightSpace = Mathf.Max(0.01f, data.rightSpace);
            data.leftSpace = Mathf.Max(0.01f, data.leftSpace);
            data.forwardSpace = Mathf.Max(0.01f, data.forwardSpace);
            data.backSpace = Mathf.Max(0.01f, data.backSpace);

            float targetSizeX = data.rightSpace + data.leftSpace;
            float targetSizeZ = data.forwardSpace + data.backSpace;
            float offsetX = (data.rightSpace - data.leftSpace) / 2f;
            float offsetZ = (data.forwardSpace - data.backSpace) / 2f;

            data.cube.localPosition = new Vector3(offsetX, data.cube.localPosition.y, offsetZ);

            data.cube.localScale = new Vector3(
                (targetSizeX / data.baseSize.x) * scaleMultiplierXZ,
                targetHeight / data.baseSize.y,
                (targetSizeZ / data.baseSize.z) * scaleMultiplierXZ
            );
        }
    }

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

        float inset = 0.02f;
        float safeCrossPos = Mathf.Max(0, crossPosBound - inset) * scaleInCrossPos;
        float safeCrossNeg = Mathf.Max(0, crossNegBound - inset) * scaleInCrossNeg;

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