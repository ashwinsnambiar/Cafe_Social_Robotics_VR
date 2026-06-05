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

    [Header("UI Prompt")]
    public GameObject cleanupApprovalUI;

    [Header("Cleaning Tools")]
    public GameObject broomPrefab;
    public GameObject dustpanPrefab;
    public Transform rightGripperSocket; // For the Broom
    public Transform leftGripperSocket;  // For the Dustpan
    public GameObject fakeGlassPile;

    [Header("Articulation Controllers")]
    public RobotBodyController robotBodyController;
    public RobotArmController robotArmController;

    private bool isApproved = false;

    private float defaultTorsoHeight = 0.55f;
    private float crouchTorsoHeight = 0.0f;

    void Start()
    {
        if (cleanupApprovalUI != null) cleanupApprovalUI.SetActive(false);

        if (navigator == null)
            navigator = GetComponent<RobotNavigator>();

    }

    // Called by RobotTaskScheduler
    public void StartCleanupSequence(Transform spillLocation)
    {
        if (spillLocation == null) return;

        Vector3 forwardDir = spillLocation.forward;
        forwardDir.y = 0;
        if (forwardDir == Vector3.zero) forwardDir = Vector3.forward; // Fallback
        forwardDir.Normalize();

        Vector3 behindDir = -forwardDir;
        Vector3 approachDirection = Quaternion.Euler(0, angleOffsetFragment, 0) * behindDir;

        currentSpillPosition = spillLocation.position + (approachDirection * distanceOffsetFragment);
        currentSpillPosition.y = spillLocation.position.y;

        StartCoroutine(FindFragmentsAndCleanup());
    }

    private IEnumerator FindFragmentsAndCleanup()
    {
        yield return new WaitForSeconds(0.5f);

        GameObject fragmentsObj = GameObject.Find("BottleFragments(Clone)");

        Vector3 thingToLookAt = new Vector3();

        if (fragmentsObj != null)
        {
            Debug.Log("RobotCleanupSequence: Found fragments, using their position for rotation.");
            fragmentsTransform = fragmentsObj.transform;
            thingToLookAt = fragmentsTransform.position;
        }
        else
        {
            Debug.LogWarning("RobotCleanupSequence: No fragments found.");
        }

        thingToLookAt.y = currentSpillPosition.y;

        // Vector pointing from the robot's standing spot to the mess
        Vector3 lookDirection = thingToLookAt - currentSpillPosition;
        lookDirection.y = 0; // Double-checking it's flat

        Quaternion finalRotation = Quaternion.LookRotation(lookDirection);

        // Create a temporary object for the NavMeshAgent to align to
        GameObject tempSpillTarget = new GameObject("TempSpillTarget");
        tempSpillTarget.transform.position = currentSpillPosition;
        tempSpillTarget.transform.rotation = finalRotation;

        StartCoroutine(CleanupRoutine(tempSpillTarget));
    }

    // Called by the "Yes" Button on the VR Canvas
    public void UI_ConfirmCleanup()
    {
        isApproved = true;
        if (cleanupApprovalUI != null) cleanupApprovalUI.SetActive(false);
    }

    private IEnumerator CleanupRoutine(GameObject tempSpillTarget)
    {

        // 1. Go to Operator
        if (operatorStandpoint != null)
        {
            Vector3 flatForward = operatorStandpoint.forward;
            flatForward.y = 0;
            flatForward.Normalize();

            Vector3 targetPosition = operatorStandpoint.position + (flatForward * 1.0f);
            Vector3 lookDirection = operatorStandpoint.position - targetPosition;
            lookDirection.y = 0; // Keep the robot perfectly upright (by projecting onto the horizontal plane)

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
        if (currentSpillPosition != null)
            yield return StartCoroutine(MoveToLocation(currentSpillPosition, tempSpillTarget.transform));

        Destroy(tempSpillTarget);

        // 5. Sweep
        yield return StartCoroutine(SweepAndCollect());

        // 6. Return tools
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
            broomPrefab.transform.SetParent(null, true);

        if (dustpanPrefab != null)
            dustpanPrefab.transform.SetParent(null, true);
    }

    private IEnumerator SweepAndCollect()
    {
        Debug.Log("Robot: Beginning cleanup sequence...");

        float[] dustpanRestingPose = { 0f, -90f, 0f, 80f, 20f, -20f, 0f };
        float[] sweepStartPose = { 25f, -90f, 0f, 80f, -20f, 0f, 0f };
        float[] sweepEndPose = { -10f, -80f, 0f, 100f, -10f, 0f, 0f };
        float[] sweepBodyPose = { crouchTorsoHeight, 45f, 0f };

        Debug.Log("Robot: Crouching to spill...");
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

        Debug.Log("Robot: Cleanup sequence complete.");
    }
}
