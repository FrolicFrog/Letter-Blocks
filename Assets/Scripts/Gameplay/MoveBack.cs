using UnityEngine;

public class MoveBack : MonoBehaviour
{
    [Header("Target To Move")]
    [Tooltip("The GameObject/Transform that will move. If left unassigned, defaults to this GameObject.")]
  public Transform objectToMove;

    [Header("Position Offsets & Target")]
    [Tooltip("Offset added to the attached GameObject's position for the starting point.")]
    [SerializeField] private Vector3 startOffset = new Vector3(1,6,-4);

    [Tooltip("The destination Z coordinate in world space.")]
    public float targetZ = 10f;

    [Header("Movement Settings")]
    [Tooltip("Movement speed along the Z axis in units per second.")]
    [SerializeField] private float moveSpeed = 6.2f;

    [Tooltip("Tolerance distance to consider the target Z reached.")]
    [SerializeField] private float arrivalThreshold = 0.05f;

    private void Start()
    {
        // Default to the attached object if none is referenced
        if (objectToMove == null)
        {
            objectToMove = transform;
        }

        // Place the target object at the start position
        SnapToStartPosition();
    }

    private void Update()
    {
        if (objectToMove == null) return;

        // Target position keeping the moving object's X and Y, changing only Z
        Vector3 targetPosition = new Vector3(
            objectToMove.position.x,
            objectToMove.position.y,
            targetZ
        );

        // Move the target GameObject towards targetZ
        objectToMove.position = Vector3.MoveTowards(
            objectToMove.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        // Check if the moving object reached targetZ
        if (Mathf.Abs(objectToMove.position.z - targetZ) <= arrivalThreshold)
        {
            SnapToStartPosition();
        }
    }

    /// <summary>
    /// Snaps the target object back to the attached GameObject's position + offset.
    /// </summary>
    public void SnapToStartPosition()
    {
        if (objectToMove != null)
        {
            objectToMove.position = transform.position + startOffset;
        }
    }
}