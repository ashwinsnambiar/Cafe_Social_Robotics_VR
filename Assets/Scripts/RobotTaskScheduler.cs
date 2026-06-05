using UnityEngine;

public class RobotTaskScheduler : MonoBehaviour
{
    [Header("Robot Modules")]
    public DeliveryRobot deliveryModule;
    public RobotCleanupSequence cleanupModule;

    [Header("Events")]
    public DistractionEvent distractionEvent;

    private bool pendingCleanup = false;
    private Transform pendingSpillLocation;

    void Start()
    {
        // 1. Subscribe to the events
        if (distractionEvent != null)
        {
            distractionEvent.onCrashOccurred.AddListener(OnSpillDetected);
        }

        // Listen for when modules finish their tasks
        deliveryModule.OnDeliveryFinished += CheckPendingTasks;
        cleanupModule.OnCleanupComplete += ResumeDelivery;

        // 2. Start normal cafe operations
        deliveryModule.EnableDelivery();
    }

    void OnDestroy()
    {
        if (distractionEvent != null)
        {
            distractionEvent.onCrashOccurred.RemoveListener(OnSpillDetected);
        }
        deliveryModule.OnDeliveryFinished -= CheckPendingTasks;
        cleanupModule.OnCleanupComplete -= ResumeDelivery;
    }

    // Triggered by DistractionEvent.cs
    public void OnSpillDetected(Transform spillLocation)
    {
        Debug.Log("Spill detected! Queuing cleanup task.");
        if (spillLocation != null)
        {
            // Create a temporary, lightweight dummy GameObject to preserve the position and rotation
            // before the original spillLocation transform is destroyed.
            GameObject dummy = new GameObject("PendingSpillLocationDummy");
            dummy.transform.position = spillLocation.position;
            dummy.transform.rotation = spillLocation.rotation;
            pendingSpillLocation = dummy.transform;
        }
        pendingCleanup = true;

        // Tell the delivery module to stop taking new orders.
        // If it is actively delivering, it will finish its current order first.
        deliveryModule.StopDeliverySafely();
    }

    // Called automatically by DeliveryRobot when it finishes an order (or if it was idling)
    private void CheckPendingTasks()
    {
        if (pendingCleanup)
        {
            Debug.Log("Delivery finished. Starting cleanup sequence.");
            cleanupModule.StartCleanupSequence(pendingSpillLocation);

            // Clean up the dummy GameObject now that its position has been read
            if (pendingSpillLocation != null && pendingSpillLocation.name == "PendingSpillLocationDummy")
            {
                Destroy(pendingSpillLocation.gameObject);
                pendingSpillLocation = null;
            }
        }
        else
        {
            // If nothing is pending, keep taking delivery orders
            deliveryModule.EnableDelivery();
        }
    }

    // Called automatically by RobotCleanupSequence when the mess is gone
    private void ResumeDelivery()
    {
        Debug.Log("Cleanup complete. Resuming delivery operations.");
        pendingCleanup = false;
        
        if (pendingSpillLocation != null && pendingSpillLocation.name == "PendingSpillLocationDummy")
        {
            Destroy(pendingSpillLocation.gameObject);
        }
        pendingSpillLocation = null;

        deliveryModule.EnableDelivery();
    }
}