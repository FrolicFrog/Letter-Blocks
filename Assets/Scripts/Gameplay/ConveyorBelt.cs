using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [Tooltip("Place an empty GameObject at the end of THIS specific belt segment.")]
    public Transform endPoint;

    [Tooltip("Drag the NEXT conveyor belt segment here. Leave empty if this is the final stop.")]
    public ConveyorBelt nextBelt;
}