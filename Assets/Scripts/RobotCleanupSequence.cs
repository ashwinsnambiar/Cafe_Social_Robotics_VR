using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class RobotCleanupSequence : MonoBehaviour
{
    [Header("Core References")]
    public NavMeshAgent agent;
    // Main robot logic that should be suspended during cleanup
    public MonoBehaviour mainRobotLogic;
    
    [Header("Auto Subscription")]
    [Tooltip("If true the robot will find a DistractionEvent in the scene and subscribe to its onCrashOccurred event at Start().")]
    public bool autoSubscribeToDistraction = true;
    public DistractionEvent distractionEventReference;

    [Header("Locations")]
    public Transform operatorStandpoint;
    public Transform broomStorageLocation;
    private Transform currentSpillLocation;

    [Header("UI Prompt")]
    public GameObject cleanupApprovalUI;

    [Header("Cleaning Tools")]
    public GameObject broomAndPanPrefab;
    public Transform gripperSocket;
    public Transform dustpanCatchArea;

    private bool isApproved = false;

    void Start()
    {
        if (cleanupApprovalUI != null) cleanupApprovalUI.SetActive(false);
        Debug.Log("RobotCleanupSequence: Initialized. Waiting for crash events.");

        if (autoSubscribeToDistraction)
        {
            if (distractionEventReference == null)
            {
                distractionEventReference = FindObjectOfType<DistractionEvent>();
            }

            if (distractionEventReference != null)
            {
                distractionEventReference.onCrashOccurred.AddListener(OnHeardCrash);
                Debug.Log($"RobotCleanupSequence: Auto-subscribed to DistractionEvent on '{distractionEventReference.gameObject.name}'.");
            }
            else
            {
                Debug.LogWarning("RobotCleanupSequence: No DistractionEvent found in scene to subscribe to.");
            }
        }
    }

    void OnDestroy()
    {
        if (distractionEventReference != null)
        {
            distractionEventReference.onCrashOccurred.RemoveListener(OnHeardCrash);
            Debug.Log("RobotCleanupSequence: Unsubscribed from DistractionEvent.");
        }
    }

    // Called by the UnityEvent in DistractionEvent.cs
    public void OnHeardCrash(Transform spillLocation)
    {
        currentSpillLocation = spillLocation;
        Debug.Log($"RobotCleanupSequence: Heard crash at {spillLocation?.position}. Starting cleanup routine.");
        if (spillLocation == null)
        {
            Debug.LogWarning("RobotCleanupSequence: Received null spillLocation in OnHeardCrash.");
            return;
        }
        StartCoroutine(CleanupRoutine());
    }

    // Called by the "Yes" Button on the VR Canvas
    public void UI_ConfirmCleanup()
    {
        isApproved = true;
        if (cleanupApprovalUI != null) cleanupApprovalUI.SetActive(false);
        Debug.Log("RobotCleanupSequence: Cleanup approved by operator via UI.");
    }

    private IEnumerator CleanupRoutine()
    {
        // 1. Suspend normal operations
        if (mainRobotLogic != null) mainRobotLogic.enabled = false;
        else
        {
            Debug.LogWarning("RobotCleanupSequence: mainRobotLogic not assigned; nothing to suspend.");
        }
        Debug.Log("RobotCleanupSequence: Suspended main robot logic and beginning cleanup sequence.");

        // 2. Go to Operator
        if (operatorStandpoint != null)
        {
            Debug.Log("RobotCleanupSequence: Moving to operator standpoint.");
            yield return StartCoroutine(MoveToLocation(operatorStandpoint.position));
            Debug.Log("RobotCleanupSequence: Arrived at operator standpoint.");
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: operatorStandpoint not assigned.");
        }

        // 3. Prompt VR User
        if (cleanupApprovalUI != null)
        {
            cleanupApprovalUI.SetActive(true);
            Debug.Log("RobotCleanupSequence: Displayed cleanup approval UI to operator.");
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: cleanupApprovalUI not assigned - cannot prompt operator.");
        }
        isApproved = false;
        yield return new WaitUntil(() => isApproved == true);

        // 4. Fetch Tools
        if (broomStorageLocation != null)
        {
            Debug.Log("RobotCleanupSequence: Moving to broom storage to pick up tools.");
            yield return StartCoroutine(MoveToLocation(broomStorageLocation.position));
            Debug.Log("RobotCleanupSequence: Arrived at broom storage.");
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: broomStorageLocation not assigned.");
        }
        
        // Snap tools to gripper
        if (broomAndPanPrefab != null && gripperSocket != null)
        {
            broomAndPanPrefab.transform.SetParent(gripperSocket, worldPositionStays: false);
            broomAndPanPrefab.transform.localPosition = Vector3.zero;
            broomAndPanPrefab.transform.localRotation = Quaternion.identity;
            Debug.Log("RobotCleanupSequence: Picked up broom and pan and attached to gripper.");
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: broomAndPanPrefab or gripperSocket not assigned; cannot pick up tools.");
        }

        // 5. Go to Spill
        if (currentSpillLocation != null)
        {
            Debug.Log($"RobotCleanupSequence: Moving to spill at {currentSpillLocation.position}.");
            yield return StartCoroutine(MoveToLocation(currentSpillLocation.position));
            Debug.Log("RobotCleanupSequence: Arrived at spill location.");
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: currentSpillLocation is null - aborting cleanup.");
            // Resume operations if possible
            if (mainRobotLogic != null) mainRobotLogic.enabled = true;
            yield break;
        }

        // 6. Perform procedural cleaning motion & collect fragments
        Debug.Log("RobotCleanupSequence: Beginning sweep and collect routine.");
        yield return StartCoroutine(SweepAndCollect());
        Debug.Log("RobotCleanupSequence: Finished sweep and collect routine.");

        // 7. Resume normal operations (or go to a trash can state)
        if (mainRobotLogic != null)
        {
            mainRobotLogic.enabled = true;
            Debug.Log("RobotCleanupSequence: Resumed main robot logic.");
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: mainRobotLogic not assigned when trying to resume.");
        }
    }

    private IEnumerator MoveToLocation(Vector3 destination)
    {
        if (agent == null)
        {
            Debug.LogWarning("RobotCleanupSequence: NavMeshAgent not assigned; cannot move to destination.");
            yield break;
        }
        Debug.Log($"RobotCleanupSequence: Agent setting destination to {destination}.");
        agent.SetDestination(destination);
        // Wait until path is calculated and agent is moving
        yield return new WaitUntil(() => agent.pathPending == false);
        Debug.Log("RobotCleanupSequence: Agent path pending false, waiting for arrival.");
        // Wait until agent reaches destination
        yield return new WaitUntil(() => agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("RobotCleanupSequence: Agent reached destination.");
        // Brief pause to settle
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator SweepAndCollect()
    {
        // Procedural Animation: Bend forward
        Quaternion startRot = transform.rotation;
        Quaternion bendRot = startRot * Quaternion.Euler(30f, 0, 0); // Pitch forward 30 degrees
        
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, bendRot, t);
            yield return null;
        }

        // Simulate brushing motion (simple delay for now, can add arm rotation here)
        yield return new WaitForSeconds(1f);

        // Collect fragments: Find rigidbodies in a sphere around the spill
        int collectedCount = 0;
        if (currentSpillLocation != null)
        {
            Collider[] hits = Physics.OverlapSphere(currentSpillLocation.position, 1.5f);
            Debug.Log($"RobotCleanupSequence: Found {hits.Length} colliders near spill.");
            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && col.gameObject != this.gameObject) // Don't suck up the robot itself
                {
                    // Disable physics on the fragments
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    
                    // Teleport and parent them to the dustpan
                    if (dustpanCatchArea != null)
                    {
                        col.transform.SetParent(dustpanCatchArea, worldPositionStays: false);
                        // Add a little randomness to the pile
                        Vector3 randomOffset = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0f, 0.1f), Random.Range(-0.1f, 0.1f));
                        col.transform.localPosition = randomOffset;
                        collectedCount++;
                        Debug.Log($"RobotCleanupSequence: Collected fragment '{col.gameObject.name}' into dustpan.");
                    }
                    else
                    {
                        Debug.LogWarning($"RobotCleanupSequence: dustpanCatchArea not assigned - cannot parent '{col.gameObject.name}'.");
                    }
                }
            }
        }
        Debug.Log($"RobotCleanupSequence: Collected {collectedCount} fragments into the dustpan.");

        yield return new WaitForSeconds(1f);

        // Stand back up
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(bendRot, startRot, t);
            yield return null;
        }
    }
}
