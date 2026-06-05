using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DeliveryRobot : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform barPoint;
    public Transform[] tables; // Will be auto-populated

    [Header("UI Elements")]
    public GameObject tableSelectionCanvas; // Drag the popup Canvas here

    public delegate void TraySecuredAction();
    public event TraySecuredAction OnTraySecuredEvent;

    public delegate void TableSelectedAction(int tableIndex);
    public event TableSelectedAction OnTableSelectedEvent;

    public event System.Action OnDeliveryFinished;
    public bool isActivelyDelivering { get; private set; } = false;
    private bool deliveryEnabled = false;
    private Coroutine deliveryRoutine;

    [Header("Controllers")]
    [SerializeField] private RobotArmController armController;
    [SerializeField] private RobotBodyController bodyController;
    [SerializeField] private GripperControl leftGripperControl;
    [SerializeField] private GripperControl rightGripperControl;

    [Header("Socket Interactors")]
    [SerializeField] private XRSocketInteractor leftSocketInteractor;
    [SerializeField] private XRSocketInteractor rightSocketInteractor;

    [Header("Arm Poses")]
    [SerializeField] private float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };
    [SerializeField] private float[] placePose = { 20f, -90f, 0f, 85f, 0f, -20f, 0f };

    [Header("Placement Settings")]
    [SerializeField] private float placeDelay = 1f; // Delay before opening gripper to place tray
    [SerializeField] private float bodyForwardPitch = -15f; // Forward bend angle (negative = forward)

    private NavMeshAgent agent;
    private Transform currentTarget;
    public RobotNavigator navigator;
    private int? currentTableIndex;
    private bool isAtDestination = false;

    // Internal flags to handle UI events in the coroutine
    private bool traySecuredThisCycle = false;
    private bool tableSelectedThisCycle = false;
    private int selectedTableThisCycle = -1;

    void Start()
    {
        // Prefer a shared Navigator if present
        if (navigator == null) navigator = GetComponent<RobotNavigator>();

        if (navigator != null)
        {
            agent = navigator.agent;
            // Navigator controls rotation
            if (agent != null) agent.updateRotation = false;
        }
        else
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.updateRotation = false;
        }

        // Auto-populate tables array with all GameObjects named WaypointTable<number>
        var allTables = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("WaypointTable"))
            .Select(t => new
            {
                Transform = t,
                Index = ParseTableIndex(t.name)
            })
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .Select(x => x.Transform)
            .ToArray();
        tables = allTables;

        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);

    }

    // Task Scheduler Interface Methods
    public void EnableDelivery()
    {
        if (!deliveryEnabled)
        {
            deliveryEnabled = true;
            deliveryRoutine = StartCoroutine(DeliveryRoutine());
        }
    }

    public void StopDeliverySafely()
    {
        deliveryEnabled = false;

        // If the robot is just idling at the bar, stop immediately.
        // If it is actively delivering, it will naturally end when the cycle finishes.
        if (!isActivelyDelivering)
        {
            if (deliveryRoutine != null)
            {
                StopCoroutine(deliveryRoutine);
                deliveryRoutine = null;
            }
            // Notify controller we have cleanly stopped
            OnDeliveryFinished?.Invoke();
        }
    }

    private int ParseTableIndex(string name)
    {
        var match = Regex.Match(name, @"WaypointTable(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int idx))
            return idx;
        return -1;
    }

    private IEnumerator DeliveryRoutine()
    {
        while (deliveryEnabled)
        {
            isActivelyDelivering = false;
            traySecuredThisCycle = false;
            tableSelectedThisCycle = false;

            // Initialize arms and body to rest pose
            bool armsReady = false;
            bool bodyReady = false;

            float[] restPose = { 0f, -90f, 0f, 85f, 0f, 0f, 0f };
            armController.MoveBothArms(restPose, restPose, () => armsReady = true);
            bodyController.MoveBodyAndHead(0.55f, 0f, 0f, () => bodyReady = true);

            yield return new WaitUntil(() => armsReady && bodyReady);

            GoToBar();

            // Wait until robot reaches bar
            yield return new WaitUntil(() => agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

            // Enable socket interactors so it can pick up a tray
            EnableSocketInteractors();

            // Wait until a tray is secured OR delivery is cancelled
            void OnSecured() => traySecuredThisCycle = true;
            OnTraySecuredEvent += OnSecured;

            yield return new WaitUntil(() => traySecuredThisCycle || !deliveryEnabled);
            OnTraySecuredEvent -= OnSecured;

            // If a spill happened while we were waiting at the bar, break out gracefully
            if (!deliveryEnabled) break;

            // We have a tray. We must finish this delivery regardless of spills.
            isActivelyDelivering = true;

            void OnTableSelected(int idx)
            {
                tableSelectedThisCycle = true;
                selectedTableThisCycle = idx;
            }
            OnTableSelectedEvent += OnTableSelected;

            yield return new WaitUntil(() => tableSelectedThisCycle);
            OnTableSelectedEvent -= OnTableSelected;

            yield return StartCoroutine(PrepareAndGoToTable(selectedTableThisCycle));
            yield return StartCoroutine(PlaceTraysAndReturnToBar());

            // Delivery cycle complete
            isActivelyDelivering = false;

            // Notify the Controller that we finished a run so it can check for pending cleanup tasks
            OnDeliveryFinished?.Invoke();
        }
    }

    void Update()
    {
        if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && !isAtDestination)
        {
            isAtDestination = true;
        }

        if (agent != null)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
            // Rotate towards target rotation
                if (currentTarget != null)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, currentTarget.rotation, agent.angularSpeed * Time.deltaTime);
            }
            else
            {
            // While moving, rotate towards the agent's desired velocity direction (more stable than actual velocity)
                var desired = agent.desiredVelocity;
                var horiz = new Vector3(desired.x, 0f, desired.z);
                if (horiz.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(horiz.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, agent.angularSpeed * Time.deltaTime);
                }
            }
        }
    }

    // Called by GripperSocketController once the tray is secured
    public void OnTraySecured()
    {
        OnTraySecuredEvent?.Invoke();

        if (tableSelectionCanvas != null)
        {
            tableSelectionCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Table Selection Canvas is missing! Defaulting to Table 0.");
            SelectTable(0);
        }
    }

    // Optional: If the tray is removed before selecting a table
    public void OnTrayRemoved()
    {
        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);
    }

    // Called by UI Buttons (On Click events)
    public void SelectTable(int tableIndex)
    {
        if (tableIndex < 0 || tableIndex >= tables.Length)
        {
            Debug.LogError("Invalid Table Index!");
            return;
        }

        OnTableSelectedEvent?.Invoke(tableIndex);

        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);
    }

    private IEnumerator PrepareAndGoToTable(int tableIndex)
    {
        bool armsReady = false;
        armController.MoveBothArms(carryPose, carryPose, () => armsReady = true);
        yield return new WaitUntil(() => armsReady);

        GoToTable(tableIndex);

        // Wait until we actually reach the table
        yield return new WaitUntil(() => agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
    }

    private void GoToTable(int tableIndex)
    {
        currentTableIndex = tableIndex;
        isAtDestination = false;
        currentTarget = tables[tableIndex];
        if (navigator != null)
            navigator.MoveTo(tables[tableIndex].position, tables[tableIndex]);
        else if (agent != null)
            agent.SetDestination(tables[tableIndex].position);
    }

    // Coroutine to place tray at table and return to bar
    private IEnumerator PlaceTraysAndReturnToBar()
    {
        // Wait a brief moment before placing
        yield return new WaitForSeconds(placeDelay);

        // Move arms to place pose and body forward
        bool armsReady = false;
        bool bodyReady = false;
        armController.MoveBothArms(placePose, placePose, () => armsReady = true);
        bodyController.MoveBodyAndHead(0.55f, bodyForwardPitch, 0f, () => bodyReady = true);
        yield return new WaitUntil(() => armsReady && bodyReady);

        // Open both grippers to release tray
        if (leftGripperControl != null)
        {
            leftGripperControl.OpenGripper();
        }
        if (rightGripperControl != null)
        {
            rightGripperControl.OpenGripper();
        }

        // Wait a moment for tray to settle on table before detaching
        yield return new WaitForSeconds(placeDelay);

        // Detach tray from socket interactors and disable socket interactors to prevent re-grabbing during placement
        DisableSocketInteractors();
        DetachTrayFromSockets();

        // Retract arms back to carry pose to ensure tray is fully separated
        bool armsReady2 = false;
        armController.MoveBothArms(carryPose, carryPose, () => armsReady2 = true);
        yield return new WaitUntil(() => armsReady2);

        // Move body back to rest pose
        bool bodyReady2 = false;
        float[] restPose = { 0f, -90f, 0f, 85f, 0f, 0f, 0f };
        bodyController.MoveBodyAndHead(0.55f, 0f, 0f, () => bodyReady2 = true);
        yield return new WaitUntil(() => bodyReady2);

        // Reset state and return to bar
        currentTableIndex = null;
        GoToBar();

        // Wait for robot to return to bar before re-enabling sockets
        yield return new WaitUntil(() => agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Re-enable socket interactors once safely back at bar
        EnableSocketInteractors();
    }

    private void DisableSocketInteractors()
    {
        if (leftSocketInteractor != null)
        {
            leftSocketInteractor.enabled = false;
            Debug.Log("Left socket interactor disabled.");
        }

        if (rightSocketInteractor != null)
        {
            rightSocketInteractor.enabled = false;
            Debug.Log("Right socket interactor disabled.");
        }
    }

    private void EnableSocketInteractors()
    {
        if (leftSocketInteractor != null)
        {
            leftSocketInteractor.enabled = true;
            Debug.Log("Left socket interactor re-enabled.");
        }

        if (rightSocketInteractor != null)
        {
            rightSocketInteractor.enabled = true;
            Debug.Log("Right socket interactor re-enabled.");
        }
    }

    private void DetachTrayFromSockets()
    {
        // Detach from left socket if it has selected interactables
        if (leftSocketInteractor != null && leftSocketInteractor.interactablesSelected.Count > 0)
        {
            var selectedInteractable = leftSocketInteractor.interactablesSelected[0];
            if (selectedInteractable != null)
            {
                leftSocketInteractor.interactionManager.SelectExit(leftSocketInteractor, selectedInteractable);
                Debug.Log("Detached tray from left socket.");
            }
        }

        // Detach from right socket if it has selected interactables
        if (rightSocketInteractor != null && rightSocketInteractor.interactablesSelected.Count > 0)
        {
            var selectedInteractable = rightSocketInteractor.interactablesSelected[0];
            if (selectedInteractable != null)
            {
                rightSocketInteractor.interactionManager.SelectExit(rightSocketInteractor, selectedInteractable);
                Debug.Log("Detached tray from right socket.");
            }
        }
    }

    public void GoToBar()
    {
        currentTableIndex = null;
        isAtDestination = false;
        currentTarget = barPoint;
        if (navigator != null)
            navigator.MoveTo(barPoint.position, barPoint);
        else if (agent != null)
            agent.SetDestination(barPoint.position);
    }
}
