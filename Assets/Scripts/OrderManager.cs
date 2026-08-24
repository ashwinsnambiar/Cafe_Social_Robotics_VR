// OrderManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class OrderManager : MonoBehaviour
{
    [System.Serializable]
    public class OrderData
    {
        public string orderTitle = "Order";
        [Tooltip("Table index matching your WaypointTable array (0-indexed or 1-indexed to match your buttons)")]
        public int targetTableIndex = 0;
        public List<ItemType> requiredItems = new List<ItemType>();

        [TextArea(2, 4)]
        public string displayDescription;
    }

    [System.Serializable]
    public class OrderSet
    {
        public string setName = "Set";
        public List<OrderData> orders = new List<OrderData>();
    }

    public static OrderManager Instance { get; private set; }

    [Header("Trial Setup")]
    public List<OrderSet> orderSets = new List<OrderSet>();
    public bool randomizeSetOrder = false;
    public bool randomizeOrdersWithinSets = false;
    [Tooltip("If enabled, table numbers are randomly distributed among the orders in each set rather than keeping fixed assignments.")]
    public bool randomizeTableAssignments = false;
    [Tooltip("Optional list of table indices to assign from (e.g. 0, 1, 2 for Tables 1, 2, 3). If empty, shuffles the existing targetTableIndex values from the orders in the set.")]
    public List<int> availableTableIndices = new List<int>();
    public int randomSeed = 0;

    [Header("UI & Timing")]
    public GameObject orderPrefab;
    public Transform contentPanel;
    public float spawnInterval = 5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip newOrderSound;
    public AudioClip completeOrderSound;

    [Header("Events")]
    public UnityEvent<int> onSetStarted;
    public UnityEvent<int> onSetCompleted;
    public UnityEvent onStudyCompleted;

    [System.Serializable]
    public class ActiveOrderInstance
    {
        public OrderData data;
        public GameObject cardObject;
    }

    private List<OrderSet> runtimeSets = new List<OrderSet>();
    private int currentSetIndex = 0;
    private int currentOrderInSetIndex = 0;
    private int currentDeliveryIndexInSet = 0;
    private int totalOrdersCompletedInSet = 0;
    private bool isPaused = false;
    private List<ActiveOrderInstance> activeOrders = new List<ActiveOrderInstance>();

    public IReadOnlyList<ActiveOrderInstance> ActiveOrders => activeOrders;
    public OrderData CurrentActiveOrder => activeOrders.Count > 0 ? activeOrders[0].data : null;
    public string CurrentSetName => (currentSetIndex >= 0 && currentSetIndex < runtimeSets.Count) ? runtimeSets[currentSetIndex].setName : $"Set {currentSetIndex + 1}";
    public int CurrentSetNumber => currentSetIndex + 1;
    public int CurrentSetIndex
    {
        get => currentSetIndex;
        set => currentSetIndex = value;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        foreach (Transform child in contentPanel) Destroy(child.gameObject);
        InitializeTrialData();

        if (ExperimentSessionManager.Instance != null)
        {
            currentSetIndex = ExperimentSessionManager.Instance.CurrentSetIndex;
        }

        StartNextSet();
    }

    private void InitializeTrialData()
    {
        if (randomSeed != 0) Random.InitState(randomSeed);

        runtimeSets = new List<OrderSet>();
        foreach (var set in orderSets)
        {
            OrderSet copy = new OrderSet
            {
                setName = set.setName,
                orders = new List<OrderData>()
            };

            // Deep clone so runtime table randomization doesn't alter inspector asset data
            foreach (var order in set.orders)
            {
                copy.orders.Add(new OrderData
                {
                    orderTitle = order.orderTitle,
                    targetTableIndex = order.targetTableIndex,
                    requiredItems = new List<ItemType>(order.requiredItems),
                    displayDescription = order.displayDescription
                });
            }

            if (randomizeOrdersWithinSets) ShuffleList(copy.orders);

            if (randomizeTableAssignments)
            {
                AssignRandomTables(copy.orders);
            }

            runtimeSets.Add(copy);
        }

        if (randomizeSetOrder) ShuffleList(runtimeSets);
    }

    private void AssignRandomTables(List<OrderData> orders)
    {
        if (orders == null || orders.Count == 0) return;

        List<int> tablePool;
        if (availableTableIndices != null && availableTableIndices.Count > 0)
        {
            tablePool = new List<int>(availableTableIndices);
        }
        else
        {
            tablePool = new List<int>();
            foreach (var o in orders)
            {
                tablePool.Add(o.targetTableIndex);
            }
        }

        ShuffleList(tablePool);

        for (int i = 0; i < orders.Count; i++)
        {
            orders[i].targetTableIndex = tablePool[i % tablePool.Count];
        }
    }

    private void StartNextSet()
    {
        if (currentSetIndex >= runtimeSets.Count)
        {
            onStudyCompleted?.Invoke();
            Debug.Log("<color=green>All Trial Sets Finished.</color>");
            return;
        }

        currentOrderInSetIndex = 0;
        currentDeliveryIndexInSet = 0;
        totalOrdersCompletedInSet = 0;
        activeOrders.Clear();
        isPaused = false;
        onSetStarted?.Invoke(currentSetIndex);
        StartCoroutine(SpawnOrdersRoutine());
    }

    public OrderData GetCurrentDeliveryTargetOrder(out bool isTicked, out int deliveryNumber, out int totalOrdersInSet)
    {
        isTicked = false;
        deliveryNumber = currentDeliveryIndexInSet + 1;
        totalOrdersInSet = 0;

        if (currentSetIndex >= runtimeSets.Count) return null;
        var currentSet = runtimeSets[currentSetIndex];
        totalOrdersInSet = currentSet.orders.Count;

        if (currentDeliveryIndexInSet >= currentSet.orders.Count)
        {
            return null;
        }

        OrderData target = currentSet.orders[currentDeliveryIndexInSet];

        // An active order instance still in activeOrders list means it has not been ticked yet
        bool stillOnScreen = activeOrders.Exists(a => a.data == target);
        isTicked = !stillOnScreen;

        return target;
    }

    public void AdvanceDelivery()
    {
        currentDeliveryIndexInSet++;

        if (currentSetIndex < runtimeSets.Count && currentDeliveryIndexInSet >= runtimeSets[currentSetIndex].orders.Count)
        {
            CheckSetCompletion();
        }
    }

    private IEnumerator SpawnOrdersRoutine()
    {
        var currentSet = runtimeSets[currentSetIndex];

        while (currentOrderInSetIndex < currentSet.orders.Count)
        {
            if (isPaused) yield break;

            SpawnOrder(currentSet.orders[currentOrderInSetIndex]);
            currentOrderInSetIndex++;

            if (currentOrderInSetIndex < currentSet.orders.Count)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }

    private void SpawnOrder(OrderData order)
    {
        GameObject card = Instantiate(orderPrefab, contentPanel);
        ActiveOrderInstance instance = new ActiveOrderInstance
        {
            data = order,
            cardObject = card
        };
        activeOrders.Add(instance);

        // Bind tick / clear button on this specific card
        Button clearBtn = card.GetComponentInChildren<Button>();
        if (clearBtn != null)
        {
            clearBtn.onClick.RemoveAllListeners();
            clearBtn.onClick.AddListener(() => CompleteOrder(instance));
        }

        if (audioSource && newOrderSound) audioSource.PlayOneShot(newOrderSound);

        TMP_Text textComp = card.GetComponentInChildren<TMP_Text>();
        if (textComp != null)
        {
            string desc = string.IsNullOrEmpty(order.displayDescription)
                ? string.Join("\n", order.requiredItems)
                : order.displayDescription;

            int displayTableNumber = order.targetTableIndex + 1;
            string titlePrefix = string.IsNullOrEmpty(order.orderTitle) ? "Order" : order.orderTitle;

            textComp.text = $"<b>{titlePrefix} (Deliver to Table: {displayTableNumber})</b>\n{desc}";
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel.GetComponent<RectTransform>());
    }

    public void CompleteOrder(ActiveOrderInstance instance)
    {
        if (instance == null || !activeOrders.Contains(instance)) return;

        if (audioSource && completeOrderSound) audioSource.PlayOneShot(completeOrderSound);

        if (instance.cardObject != null) Destroy(instance.cardObject);
        activeOrders.Remove(instance);
        totalOrdersCompletedInSet++;

        CheckSetCompletion();
    }

    public void CheckSetCompletion()
    {
        if (isPaused || currentSetIndex >= runtimeSets.Count) return;

        int totalInSet = runtimeSets[currentSetIndex].orders.Count;

        // Condition for set completion:
        // 1. All order cards must have been cleared/ticked by the VR user (activeOrders list is empty)
        // 2. All orders in the set must have been completed (totalOrdersCompletedInSet >= totalInSet)
        // 3. The robot must have received all delivery dispatches (currentDeliveryIndexInSet >= totalInSet)
        // 4. The robot must have finished placing the order and returned/finished delivering (!isActivelyDelivering)
        if (totalOrdersCompletedInSet >= totalInSet && activeOrders.Count == 0 && currentDeliveryIndexInSet >= totalInSet)
        {
            DeliveryRobot robot = FindAnyObjectByType<DeliveryRobot>();
            if (robot != null && robot.isActivelyDelivering)
            {
                StartCoroutine(WaitForRobotToFinishAndCompleteSet());
                return;
            }

            CompleteSetNow();
        }
    }

    private IEnumerator WaitForRobotToFinishAndCompleteSet()
    {
        DeliveryRobot robot = FindAnyObjectByType<DeliveryRobot>();
        if (robot != null)
        {
            yield return new WaitUntil(() => !robot.isActivelyDelivering);
        }

        if (isPaused || currentSetIndex >= runtimeSets.Count) yield break;

        int totalInSet = runtimeSets[currentSetIndex].orders.Count;
        if (totalOrdersCompletedInSet >= totalInSet && activeOrders.Count == 0 && currentDeliveryIndexInSet >= totalInSet)
        {
            CompleteSetNow();
        }
    }

    private void CompleteSetNow()
    {
        if (isPaused || currentSetIndex >= runtimeSets.Count) return;

        isPaused = true;
        int finishedIdx = currentSetIndex;
        currentSetIndex++;
        onSetCompleted?.Invoke(finishedIdx);
    }

    public void CompleteCurrentOrder()
    {
        if (activeOrders.Count > 0)
        {
            CompleteOrder(activeOrders[0]);
        }
    }

    public void ResumeNextSet()
    {
        if (!isPaused) return;
        foreach (Transform child in contentPanel) Destroy(child.gameObject);
        StartNextSet();
    }

    private void ShuffleList<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[r];
            list[r] = temp;
        }
    }
}