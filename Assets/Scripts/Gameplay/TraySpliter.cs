using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TraySpliter : MonoBehaviour
{
    [HideInInspector] public List<Vector2Int> trayPos;
    [HideInInspector] public Dictionary<Vector2Int, string> trayCells;
    [HideInInspector] public bool horizontalLock = false;
    public static Material trayMat;

    /// <summary>
    /// Shifts internal grid coordinates by row and column movement deltas.
    /// </summary>
    public void UpdateGridPosition(int deltaRow, int deltaCol)
    {
        if (deltaRow == 0 && deltaCol == 0) return;

        // Grid system: (x: Row, y: Column)
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

    public void Split()
    {
        if (horizontalLock)
        {
            BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, true, true).tag = "Vertical";
        }
        else
        {
            BottomGridManager.Instance.CreateTray(trayPos, 2.4f, trayMat, new Vector3(.995f, .988f, .988f), true, trayCells, false, true);
        }
        Destroy(gameObject);
    }
}