using System.Collections.Generic;
using UnityEngine;

public class TraySpliter : MonoBehaviour
{
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    public bool horizontalLock = false;
    public static Material trayMat;

    /// <summary>
    /// Shifts internal grid coordinates and dictionary keys by row and column deltas.
    /// Leaves all string values ("T*", "AB", etc.) completely untouched.
    /// </summary>
    public void UpdateGridPosition(int deltaRow, int deltaCol)
    {
        if (deltaRow == 0 && deltaCol == 0) return;

        Vector2Int offset = new Vector2Int(deltaRow, deltaCol);

        if (trayPos != null)
        {
            for (int i = 0; i < trayPos.Count; i++)
            {
                trayPos[i] += offset;
            }
        }

        if (trayCells != null)
        {
            Dictionary<Vector2Int, string> updatedCells = new Dictionary<Vector2Int, string>();
            foreach (var kvp in trayCells)
            {
                updatedCells[kvp.Key + offset] = kvp.Value;
            }
            trayCells = updatedCells;
        }
    }

    /// <summary>
    /// Compares the physical world position of the tray against BottomGridManager
    /// to ensure Vector2Int keys perfectly match where the tray currently sits before splitting.
    /// </summary>
    private void SyncGridCoordinatesToCurrentWorldPosition()
    {
        if (BottomGridManager.Instance == null || trayPos == null || trayPos.Count == 0) return;

        Transform gridTransform = BottomGridManager.Instance.transform;
        int width = BottomGridManager.Instance.width;
        int height = BottomGridManager.Instance.height;
        int totalSlots = width * height;

        if (width <= 0 || gridTransform.childCount == 0) return;

        // Find the first valid active tile representing trayPos[0]
        Transform referenceTile = null;
        foreach (Transform child in transform)
        {
            if (child.name == "JumpingTile" || !child.gameObject.activeSelf) continue;
            if (child.name.Contains("Wall") || child.name.Contains("Arrow") || child.name.Contains("Border")) continue;
            referenceTile = child;
            break;
        }

        Vector3 currentWorldPos = referenceTile != null ? referenceTile.position : transform.position;

        bool is2D = (GlobalTrayDragger.Instance != null &&
                     GlobalTrayDragger.Instance.planeMode == GlobalTrayDragger.PlaneAxisMode.XY_FrontalPlane_2D);

        // Find closest slot in BottomGridManager to current position
        int closestIndex = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < totalSlots && i < gridTransform.childCount; i++)
        {
            Transform slot = gridTransform.GetChild(i);
            float dist = is2D
                ? Vector2.Distance(new Vector2(currentWorldPos.x, currentWorldPos.y), new Vector2(slot.position.x, slot.position.y))
                : Vector2.Distance(new Vector2(currentWorldPos.x, currentWorldPos.z), new Vector2(slot.position.x, slot.position.z));

            if (dist < minDistance)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }

        if (closestIndex != -1)
        {
            int currentGridRow = closestIndex / width;
            int currentGridCol = closestIndex % width;

            // Calculate exact delta between current grid slot and original trayPos[0]
            int deltaRow = currentGridRow - trayPos[0].x;
            int deltaCol = currentGridCol - trayPos[0].y;

            UpdateGridPosition(deltaRow, deltaCol);
        }
    }

    public void Split()
    {
        // 1. Re-align Vector2Int keys with the current physical position in the grid
        SyncGridCoordinatesToCurrentWorldPosition();

        // 2. Re-create trays at the updated coordinates
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