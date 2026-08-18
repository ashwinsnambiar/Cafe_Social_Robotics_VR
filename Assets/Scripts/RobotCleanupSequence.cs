using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RobotCleanupSequence : MonoBehaviour
{
    [Header("Core References")]
    public NavMeshAgent agent;
    public RobotNavigator navigator;


    public event System.Action OnCleanupComplete;

    [Header("Locations")]
    public Transform operatorStandpoint;
    public Transform broomStorageLocation;
    private Vector3 currentSpillPosition;
    private Transform fragmentsTransform;
    public float distanceOffsetFragment = 0.5f;
    public float angleOffsetFragment = 0f;

    [Header("Spill Arrival Orientation")]
    [Tooltip("If true, rotates the robot to face the spill upon arrival. If false, the robot stays facing the direction in which it travelled.")]
    public bool rotateToFaceSpill = false;

    [Tooltip("Additional Y-axis rotation offset (in degrees) when facing the spill. E.g. set 180 to face 180 degrees opposite.")]
    public float spillFacingAngleOffset = 0f;

    [Header("UI Prompt")]
    public GameObject cleanupApprovalUI;

    [Header("Cleaning Tools")]
    public GameObject broomPrefab;
    public GameObject dustpanPrefab;
    public Transform rightGripperSocket; // For the Broom
    public Transform leftGripperSocket;  // For the Dustpan
    public GameObject fakeGlassPile;

    private Vector3 initialBroomPos;
    private Quaternion initialBroomRot;
    private Transform initialBroomParent;

    private Vector3 initialDustpanPos;
    private Quaternion initialDustpanRot;
    private Transform initialDustpanParent;

    [Header("Articulation Controllers")]
    public RobotBodyController robotBodyController;
    public RobotArmController robotArmController;

    [Header("Coffee Spill — Cup Placement on Dustpan")]
    [Tooltip("Local position offset for the spilled cup when placed on the dustpan.")]
    public Vector3 spilledCupDustpanOffset = Vector3.zero;

    [Tooltip("Local rotation (Euler angles) for the spilled cup when placed on the dustpan.")]
    public Vector3 spilledCupDustpanRotation = Vector3.zero;

    private bool isApproved = false;

    private float defaultTorsoHeight = 0.5f;
    private float crouchTorsoHeight = 0.0f;

    // Current cleanup task state
    private CleanupTask currentTask;
    private GameObject spilledCupInstance;
    private GameObject vrIndicatorInstance;

    void Start()
    {
        if (cleanupApprovalUI != null) cleanupApprovalUI.SetActive(false);

        if (navigator == null)
            navigator = GetComponent<RobotNavigator>();

        // Cache initial storage poses
        if (broomPrefab != null)
        {
            initialBroomPos = broomPrefab.transform.position;
            initialBroomRot = broomPrefab.transform.rotation;
            initialBroomParent = broomPrefab.transform.parent;
        }

        if (dustpanPrefab != null)
        {
            initialDustpanPos = dustpanPrefab.transform.position;
            initialDustpanRot = dustpanPrefab.transform.rotation;
            initialDustpanParent = dustpanPrefab.transform.parent;
        }
    }

    /// <summary>
    /// Entry point called by RobotTaskScheduler with a typed CleanupTask.
    /// </summary>
    public void StartCleanupSequence(CleanupTask task)
    {
        if (task == null) return;

        currentTask = task;

        // Calculate approach position from the event location
        Vector3 forwardDir = task.Rotation * Vector3.forward;
        forwardDir.y = 0;
        if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;
        forwardDir.Normalize();

        Vector3 behindDir = -forwardDir;
        Vector3 approachDirection = Quaternion.Euler(0, angleOffsetFragment, 0) * behindDir;

        currentSpillPosition = task.Position + (approachDirection * distanceOffsetFragment);
        currentSpillPosition.y = task.Position.y;

        StartCoroutine(PrepareAndCleanup());
    }

    /// <summary>
    /// Legacy overload — wraps the Transform into a BrokenBottle CleanupTask.
    /// </summary>
    public void StartCleanupSequence(Transform spillLocation)
    {
        if (spillLocation == null) return;

        var task = new CleanupTask
        {
            Type = CleanupType.BrokenBottle,
            Position = spillLocation.position,
            Rotation = spillLocation.rotation
        };
        StartCleanupSequence(task);
    }

    private IEnumerator PrepareAndCleanup()
    {
        yield return new WaitForSeconds(0.5f);

        if (currentTask.Type == CleanupType.BrokenBottle)
        {
            GameObject fragmentsObj = GameObject.Find("BottleFragments(Clone)");
            if (fragmentsObj != null)
            {
                Debug.Log("RobotCleanupSequence: Found fragments.");
                fragmentsTransform = fragmentsObj.transform;
            }
            else
            {
                Debug.LogWarning("RobotCleanupSequence: No fragments found.");
                fragmentsTransform = null;
            }
        }
        else // SpilledCoffee
        {
            spilledCupInstance = currentTask.SpilledCupInstance;
            vrIndicatorInstance = currentTask.VrIndicatorInstance;
            Debug.Log("RobotCleanupSequence: Coffee spill cleanup prepared.");
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

        // 1. Go to Operator
        if (operatorStandpoint != null)
        {
            Vector3 flatForward = operatorStandpoint.forward;
            flatForward.y = 0;
            flatForward.Normalize();

            Vector3 targetPosition = operatorStandpoint.position + (flatForward * 1.0f);
            Vector3 lookDirection = operatorStandpoint.position - targetPosition;
            lookDirection.y = 0;

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

            GameObject tempTarget = new GameObject("TempTarget");
            tempTarget.transform.position = targetPosition;
            tempTarget.transform.rotation = targetRotation;

            yield return StartCoroutine(MoveToLocation(targetPosition, tempTarget.transform));

            Destroy(tempTarget);
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: operatorStandpoint not assigned.");
        }

        // 2. Prompt VR User
        if (cleanupApprovalUI != null)
            cleanupApprovalUI.SetActive(true);
        isApproved = false;
        yield return new WaitUntil(() => isApproved == true);

        // 3. Fetch Tools
        if (broomStorageLocation != null)
            yield return StartCoroutine(PickUpToolsRoutine());

        // 4. Go to Spill
        if (rotateToFaceSpill && currentTask != null)
        {
            Vector3 lookDirection = currentTask.Position - currentSpillPosition;
            lookDirection.y = 0;
            if (lookDirection == Vector3.zero)
            {
                lookDirection = currentTask.Rotation * Vector3.forward;
                lookDirection.y = 0;
            }

            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0, spillFacingAngleOffset, 0);
                GameObject tempSpillTarget = new GameObject("TempSpillTarget");
                tempSpillTarget.transform.position = currentSpillPosition;
                tempSpillTarget.transform.rotation = targetRotation;

                yield return StartCoroutine(MoveToLocation(currentSpillPosition, tempSpillTarget.transform));
                Destroy(tempSpillTarget);
            }
            else
            {
                yield return StartCoroutine(MoveToLocation(currentSpillPosition));
            }
        }
        else
        {
            // By default, stay in the same direction in which the robot travelled (no final rotation)
            yield return StartCoroutine(MoveToLocation(currentSpillPosition));
        }

        // 5. Sweep and collect — dispatch by cleanup type
        if (currentTask.Type == CleanupType.BrokenBottle)
        {
            yield return StartCoroutine(SweepFloorAndCollect());
        }
        else
        {
            // Floor sweep first (cleans the puddle area), then grab cup off the table
            yield return StartCoroutine(SweepFloorForSpill());
            yield return StartCoroutine(GrabCupFromTable());
        }

        // 6. Return tools (also detaches spilled cup if attached)
        if (broomStorageLocation != null)
            yield return StartCoroutine(ReturnToolsRoutine());

        // 7. Notify the Master Controller
        OnCleanupComplete?.Invoke();
    }

    private IEnumerator MoveToLocation(Vector3 destination)
    {
        // Prefer Navigator if present
        if (navigator != null)
        {
            yield return StartCoroutine(navigator.MoveToAsync(destination));
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

    private IEnumerator MoveBodyArm(float[] bodyPose, float[] leftArmPose, float[] rightArmPose)
    {
        if (robotBodyController != null)
        {
            robotBodyController.MoveBodyAndHead(bodyPose[0], bodyPose[1], bodyPose[2]);
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: robotBodyController is not of type RobotBodyController - cannot perform body movement.");
        }
        if (robotArmController != null)
        {
            robotArmController.MoveBothArms(leftArmPose, rightArmPose);
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: robotArmController is not of type RobotArmController - cannot perform arm movement.");
        }

        yield return null;

        yield return new WaitUntil(() =>
             (robotBodyController == null || !robotBodyController.IsMoving) &&
             (robotArmController == null || !robotArmController.IsMoving)
         );
    }

    private IEnumerator PickUpToolsRoutine()
    {
        yield return StartCoroutine(MoveToLocation(broomStorageLocation.position, broomStorageLocation));

        float[] broomPose = { 10f, -90f, 0f, 85f, -10f, 0f, 0f };
        float[] dustpanPose = { 10f, -90f, 0f, 60f, 10f, 0f, 0f };

        float[] pickBodyPose = { crouchTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(pickBodyPose, dustpanPose, broomPose));

        SnapToolsToGrippers();

        float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };
        float[] restBodyPose = { defaultTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(restBodyPose, carryPose, carryPose));
    }

    private IEnumerator ReturnToolsRoutine()
    {
        yield return StartCoroutine(MoveToLocation(broomStorageLocation.position, broomStorageLocation));

        float[] broomPose = { 10f, -90f, 0f, 85f, -10f, 0f, 0f };
        float[] dustpanPose = { 10f, -90f, 0f, 60f, 10f, 0f, 0f };

        float[] pickBodyPose = { crouchTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(pickBodyPose, dustpanPose, broomPose));

        DetachToolsToGround();

        float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };
        float[] restBodyPose = { defaultTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(restBodyPose, carryPose, carryPose));
    }

    private void SnapToolsToGrippers()
    {
        if (broomPrefab != null && rightGripperSocket != null)
        {
            broomPrefab.transform.SetParent(rightGripperSocket, false);
            broomPrefab.transform.localPosition = Vector3.zero;
            broomPrefab.transform.localRotation = Quaternion.identity;
        }

        if (dustpanPrefab != null && leftGripperSocket != null)
        {
            dustpanPrefab.transform.SetParent(leftGripperSocket, false);
            dustpanPrefab.transform.localPosition = new Vector3(-0.033f, -0.04f, 0.085f);
            dustpanPrefab.transform.localRotation = Quaternion.Euler(70f, 0f, 325f);
        }
    }

    private void DetachToolsToGround()
    {
        if (broomPrefab != null)
        {
            broomPrefab.transform.SetParent(initialBroomParent, true);
            broomPrefab.transform.position = initialBroomPos;
            broomPrefab.transform.rotation = initialBroomRot;
        }

        if (dustpanPrefab != null)
        {
            dustpanPrefab.transform.SetParent(initialDustpanParent, true);
            dustpanPrefab.transform.position = initialDustpanPos;
            dustpanPrefab.transform.rotation = initialDustpanRot;
        }

        // Destroy the collected spilled cup
        if (spilledCupInstance != null)
        {
            Destroy(spilledCupInstance);
            spilledCupInstance = null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Shared sweep motion (used by both cleanup types)
    // ────────────────────────────────────────────────────────────────

    private IEnumerator PerformFloorSweepMotion()
    {
        float[] dustpanRestingPose = { 0f, -90f, 0f, 80f, 20f, -20f, 0f };
        float[] sweepStartPose = { 25f, -90f, 0f, 80f, -20f, 0f, 0f };
        float[] sweepEndPose = { -10f, -80f, 0f, 100f, -10f, 0f, 0f };
        float[] sweepBodyPose = { crouchTorsoHeight, 45f, 0f };

        Debug.Log("Robot: Crouching to sweep...");
        yield return StartCoroutine(MoveBodyArm(sweepBodyPose, dustpanRestingPose, sweepStartPose));

        int numberOfSweeps = 3;
        for (int i = 0; i < numberOfSweeps; i++)
        {
            Debug.Log($"Robot: Sweeping... ({i + 1}/{numberOfSweeps})");

            yield return StartCoroutine(MoveBodyArm(sweepBodyPose, dustpanRestingPose, sweepEndPose));
            yield return new WaitForSeconds(1.0f);

            yield return StartCoroutine(MoveBodyArm(sweepBodyPose, dustpanRestingPose, sweepStartPose));
            yield return new WaitForSeconds(1.0f);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  BROKEN BOTTLE — floor sweep + collect fragments
    // ────────────────────────────────────────────────────────────────

    private IEnumerator SweepFloorAndCollect()
    {
        Debug.Log("Robot: Beginning floor cleanup sequence (broken bottle)...");

        yield return StartCoroutine(PerformFloorSweepMotion());

        Debug.Log("Robot: Swapping real fragments for fake pile...");

        if (fragmentsTransform != null)
        {
            Destroy(fragmentsTransform.gameObject);
        }

        // Turn on the fake visual glass pile sitting in the dustpan
        if (fakeGlassPile != null)
        {
            fakeGlassPile.SetActive(true);
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: fakeGlassPile is not assigned in the Inspector!");
        }

        yield return new WaitForSeconds(1.0f);

        Debug.Log("Robot: Standing back up...");
        float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };
        float[] restBodyPose = { defaultTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(restBodyPose, carryPose, carryPose));

        Debug.Log("Robot: Floor cleanup sequence complete.");
    }

    // ────────────────────────────────────────────────────────────────
    //  SPILLED COFFEE — floor sweep (puddle area) + stand up
    // ────────────────────────────────────────────────────────────────

    private IEnumerator SweepFloorForSpill()
    {
        Debug.Log("Robot: Beginning floor sweep (coffee spill area)...");

        yield return StartCoroutine(PerformFloorSweepMotion());

        // Clean up the spill visual effects (particles + puddle decal)
        if (spilledCupInstance != null)
        {
            var controller = spilledCupInstance.GetComponent<SpilledCupController>();
            if (controller != null)
            {
                controller.Cleanup();
            }
        }

        yield return new WaitForSeconds(1.0f);

        Debug.Log("Robot: Standing back up after floor sweep...");
        float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };
        float[] restBodyPose = { defaultTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(restBodyPose, carryPose, carryPose));
    }

    // ────────────────────────────────────────────────────────────────
    //  SPILLED COFFEE — grab the cup from the table onto the dustpan
    // ────────────────────────────────────────────────────────────────

    private IEnumerator GrabCupFromTable()
    {
        Debug.Log("Robot: Reaching to grab spilled cup from table...");

        // Standing height, left arm holds dustpan steady, right arm reaches forward
        float[] grabBodyPose = { 0.4f, 0f, 0f };
        float[] dustpanHoldPose = { 0f, -90f, 0f, 85f, 0f, -20f, 0f };
        float[] reachPose = { 10f, -45f, 0f, 60f, -10f, 0f, 0f };

        // Reach toward the cup on the table
        yield return StartCoroutine(MoveBodyArm(grabBodyPose, dustpanHoldPose, reachPose));
        yield return new WaitForSeconds(0.5f);

        // Reparent the spilled cup onto the dustpan (left gripper socket)
        if (spilledCupInstance != null && leftGripperSocket != null)
        {
            spilledCupInstance.transform.SetParent(leftGripperSocket, false);
            spilledCupInstance.transform.localPosition = spilledCupDustpanOffset;
            spilledCupInstance.transform.localRotation = Quaternion.Euler(spilledCupDustpanRotation);
        }

        // Destroy the VR indicator (glowing red exclamation mark)
        if (vrIndicatorInstance != null)
        {
            Destroy(vrIndicatorInstance);
            vrIndicatorInstance = null;
        }

        yield return new WaitForSeconds(0.5f);

        // Return both arms to carry pose
        Debug.Log("Robot: Cup collected, returning to carry pose...");
        float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };
        float[] restBodyPose = { defaultTorsoHeight, 0f, 0f };
        yield return StartCoroutine(MoveBodyArm(restBodyPose, carryPose, carryPose));

        Debug.Log("Robot: Cup grab complete.");
    }
}
