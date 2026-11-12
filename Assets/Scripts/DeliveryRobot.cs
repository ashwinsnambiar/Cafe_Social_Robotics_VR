using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DeliveryRobot : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform barPoint;
    public Transform[] tables; // Auto-populated from WaypointTable<number>

    [Header("UI Elements")]
    public GameObject tableSelectionCanvas; // Optional old/manual fallback panel

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
    [SerializeField] private float[] placePose = { 40f, -90f, 0f, 65f, 0f, -20f, 0f };

    [Header("Placement Settings")]
    public float placeDelay = 1f;
    [SerializeField] private float bodyForwardPitch = 20f;

    [Header("Operator Control")]
    public RobotKeyboardOperator keyboardOperator;

    [Header("Operator Mode")]
    public bool operatorMode = true;

    private bool operatorPickupArmed = false;
    private bool operatorHasTray = false;
    private Coroutine operatorDeliveryRoutine;

    private NavMeshAgent agent;
    private Transform currentTarget;
    public RobotNavigator navigator;

    private int? currentTableIndex;
    private bool isAtDestination = false;

    // Internal flags for old automatic coroutine mode
    private bool traySecuredThisCycle = false;
    private bool tableSelectedThisCycle = false;
    private int selectedTableThisCycle = -1;

    [Header("Experiment Order Integration")]
    public bool useExperimentOrderTable = true;

    private int pendingExperimentTableIndex = -1;

    public void SetExperimentOrderTable(int tableIndex)
    {
        pendingExperimentTableIndex = tableIndex;
        Debug.Log($"DeliveryRobot: Received experiment target table index {tableIndex}.");

        //TryStartExperimentDelivery();
    }

    private void TryStartExperimentDelivery()
    {
        if (!operatorMode)
            return;

        if (operatorDeliveryRoutine != null)
        {
            Debug.Log("DeliveryRobot: Operator delivery is already running.");
            return;
        }

        if (!operatorHasTray)
        {
            Debug.Log("DeliveryRobot: Table index available, waiting for tray pickup.");
            return;
        }

        int tableIndex = pendingExperimentTableIndex;

        if (tableIndex < 0)
        {
            if (!TryGetCurrentExperimentTableIndex(out tableIndex))
            {
                Debug.LogWarning("DeliveryRobot: No experiment table index available yet.");
                return;
            }
        }

        pendingExperimentTableIndex = -1;
        SelectTable(tableIndex);
    }

    private bool TryGetCurrentExperimentTableIndex(out int tableIndex)
    {
        tableIndex = -1;

        if (OrderManager.Instance == null)
        {
            Debug.LogWarning("DeliveryRobot: OrderManager is missing. Cannot get experiment table index.");
            return false;
        }

        bool wasTicked;
        int deliveryNumber;
        int totalInSet;

        var activeOrder = OrderManager.Instance.GetCurrentDeliveryTargetOrder(
            out wasTicked,
            out deliveryNumber,
            out totalInSet
        );

        if (activeOrder == null)
        {
            Debug.LogWarning("DeliveryRobot: No active experiment order found.");
            return false;
        }

        tableIndex = activeOrder.targetTableIndex;
        Debug.Log($"DeliveryRobot: Auto-selected table {tableIndex} from active experiment order.");

        return true;
    }

    private void Start()
    {
        if (navigator == null)
            navigator = GetComponent<RobotNavigator>();

        if (navigator != null)
        {
            agent = navigator.agent;

            if (agent != null)
                agent.updateRotation = false;
        }
        else
        {
            agent = GetComponent<NavMeshAgent>();

            if (agent != null)
                agent.updateRotation = false;
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

        if (tableSelectionCanvas != null)
            tableSelectionCanvas.SetActive(false);
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
            currentTarget = null;

            if (deliveryRoutine != null)
            {
                StopCoroutine(deliveryRoutine);
                deliveryRoutine = null;
            }

            traySecuredThisCycle = false;
            tableSelectedThisCycle = false;
            selectedTableThisCycle = -1;

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

            // Do NOT reset tray/table flags here.
            // They might already have been set while this routine
            // was preparing after cleanup.

            // 1. Initialize arms and body
            bool armsReady = false;
            bool bodyReady = false;

            float[] restPose = { 0f, -90f, 0f, 85f, 0f, -20f, 0f };

            armController.MoveBothArms(
                restPose, restPose,
                () => armsReady = true
            );

            bodyController.MoveBodyAndHead(
                0.5f, 0f, 0f,
                () => bodyReady = true
            );

            yield return new WaitUntil(
                () => (armsReady && bodyReady) || !deliveryEnabled
            );

            if (!deliveryEnabled)
                break;

            // 2. Move to tray pickup position
            GoToBar();

            yield return new WaitUntil(() =>
                !deliveryEnabled ||
                (agent != null &&
                 !agent.pathPending &&
                 agent.remainingDistance <= agent.stoppingDistance)
            );

            if (!deliveryEnabled)
                break;

            EnableSocketInteractors();

            // 3. Wait for tray (real or simulated)
            Debug.Log("DeliveryRobot: Waiting for tray.");

            yield return new WaitUntil(
                () => traySecuredThisCycle || !deliveryEnabled
            );

            if (!deliveryEnabled)
                break;

            isActivelyDelivering = true;

            // 4. Wait for operator to select a table
            Debug.Log("DeliveryRobot: Waiting for table selection.");

            yield return new WaitUntil(
                () => tableSelectedThisCycle || !deliveryEnabled
            );

            // If a cleanup interrupted us while waiting,
            // notify the scheduler so it can start cleanup.
            if (!deliveryEnabled)
            {
                Debug.Log("DeliveryRobot: Delivery interrupted by cleanup.");

                isActivelyDelivering = false;
                currentTarget = null;

                traySecuredThisCycle = false;
                tableSelectedThisCycle = false;
                selectedTableThisCycle = -1;

                OnDeliveryFinished?.Invoke();
                break;
            }

            // 5. Deliver to selected table
            int targetTable = selectedTableThisCycle;

            Debug.Log($"DeliveryRobot: Going to table {targetTable + 1}.");

            yield return StartCoroutine(
                PrepareAndGoToTable(targetTable)
            );

            yield return StartCoroutine(
                PlaceTraysAndReturnToBar()
            );

            // 6. Reset state for next delivery
            isActivelyDelivering = false;
            currentTarget = null;

            traySecuredThisCycle = false;
            tableSelectedThisCycle = false;
            selectedTableThisCycle = -1;

            Debug.Log("DeliveryRobot: Delivery completed.");

            OnDeliveryFinished?.Invoke();

            if (OrderManager.Instance != null)
                OrderManager.Instance.CheckSetCompletion();
        }

        deliveryRoutine = null;
    }

    private void Update()
    {
        // In operator mode, deliveryEnabled may be false.
        // So only require deliveryEnabled for non-operator automatic delivery.
        if (!isActivelyDelivering)
            return;

        if (!operatorMode && !deliveryEnabled)
            return;

        if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && !isAtDestination)
        {
            isAtDestination = true;
        }

        if (agent != null)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (currentTarget != null)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        currentTarget.rotation,
                        agent.angularSpeed * Time.deltaTime
                    );
                }
            }
            else
            {
                var desired = agent.desiredVelocity;
                var horiz = new Vector3(desired.x, 0f, desired.z);

                if (horiz.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(horiz.normalized);

                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        agent.angularSpeed * Time.deltaTime
                    );
                }
            }
        }
    }

    // Called by GripperSocketController once the tray is secured
    public void OnTraySecured()
    {
        Debug.Log("DeliveryRobot: Tray secured. Waiting for operator table selection");

        operatorHasTray = true;
        operatorPickupArmed = false;

        // Remember this event even if the automatic coroutine
        // has not yet reached its tray-waiting step.
        traySecuredThisCycle = true;

        // Keep compatibility with the old automatic delivery coroutine.
        OnTraySecuredEvent?.Invoke();

        if (tableSelectionCanvas != null)
        {
            tableSelectionCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Delivery Robot: Table Selection Canvas not assigned!");
        }

       /* if (operatorMode && useExperimentOrderTable)
        {
            TryStartExperimentDelivery();
            return;
        }

        // Old manual table-selection fallback
        if (tableSelectionCanvas != null)
            tableSelectionCanvas.SetActive(true);

        */
    }

    private TrayController GetDockedTray()
    {
        if (leftSocketInteractor != null && leftSocketInteractor.hasSelection)
        {
            var tr = leftSocketInteractor.interactablesSelected[0].transform;
            var tray =
                tr.GetComponent<TrayController>() ??
                tr.GetComponentInChildren<TrayController>() ??
                tr.GetComponentInParent<TrayController>();

            if (tray != null)
                return tray;
        }

        if (rightSocketInteractor != null && rightSocketInteractor.hasSelection)
        {
            var tr = rightSocketInteractor.interactablesSelected[0].transform;
            var tray =
                tr.GetComponent<TrayController>() ??
                tr.GetComponentInChildren<TrayController>() ??
                tr.GetComponentInParent<TrayController>();

            if (tray != null)
                return tray;
        }

        return null;
    }

    // Optional: If the tray is removed before delivery starts
    public void OnTrayRemoved()
    {
        operatorHasTray = false;
        operatorPickupArmed = false;

        if (tableSelectionCanvas != null)
            tableSelectionCanvas.SetActive(false);
    }

    // Can still be called by old UI buttons, but now also used by experiment order flow.
    public void SelectTable(int tableIndex)
    {
        if (tables == null || tables.Length == 0)
        {
            Debug.LogError("DeliveryRobot: No table waypoints found. Check WaypointTable objects.");
            return;
        }

        if (tableIndex < 0 || tableIndex >= tables.Length)
        {
            Debug.LogError($"DeliveryRobot: Invalid Table Index {tableIndex}. Tables length is {tables.Length}.");
            return;
        }

        if (operatorMode && !operatorHasTray)
        {
            Debug.LogWarning("DeliveryRobot: Cannot start delivery. Robot does not have a tray yet.");
            return;
        }


        if (!operatorMode)
        {
            if (!deliveryEnabled || !traySecuredThisCycle)
            {
                Debug.LogWarning(
                    "DeliveryRobot: Automatic delivery is not ready for table selection."
                );
                return;
            }

            if (tableSelectedThisCycle)
                return;

            // Remember the selected table until the coroutine processes it.
            selectedTableThisCycle = tableIndex;
            tableSelectedThisCycle = true;
        }

        // Validation/logging should happen only after we know delivery is valid.
        ValidateOrderDelivery(tableIndex);

        if (tableSelectionCanvas != null)
            tableSelectionCanvas.SetActive(false);

       /* if (operatorMode)
        {
            if (operatorDeliveryRoutine != null)
                StopCoroutine(operatorDeliveryRoutine);

            operatorDeliveryRoutine = StartCoroutine(OperatorDeliverToTable(tableIndex));
            return;
        }*/

        if (operatorMode)
        {
            if (operatorDeliveryRoutine != null)
            {
                Debug.LogWarning("Delivery already in progress.");
                return;
            }
              
            operatorDeliveryRoutine = StartCoroutine(OperatorDeliverToTable(tableIndex));
            return;
        }

        // Original automatic coroutine behavior
        OnTableSelectedEvent?.Invoke(tableIndex);
    }

    private IEnumerator OperatorDeliverToTable(int tableIndex)
    {
        if (keyboardOperator != null)
            keyboardOperator.SetInputEnabled(false);

        operatorHasTray = false;
        operatorPickupArmed = false;
        isActivelyDelivering = true;

        yield return StartCoroutine(PrepareAndGoToTable(tableIndex));
        yield return StartCoroutine(PlaceTraysAndReturnToBar());

        isActivelyDelivering = false;
        operatorDeliveryRoutine = null;
        currentTarget = null;

        if (keyboardOperator != null)
            keyboardOperator.SetInputEnabled(true);

        OnDeliveryFinished?.Invoke();

        if (OrderManager.Instance != null)
            OrderManager.Instance.CheckSetCompletion();

        Debug.Log("DeliveryRobot: Robot returned to bar. Operator control re-enabled.");
    }

    private void ValidateOrderDelivery(int selectedTable)
    {
        if (OrderManager.Instance == null)
        {
            Debug.LogWarning("DeliveryRobot: OrderManager is missing.");
            return;
        }

        var activeOrder = OrderManager.Instance.GetCurrentDeliveryTargetOrder(
            out bool wasTicked,
            out int deliveryNumber,
            out int totalInSet
        );

        if (activeOrder == null)
        {
            Debug.LogWarning("DeliveryRobot: No scheduled order for this delivery attempt.");
            return;
        }

        bool isTableCorrect = selectedTable == activeOrder.targetTableIndex;

        TrayController dockedTray = GetDockedTray();

        bool areDishesCorrect = false;
        List<ItemType> actualDishes = new List<ItemType>();

        if (dockedTray != null)
        {
            actualDishes = dockedTray.GetCurrentDishTypes();
            areDishesCorrect = CompareDishLists(activeOrder.requiredItems, actualDishes);
        }

        int displaySelectedTable = selectedTable + 1;
        int displayTargetTable = activeOrder.targetTableIndex + 1;
        string orderTitle = string.IsNullOrEmpty(activeOrder.orderTitle)
            ? $"Order {deliveryNumber}"
            : activeOrder.orderTitle;

        string setName = OrderManager.Instance.CurrentSetName;

        Debug.Log(
            $"<color=cyan>[HRI Log]</color> Set: '{setName}' | Delivery: {deliveryNumber}/{totalInSet} | Order: '{orderTitle}' | " +
            $"Table Match: <b>{isTableCorrect}</b> (Selected: Table {displaySelectedTable}, Target: Table {displayTargetTable}) | " +
            $"Dish Match: <b>{areDishesCorrect}</b> (Placed: [{string.Join(", ", actualDishes)}], Required: [{string.Join(", ", activeOrder.requiredItems)}]) | " +
            $"Ticked On Board: <b>{wasTicked}</b>"
        );

        if (TrialLogging.TrialDataLogger.Instance != null)
        {
            TrialLogging.TrialDataLogger.Instance.OnOrderDispatched(
                activeOrder,
                selectedTable,
                actualDishes,
                wasTicked
            );
        }

        OrderManager.Instance.AdvanceDelivery();
    }

    private bool CompareDishLists(List<ItemType> expected, List<ItemType> actual)
    {
        if (expected == null || actual == null)
            return false;

        if (expected.Count != actual.Count)
            return false;

        List<ItemType> pool = new List<ItemType>(actual);

        foreach (var item in expected)
        {
            if (!pool.Remove(item))
                return false;
        }

        return pool.Count == 0;
    }

    private IEnumerator PrepareAndGoToTable(int tableIndex)
    {
        bool armsReady = false;

        armController.MoveBothArms(carryPose, carryPose, () => armsReady = true);
        yield return new WaitUntil(() => armsReady);

        GoToTable(tableIndex);

        yield return new WaitUntil(() =>
            agent != null &&
            !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance
        );
    }

    private void GoToTable(int tableIndex)
    {
        currentTableIndex = tableIndex;
        isAtDestination = false;
        currentTarget = tables[tableIndex];

        if (navigator != null)
        {
            navigator.MoveTo(tables[tableIndex].position, tables[tableIndex]);
        }
        else if (agent != null)
        {
            agent.SetDestination(tables[tableIndex].position);
        }
    }

    private IEnumerator PlaceTraysAndReturnToBar()
    {
        yield return new WaitForSeconds(placeDelay);

        bool armsReady = false;
        bool bodyReady = false;

        armController.MoveBothArms(placePose, placePose, () => armsReady = true);
        bodyController.MoveBodyAndHead(0.4f, bodyForwardPitch, 0f, () => bodyReady = true);

        yield return new WaitUntil(() => armsReady && bodyReady);

        if (leftGripperControl != null)
            leftGripperControl.OpenGripper();

        if (rightGripperControl != null)
            rightGripperControl.OpenGripper();

        yield return new WaitForSeconds(placeDelay);

        DisableSocketInteractors();
        DetachTrayFromSockets();

        if (TrialLogging.TrialDataLogger.Instance != null)
            TrialLogging.TrialDataLogger.Instance.OnDeliveryCompleted();

        bool armsReady2 = false;
        armController.MoveBothArms(carryPose, carryPose, () => armsReady2 = true);

        yield return new WaitUntil(() => armsReady2);

        bool bodyReady2 = false;
        float[] restPose = { 0f, -90f, 0f, 85f, 0f, -20f, 0f };

        bodyController.MoveBodyAndHead(0.5f, 0f, 0f, () => bodyReady2 = true);

        yield return new WaitUntil(() => bodyReady2);

        currentTableIndex = null;

        GoToBar();

        yield return new WaitUntil(() =>
            agent != null &&
            !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance
        );

        EnableSocketInteractors();
    }

    private void DisableSocketInteractors()
    {
        if (leftSocketInteractor != null)
        {
            leftSocketInteractor.enabled = false;
            Debug.Log("DeliveryRobot: Left socket interactor disabled.");
        }

        if (rightSocketInteractor != null)
        {
            rightSocketInteractor.enabled = false;
            Debug.Log("DeliveryRobot: Right socket interactor disabled.");
        }
    }

    private void EnableSocketInteractors()
    {
        if (leftSocketInteractor != null)
        {
            leftSocketInteractor.enabled = true;
            Debug.Log("DeliveryRobot: Left socket interactor re-enabled.");
        }

        if (rightSocketInteractor != null)
        {
            rightSocketInteractor.enabled = true;
            Debug.Log("DeliveryRobot: Right socket interactor re-enabled.");
        }
    }

    private void DetachTrayFromSockets()
    {
        if (leftSocketInteractor != null && leftSocketInteractor.interactablesSelected.Count > 0)
        {
            var selectedInteractable = leftSocketInteractor.interactablesSelected[0];

            if (selectedInteractable != null)
            {
                leftSocketInteractor.interactionManager.SelectExit(
                    leftSocketInteractor,
                    selectedInteractable
                );

                Debug.Log("DeliveryRobot: Detached tray from left socket.");
            }
        }

        if (rightSocketInteractor != null && rightSocketInteractor.interactablesSelected.Count > 0)
        {
            var selectedInteractable = rightSocketInteractor.interactablesSelected[0];

            if (selectedInteractable != null)
            {
                rightSocketInteractor.interactionManager.SelectExit(
                    rightSocketInteractor,
                    selectedInteractable
                );

                Debug.Log("DeliveryRobot: Detached tray from right socket.");
            }
        }
    }

    public void GoToBar()
    {
        if (barPoint == null)
        {
            Debug.LogWarning("DeliveryRobot: Bar Point is not assigned.");
            return;
        }

        currentTableIndex = null;
        isAtDestination = false;
        currentTarget = barPoint;

        if (navigator != null)
        {
            navigator.MoveTo(barPoint.position, barPoint);
        }
        else if (agent != null)
        {
            agent.SetDestination(barPoint.position);
        }
    }

    public void BeginOperatorPickup()
    {
        if (!operatorMode)
            return;

        if (isActivelyDelivering)
            return;

        if (operatorPickupArmed || operatorHasTray)
            return;

        operatorPickupArmed = true;

        if (tableSelectionCanvas != null)
            tableSelectionCanvas.SetActive(false);

        StartCoroutine(OperatorPrepareForTrayRoutine());
    }

    private IEnumerator OperatorPrepareForTrayRoutine()
    {
        bool armsReady = false;
        bool bodyReady = false;

        float[] restPose = { 0f, -90f, 0f, 85f, 0f, 0f, 0f };

        armController.MoveBothArms(restPose, restPose, () => armsReady = true);
        bodyController.MoveBodyAndHead(0.55f, 0f, 0f, () => bodyReady = true);

        yield return new WaitUntil(() => armsReady && bodyReady);

        EnableSocketInteractors();

        Debug.Log("DeliveryRobot: Operator pickup armed. Waiting for tray.");
    }

#if UNITY_EDITOR
    [ContextMenu("TEST - Simulate Tray Secured")]
    private void SimulateTraySecured()
    {
        if (!Application.isPlaying)
            return;

        OnTraySecured();
    }
#endif
}