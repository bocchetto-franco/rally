using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RallyCheckpointTrigger : MonoBehaviour
{
    [SerializeField] RallyCheckpointManager manager;
    [SerializeField] int checkpointIndex;

    public int CheckpointIndex => checkpointIndex;

    public void Configure(RallyCheckpointManager checkpointManager, int index)
    {
        manager = checkpointManager;
        checkpointIndex = index;
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        ResolveManager(other)?.TryPass(checkpointIndex, other);
    }

    void OnTriggerStay(Collider other)
    {
        // Covers vehicles that begin a frame overlapping the start gate.
        // TryPass is ordered/idempotent, so repeated physics callbacks are safe.
        ResolveManager(other)?.TryPass(checkpointIndex, other);
    }

    RallyCheckpointManager ResolveManager(Collider other)
    {
        RallyLocalPlayerInput player = other.attachedRigidbody != null ?
            other.attachedRigidbody.GetComponent<RallyLocalPlayerInput>() : null;
        return player != null ? player.Checkpoints : manager;
    }
}
