using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TraySpliter : MonoBehaviour
{
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    public bool horizontalLock = false;
    public static Material trayMat;

    private Dictionary<Transform, string> initialTileStrings = new Dictionary<Transform, string>();
    private bool isInitialized = false;

    private void Start()
    {
        InitializeTileMapping();
    }

    public void UpdateGridPosition(int deltaRow, int deltaCol)
    {
        // Handled dynamically on Split()
    }

    /// <summary>
    /// Binds each physical child tile GameObject to its original dictionary string at spawn.
    /// </summary>
    public void InitializeTileMapping()
    {
        if (isInitialized) return;
        if (BottomGridManager.Instance == null || trayCells == null || trayCells.Count == 0) return;

        Transform gridTransform = BottomGridManager.Instance.transform;
        int width = BottomGridManager.Instance.width;
        int height = BottomGridManager.Instance.height;
        int totalSlots = width * height;
        if (width <= 0 || gridTransform.childCount == 0) return;

        bool is2D = (GlobalTrayDragger.Instance != null &&
                     GlobalTrayDragger.Instance.planeMode == GlobalTrayDragger.PlaneAxisMode.XY_FrontalPlane_2D);

        foreach (Transform child in transform)
        {
            if (child.name.Contains("Wall") || child.name.Contains("Arrow") || child.name.Contains("Border")) continue;
            if (child.GetComponentInChildren<TextMeshPro>(true) == null) continue;

            int closestIndex = -1;
            float minDistance = float.MaxValue;

            for (int s = 0; s < totalSlots && s < gridTransform.childCount; s++)
            {
                Transform slot = gridTransform.GetChild(s);
                float dist = is2D
                    ? Vector2.Distance(new Vector2(child.position.x, child.position.y), new Vector2(slot.position.x, slot.position.y))
                    : Vector2.Distance(new Vector2(child.position.x, child.position.z), new Vector2(slot.position.x, slot.position.z));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestIndex = s;
                }
            }

            if (closestIndex != -1)
            {
                int row = closestIndex / width;
                int col = closestIndex % width;
                Vector2Int coord = new Vector2Int(row, col);

                if (trayCells.TryGetValue(coord, out string cellVal))
                {
                    initialTileStrings[child] = cellVal;
                }
            }
        }

        if (initialTileStrings.Count > 0)
        {
            isInitialized = true;
        }
    }

    /// <summary>
    /// Reconstructs grid coordinates and dictionary keys for surviving tiles only.
    /// </summary>
    private void SyncGridCoordinatesAndPruneJumpedTiles()
    {
        InitializeTileMapping();

        if (BottomGridManager.Instance == null) return;

        Transform gridTransform = BottomGridManager.Instance.transform;
        int width = BottomGridManager.Instance.width;
        int height = BottomGridManager.Instance.height;
        int totalSlots = width * height;
        if (width <= 0 || gridTransform.childCount == 0) return;

        bool is2D = (GlobalTrayDragger.Instance != null &&
                     GlobalTrayDragger.Instance.planeMode == GlobalTrayDragger.PlaneAxisMode.XY_FrontalPlane_2D);

        List<Vector2Int> freshTrayPos = new List<Vector2Int>();
        Dictionary<Vector2Int, string> freshTrayCells = new Dictionary<Vector2Int, string>();

        foreach (Transform child in transform)
        {
            // Skip fully jumped tiles, inactive objects, and tray borders
            if (child.name == "JumpingTile" || !child.gameObject.activeSelf) continue;
            if (child.name.Contains("Wall") || child.name.Contains("Arrow") || child.name.Contains("Border")) continue;
            if (child.GetComponentInChildren<TextMeshPro>(true) == null) continue;

            string tileString = ResolveCurrentTileString(child);
            if (string.IsNullOrEmpty(tileString)) continue;

            // Locate current grid slot underneath this tile
            int closestIndex = -1;
            float minDistance = float.MaxValue;

            for (int s = 0; s < totalSlots && s < gridTransform.childCount; s++)
            {
                Transform slot = gridTransform.GetChild(s);
                float dist = is2D
                    ? Vector2.Distance(new Vector2(child.position.x, child.position.y), new Vector2(slot.position.x, slot.position.y))
                    : Vector2.Distance(new Vector2(child.position.x, child.position.z), new Vector2(slot.position.x, slot.position.z));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestIndex = s;
                }
            }

            if (closestIndex != -1)
            {
                int row = closestIndex / width;
                int col = closestIndex % width;
                Vector2Int coord = new Vector2Int(row, col);

                if (!freshTrayPos.Contains(coord))
                {
                    freshTrayPos.Add(coord);
                }
                freshTrayCells[coord] = tileString;
            }
        }

        trayPos = freshTrayPos;
        trayCells = freshTrayCells;
    }

    private string ResolveCurrentTileString(Transform tile)
    {
        Transform nestedTile = tile.Find("Tile letter");
        Transform text0 = tile.Find("Text 0");

        bool hasNestedTile = nestedTile != null &&
                             nestedTile.parent == tile &&
                             nestedTile.gameObject.activeSelf &&
                             nestedTile.name != "JumpingTile";

        if (initialTileStrings.TryGetValue(tile, out string origString))
        {
            if (origString.Length >= 2)
            {
                if (hasNestedTile)
                {
                    // Both layers present: Retains full original double letter string (e.g. "T*", "AB")
                    return origString;
                }
                else
                {
                    // Top layer jumped: Exposes base layer and passes remaining character
                    if (text0 != null) text0.gameObject.SetActive(true);
                    return origString.Substring(1);
                }
            }
            else
            {
                // Single letter tile
                return origString;
            }
        }

        // Fallback in case tile mapping was not cached
        if (hasNestedTile && text0 != null)
        {
            var tm1 = nestedTile.GetComponentInChildren<TextMeshPro>(true);
            var tm0 = text0.GetComponentInChildren<TextMeshPro>(true);
            string top = (tm1 != null && !string.IsNullOrWhiteSpace(tm1.text)) ? tm1.text.Trim() : "*";
            string bot = (tm0 != null && !string.IsNullOrWhiteSpace(tm0.text)) ? tm0.text.Trim() : "*";
            return top + bot;
        }
        else if (text0 != null && text0.gameObject.activeSelf)
        {
            var tm0 = text0.GetComponentInChildren<TextMeshPro>(true);
            return (tm0 != null && !string.IsNullOrWhiteSpace(tm0.text)) ? tm0.text.Trim() : "*";
        }
        else
        {
            var tm = tile.GetComponentInChildren<TextMeshPro>(true);
            return (tm != null && !string.IsNullOrWhiteSpace(tm.text)) ? tm.text.Trim() : "*";
        }
    }

    public void Split()
    {
        SyncGridCoordinatesAndPruneJumpedTiles();

        if (trayPos != null && trayPos.Count > 0)
        {
            if (horizontalLock)
            {
                BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, true, true).tag = "Vertical";
            }
            else
            {
                BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, false, true);
            }
        }

        Destroy(gameObject);
    }
}