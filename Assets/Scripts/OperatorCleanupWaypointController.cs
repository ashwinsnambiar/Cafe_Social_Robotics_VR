using UnityEngine;

public class OperatorCleanupWaypointController : MonoBehaviour
{
    [Header("Robot")]
    public Transform robotTransform;
    public RobotKeyboardOperator keyboardOperator;

    [Header("Cleanup")]
    public RobotCleanupSequence cleanupSequence;

    [Header("Operator Waypoint")]
    public Transform cleanupOperatorWaypoint;
    public GameObject cleanupWaypointMarker;

    [Header("Trigger Settings")]
    public float triggerDistance = 0.6f;
    public float resetDistance = 1.2f;

    private bool waitingForOperator = false;
    private bool cleanupStarted = false;
    private Transform currentSpillTarget;

    private void Awake()
    {
        if (cleanupWaypointMarker != null)
            cleanupWaypointMarker.SetActive(false);
    }

    private void Update()
    {
        if (!waitingForOperator || cleanupStarted)
            return;

        if (robotTransform == null || cleanupOperatorWaypoint == null || cleanupSequence == null)
            return;

        Vector3 robotPos = robotTransform.position;
        Vector3 waypointPos = cleanupOperatorWaypoint.position;

        robotPos.y = 0f;
        waypointPos.y = 0f;

        float distance = Vector3.Distance(robotPos, waypointPos);

        if (distance <= triggerDistance)
        {
            StartCleanupFromWaypoint();
        }
    }

    public void OnCrashOccurred(Transform spillTransform)
    {
        if (spillTransform == null)
        {
            Debug.LogWarning("OperatorCleanupWaypointController: crash occurred but spill transform is null.");
            return;
        }

        // Important: the original spill object may be destroyed after impact.
        // So we create a stable target transform that survives long enough for cleanup.
        if (currentSpillTarget != null)
            Destroy(currentSpillTarget.gameObject);

        GameObject stableTarget = new GameObject("OperatorCleanupSpillTarget");
        stableTarget.transform.position = spillTransform.position;
        stableTarget.transform.rotation = spillTransform.rotation;

        currentSpillTarget = stableTarget.transform;

        waitingForOperator = true;
        cleanupStarted = false;

        if (cleanupWaypointMarker != null)
            cleanupWaypointMarker.SetActive(true);

        Debug.Log("Cleanup waypoint active. Operator should drive robot into the marked area.");
    }

    private void StartCleanupFromWaypoint()
    {
        if (cleanupSequence == null || currentSpillTarget == null)
            return;

        waitingForOperator = false;
        cleanupStarted = true;

        if (cleanupWaypointMarker != null)
            cleanupWaypointMarker.SetActive(false);

        if (keyboardOperator != null)
            keyboardOperator.SetInputEnabled(false);

        cleanupSequence.OnCleanupComplete -= HandleCleanupComplete;
        cleanupSequence.OnCleanupComplete += HandleCleanupComplete;

        Debug.Log("Operator reached cleanup waypoint. Starting cleanup sequence.");

        cleanupSequence.StartCleanupSequence(currentSpillTarget);
    }

    private void HandleCleanupComplete()
    {
        cleanupSequence.OnCleanupComplete -= HandleCleanupComplete;

        cleanupStarted = false;
        waitingForOperator = false;

        if (keyboardOperator != null)
            keyboardOperator.SetInputEnabled(true);

        if (currentSpillTarget != null)
        {
            Destroy(currentSpillTarget.gameObject);
            currentSpillTarget = null;
        }

        Debug.Log("Cleanup complete. Operator control re-enabled.");
    }
}