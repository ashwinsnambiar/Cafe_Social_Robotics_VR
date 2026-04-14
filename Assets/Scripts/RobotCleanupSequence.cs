using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class RobotCleanupSequence : MonoBehaviour
{
    [Header("Core References")]
    public NavMeshAgent agent;
    public RobotNavigator navigator;
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
    private bool mainLogicWasDisabled = false;

    void Start()
    {
        if (cleanupApprovalUI != null) cleanupApprovalUI.SetActive(false);
        if (autoSubscribeToDistraction)
        {
            if (distractionEventReference == null)
                distractionEventReference = FindObjectOfType<DistractionEvent>();

            if (distractionEventReference != null)
                distractionEventReference.onCrashOccurred.AddListener(OnHeardCrash);
        }

        if (navigator == null)
            navigator = GetComponent<RobotNavigator>();
    }

    void OnDestroy()
    {
        if (distractionEventReference != null)
        {
            distractionEventReference.onCrashOccurred.RemoveListener(OnHeardCrash);
        }
    }

    // Called by the UnityEvent in DistractionEvent.cs
    public void OnHeardCrash(Transform spillLocation)
    {
        currentSpillLocation = spillLocation;
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
    }

    private IEnumerator CleanupRoutine()
    {
        // 1. Suspend normal operations
        mainLogicWasDisabled = false;
        if (mainRobotLogic != null)
        {
            mainRobotLogic.enabled = false;
            mainLogicWasDisabled = true;
        }

        // 2. Go to Operator
        if (operatorStandpoint != null)
            yield return StartCoroutine(MoveToLocation(operatorStandpoint.position, operatorStandpoint));
        else
        {
            Debug.LogWarning("RobotCleanupSequence: operatorStandpoint not assigned.");
        }

        // 3. Prompt VR User
        if (cleanupApprovalUI != null)
            cleanupApprovalUI.SetActive(true);
        isApproved = false;
        yield return new WaitUntil(() => isApproved == true);

        // 4. Fetch Tools
        if (broomStorageLocation != null)
            yield return StartCoroutine(MoveToLocation(broomStorageLocation.position, broomStorageLocation));
        else
            Debug.LogWarning("RobotCleanupSequence: broomStorageLocation not assigned.");
        
        // Snap tools to gripper
        if (broomAndPanPrefab != null && gripperSocket != null)
        {
            broomAndPanPrefab.transform.SetParent(gripperSocket, worldPositionStays: false);
            broomAndPanPrefab.transform.localPosition = Vector3.zero;
            broomAndPanPrefab.transform.localRotation = Quaternion.identity;
        }

        // 5. Go to Spill
        if (currentSpillLocation != null)
            yield return StartCoroutine(MoveToLocation(currentSpillLocation.position, currentSpillLocation));
        else
        {
            Debug.LogWarning("RobotCleanupSequence: currentSpillLocation is null - aborting cleanup.");
            // Resume operations if possible
            if (mainRobotLogic != null && mainLogicWasDisabled)
            {
                mainRobotLogic.enabled = true;
            }
            yield break;
        }

        // 6. Perform procedural cleaning motion & collect fragments
        yield return StartCoroutine(SweepAndCollect());

        // 7. Resume normal operations (or go to a trash can state)
        if (mainRobotLogic != null && mainLogicWasDisabled)
            mainRobotLogic.enabled = true;
    }

    private IEnumerator MoveToLocation(Vector3 destination)
    {
        // Prefer Navigator if present
        if (navigator != null)
        {
            var move = StartCoroutine(navigator.MoveToAsync(destination));
            // wait until navigator completes
            yield return move;
            yield break;
        }

        // fallback to using local NavMeshAgent
        if (agent == null)
        {
            Debug.LogWarning("RobotCleanupSequence: NavMeshAgent not assigned; cannot move to destination.");
            yield break;
        }

        agent.SetDestination(destination);
        yield return new WaitUntil(() => agent.pathPending == false);
        while (agent.remainingDistance > agent.stoppingDistance)
            yield return null;
        yield return new WaitForSeconds(0.1f);
    }

    // Overload that accepts a target transform so the robot will rotate to that transform when it arrives
    private IEnumerator MoveToLocation(Vector3 destination, Transform targetTransform)
    {
        // If navigator exists, ask it to move with look target
        if (navigator != null)
        {
            yield return StartCoroutine(navigator.MoveToAsync(destination, targetTransform));
            yield break;
        }

        // Fallback: do the move and then rotate to match the targetTransform
        yield return StartCoroutine(MoveToLocation(destination));
        if (targetTransform != null && agent != null)
        {
            var timeout = 1.0f;
            while (timeout > 0f && Quaternion.Angle(transform.rotation, targetTransform.rotation) > 0.5f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetTransform.rotation, agent.angularSpeed * Time.deltaTime);
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
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
