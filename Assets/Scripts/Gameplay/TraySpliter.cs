using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TraySpliter : MonoBehaviour
{
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    public bool horizontalLock = false;
    public static Material trayMat;

    /// <summary>
    /// Included for compatibility with TrayDragger references.
    /// </summary>


    /// <summary>
    /// Scans the remaining children on the tray, discards jumped tiles,
    /// and recalculates exact grid coordinates based on current tile world positions.
    /// </summary>
    public void RebuildGridDataFromCurrentTiles()
    {
        if (BottomGridManager.Instance == null) return;

        Transform gridTransform = BottomGridManager.Instance.transform;
        int width = BottomGridManager.Instance.width;
        int height = BottomGridManager.Instance.height;
        int totalSlots = width * height;

        if (width <= 0 || gridTransform.childCount == 0) return;

        List<Vector2Int> freshTrayPos = new List<Vector2Int>();
        Dictionary<Vector2Int, string> freshTrayCells = new Dictionary<Vector2Int, string>();

        bool is2D = (GlobalTrayDragger.Instance != null &&
                     GlobalTrayDragger.Instance.planeMode == GlobalTrayDragger.PlaneAxisMode.XY_FrontalPlane_2D);

        foreach (Transform child in transform)
        {
            // Skip tiles that have jumped/are jumping or non-letter decorative objects
            if (child.name == "JumpingTile" || !child.gameObject.activeSelf) continue;

            string letter = GetTileLetter(child);
            if (string.IsNullOrEmpty(letter)) continue;

            // Find closest grid slot in BottomGridManager to this tile's current position
            int closestIndex = -1;
            float minDistance = float.MaxValue;

            for (int i = 0; i < totalSlots && i < gridTransform.childCount; i++)
            {
                Transform slot = gridTransform.GetChild(i);
                float dist = is2D
                    ? Vector2.Distance(new Vector2(child.position.x, child.position.y), new Vector2(slot.position.x, slot.position.y))
                    : Vector2.Distance(new Vector2(child.position.x, child.position.z), new Vector2(slot.position.x, slot.position.z));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestIndex = i;
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
                freshTrayCells[coord] = letter;
            }
        }

        trayPos = freshTrayPos;
        trayCells = freshTrayCells;
    }

    private string GetTileLetter(Transform tile)
    {
        Transform nestedTile = tile.Find("Tile letter");
        Transform text0 = tile.Find("Text 0");

        bool hasNestedTile = nestedTile != null &&
                             nestedTile.parent == tile &&
                             nestedTile.gameObject.activeSelf &&
                             nestedTile.name != "JumpingTile";

        // Double Letter: BOTH letters present (combines base Text 0 + top Tile letter)
        if (hasNestedTile && text0 != null)
        {
            // Read Text 0 with includeInactive = true since it stays inactive under the top tile
            var tm0 = text0.GetComponentInChildren<TextMeshPro>(true);
            var tm1 = nestedTile.GetComponentInChildren<TextMeshPro>(true);

            string baseChar = (tm0 != null) ? tm0.text : "";
            string topChar = (tm1 != null) ? tm1.text : "";

            if (!string.IsNullOrEmpty(baseChar) && !string.IsNullOrEmpty(topChar))
            {
                return baseChar + topChar; // Full 2-character string
            }
            if (!string.IsNullOrEmpty(topChar)) return topChar;
            if (!string.IsNullOrEmpty(baseChar)) return baseChar;
        }

        // Double Letter Base: Top letter already jumped, expose and read Text 0
        if (text0 != null)
        {
            text0.gameObject.SetActive(true);

            var tm0 = text0.GetComponentInChildren<TextMeshPro>(true);
            if (tm0 != null && !string.IsNullOrEmpty(tm0.text))
            {
                return tm0.text;
            }
        }

        // Nested tile present without Text 0
        if (hasNestedTile)
        {
            var tm1 = nestedTile.GetComponentInChildren<TextMeshPro>(true);
            if (tm1 != null && !string.IsNullOrEmpty(tm1.text))
            {
                return tm1.text;
            }
        }

        // Standard single tile letter
        var directTm = tile.GetComponentInChildren<TextMeshPro>(true);
        if (directTm != null && !string.IsNullOrEmpty(directTm.text))
        {
            return directTm.text;
        }

        return null;
    }

    public void Split()
    {
        RebuildGridDataFromCurrentTiles();

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