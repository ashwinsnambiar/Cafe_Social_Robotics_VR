using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrialLogging
{
    public class TrialDataLogger : MonoBehaviour
    {
        public static TrialDataLogger Instance { get; private set; }

        [Header("Subject Settings")]
        [Tooltip("Unique Participant / Subject ID.")]
        public string subjectId = "Subject_01";

        [Header("Export Settings")]
        [Tooltip("Relative or absolute export folder path. Leave empty to use 'TrialLogs' in project root.")]
        public string customLogDirectory = "";

        [Tooltip("If true, automatically updates exports after every order and set to prevent data loss.")]
        public bool autoSaveIncrementally = true;

        [Tooltip("If true, opens the log folder in Windows Explorer when the study finishes.")]
        public bool openFolderOnStudyComplete = false;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private string activeSessionId = "";
        [SerializeField] private int overallOrderCounter = 0;

        public TrialSessionRecord CurrentSession { get; private set; } = new TrialSessionRecord();
        private SetLogRecord currentSetRecord = null;
        private OrderLogRecord lastDispatchedOrder = null;

        private string sessionFolder = "";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSession();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncSubjectId();
        }

        private void Start()
        {
            SyncSubjectId();
        }

        private void OnApplicationQuit()
        {
            // Safety flush on game quit / editor play mode stop
            if (CurrentSession != null && CurrentSession.sets.Count > 0)
            {
                if (string.IsNullOrEmpty(CurrentSession.sessionEnd))
                {
                    CurrentSession.sessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    CurrentSession.sessionEndTimeSeconds = Time.realtimeSinceStartup;
                }

                if (currentSetRecord != null && currentSetRecord.endTimeSeconds <= 0f)
                {
                    currentSetRecord.endTimeSeconds = Time.realtimeSinceStartup;
                    currentSetRecord.endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    currentSetRecord.CalculateSummary();
                }

                FlushExports();
                Debug.Log("<color=yellow>[TrialDataLogger]</color> Flushed trial logs on application quit.");
            }
        }

        public void SyncSubjectId()
        {
            if (ExperimentSessionManager.Instance != null && !string.IsNullOrEmpty(ExperimentSessionManager.Instance.subjectId))
            {
                subjectId = ExperimentSessionManager.Instance.subjectId;
                if (CurrentSession != null) CurrentSession.subjectId = subjectId;
            }
        }

        private void InitializeSession()
        {
            SyncSubjectId();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            activeSessionId = $"{subjectId}_{timestamp}";

            CurrentSession = new TrialSessionRecord
            {
                subjectId = subjectId,
                sessionId = activeSessionId,
                sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sessionStartTimeSeconds = Time.realtimeSinceStartup
            };

            string rootDir = string.IsNullOrEmpty(customLogDirectory)
                ? Path.Combine(Application.dataPath, "..", "TrialLogs")
                : customLogDirectory;

            sessionFolder = Path.Combine(rootDir, activeSessionId);

            if (!Directory.Exists(sessionFolder))
            {
                Directory.CreateDirectory(sessionFolder);
            }

            Debug.Log($"<color=cyan>[TrialDataLogger]</color> Initialized trial session: <b>{activeSessionId}</b>\nOutput Directory: {sessionFolder}");
        }

        public string GetSessionFolder()
        {
            if (string.IsNullOrEmpty(sessionFolder))
            {
                string rootDir = string.IsNullOrEmpty(customLogDirectory)
                    ? Path.Combine(Application.dataPath, "..", "TrialLogs")
                    : customLogDirectory;
                sessionFolder = Path.Combine(rootDir, activeSessionId);
            }
            return sessionFolder;
        }

        // ────────────────────────────────────────────────────────────────
        //  Event Hooks
        // ────────────────────────────────────────────────────────────────

        public void OnSetStarted(int setIndex, string setName, int totalOrdersInSet)
        {
            SyncSubjectId();

            // Find or create SetLogRecord
            currentSetRecord = CurrentSession.sets.FirstOrDefault(s => s.setIndex == setIndex);
            if (currentSetRecord == null)
            {
                currentSetRecord = new SetLogRecord
                {
                    subjectId = subjectId,
                    sessionId = activeSessionId,
                    setIndex = setIndex,
                    setNumber = setIndex + 1,
                    setName = string.IsNullOrEmpty(setName) ? $"Set {setIndex + 1}" : setName,
                    startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    startTimeSeconds = Time.realtimeSinceStartup,
                    totalOrders = totalOrdersInSet
                };
                CurrentSession.sets.Add(currentSetRecord);
            }
            else
            {
                // Reloaded set
                currentSetRecord.startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                currentSetRecord.startTimeSeconds = Time.realtimeSinceStartup;
            }

            CurrentSession.totalSetsConfigured = Mathf.Max(CurrentSession.totalSetsConfigured, setIndex + 1);

            Debug.Log($"<color=cyan>[TrialDataLogger]</color> Set {setIndex + 1} ('{setName}') started with {totalOrdersInSet} orders.");
        }

        public void OnOrderSpawned(OrderManager.OrderData orderData, int orderIndexInSet)
        {
            if (currentSetRecord == null)
            {
                OnSetStarted(0, "Set 1", 1);
            }

            overallOrderCounter++;

            var orderRecord = new OrderLogRecord
            {
                subjectId = subjectId,
                sessionId = activeSessionId,
                setIndex = currentSetRecord.setIndex,
                setNumber = currentSetRecord.setNumber,
                setName = currentSetRecord.setName,
                orderIndexInSet = orderIndexInSet,
                orderNumberInSet = orderIndexInSet + 1,
                overallOrderNumber = overallOrderCounter,
                workloadCondition = ExperimentSessionManager.Instance != null ? ExperimentSessionManager.Instance.workloadCondition.ToString() : "LowIntensity",
                orderTitle = string.IsNullOrEmpty(orderData.orderTitle) ? $"Order {orderIndexInSet + 1}" : orderData.orderTitle,
                displayDescription = orderData.displayDescription,
                calorieLimit = orderData.calorieLimit,
                sugarLimit = orderData.sugarLimit,
                fatLimit = orderData.fatLimit,
                targetTableIndex = orderData.targetTableIndex,
                targetTableNumber = orderData.targetTableIndex + 1,
                requiredItems = orderData.requiredItems.Select(i => i.ToString()).ToList(),
                spawnTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                spawnTimeSeconds = Time.realtimeSinceStartup
            };

            currentSetRecord.orders.Add(orderRecord);

            Debug.Log($"<color=cyan>[TrialDataLogger]</color> Order #{overallOrderCounter} spawned: '{orderRecord.orderTitle}' for Table {orderRecord.targetTableNumber}. Items: [{string.Join(", ", orderRecord.requiredItems)}]");

            if (autoSaveIncrementally)
            {
                FlushExports();
            }
        }

        public void OnTraySecured(List<ItemType> currentDishTypes)
        {
            if (currentSetRecord == null) return;

            // Find pending order (spawned but not yet dispatched)
            var pendingOrder = currentSetRecord.orders.LastOrDefault(o => o.dispatchTimeSeconds < 0f);
            if (pendingOrder != null)
            {
                pendingOrder.traySecuredTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                pendingOrder.traySecuredTimeSeconds = Time.realtimeSinceStartup;
                pendingOrder.assembledItems = currentDishTypes != null
                    ? currentDishTypes.Select(d => d.ToString()).ToList()
                    : new List<string>();

                pendingOrder.CalculateMetrics();
                Debug.Log($"<color=cyan>[TrialDataLogger]</color> Tray secured for '{pendingOrder.orderTitle}'. Assembled items: [{string.Join(", ", pendingOrder.assembledItems)}]. Prep so far: {pendingOrder.timeToTraySecuredSeconds:F1}s.");
            }
        }

        public void OnOrderDispatched(OrderManager.OrderData activeOrder, int selectedTable, List<ItemType> placedDishes, bool wasTicked)
        {
            if (currentSetRecord == null) return;

            // Locate order in current set
            OrderLogRecord orderRec = null;
            if (activeOrder != null)
            {
                orderRec = currentSetRecord.orders.FirstOrDefault(o =>
                    o.targetTableIndex == activeOrder.targetTableIndex &&
                    o.orderTitle == activeOrder.orderTitle &&
                    o.dispatchTimeSeconds < 0f);
            }

            if (orderRec == null)
            {
                // Fallback to the oldest undispatched order
                orderRec = currentSetRecord.orders.FirstOrDefault(o => o.dispatchTimeSeconds < 0f);
            }

            if (orderRec != null)
            {
                orderRec.selectedTableIndex = selectedTable;
                orderRec.selectedTableNumber = selectedTable + 1;
                orderRec.assembledItems = placedDishes != null
                    ? placedDishes.Select(d => d.ToString()).ToList()
                    : new List<string>();

                orderRec.wasTickedOnBoard = wasTicked;
                orderRec.dispatchTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                orderRec.dispatchTimeSeconds = Time.realtimeSinceStartup;

                orderRec.CalculateMetrics();
                lastDispatchedOrder = orderRec;

                Debug.Log($"<color=cyan>[TrialDataLogger]</color> Order Dispatched! Order: '{orderRec.orderTitle}' | Target: Table {orderRec.targetTableNumber} vs Given: Table {orderRec.selectedTableNumber} (Match: {orderRec.isTableMatch}) | Dishes Match: {orderRec.isDishesMatch} | <b>Time to Dispatch (Prep Time): {orderRec.timeToDispatchSeconds:F1}s</b>");
            }

            if (autoSaveIncrementally)
            {
                FlushExports();
            }
        }

        public void OnDeliveryCompleted()
        {
            if (lastDispatchedOrder != null && lastDispatchedOrder.deliveryCompleteTimeSeconds < 0f)
            {
                lastDispatchedOrder.deliveryCompleteTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                lastDispatchedOrder.deliveryCompleteTimeSeconds = Time.realtimeSinceStartup;
                lastDispatchedOrder.CalculateMetrics();

                Debug.Log($"<color=cyan>[TrialDataLogger]</color> Delivery Completed for '{lastDispatchedOrder.orderTitle}'! Transit: {lastDispatchedOrder.deliveryDurationSeconds:F1}s | Total Turnaround: {lastDispatchedOrder.totalTurnaroundTimeSeconds:F1}s.");

                if (autoSaveIncrementally)
                {
                    FlushExports();
                }
            }
        }

        public void OnOrderTicked(OrderManager.OrderData orderData)
        {
            if (currentSetRecord == null) return;

            var match = currentSetRecord.orders.FirstOrDefault(o =>
                o.targetTableIndex == orderData.targetTableIndex &&
                o.orderTitle == orderData.orderTitle &&
                !o.wasTickedOnBoard);

            if (match == null)
            {
                match = currentSetRecord.orders.FirstOrDefault(o => !o.wasTickedOnBoard);
            }

            if (match != null)
            {
                match.wasTickedOnBoard = true;
                match.tickedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                match.tickedTimeSeconds = Time.realtimeSinceStartup;
                Debug.Log($"<color=cyan>[TrialDataLogger]</color> Order '{match.orderTitle}' ticked on board at {match.tickedTimestamp}.");
            }
        }

        public void OnSetCompleted(int finishedSetIndex)
        {
            if (currentSetRecord != null)
            {
                currentSetRecord.endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                currentSetRecord.endTimeSeconds = Time.realtimeSinceStartup;
                currentSetRecord.CalculateSummary();

                Debug.Log($"<color=green>[TrialDataLogger]</color> Set {currentSetRecord.setNumber} Complete! <b>Duration: {currentSetRecord.totalSetDurationSeconds:F1}s</b> | Accuracy: {currentSetRecord.overallAccuracyPercent:F1}% | Avg Prep: {currentSetRecord.avgTimeToDispatchSeconds:F1}s.");
            }

            FlushExports();
        }

        public void OnStudyCompleted()
        {
            CurrentSession.sessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CurrentSession.sessionEndTimeSeconds = Time.realtimeSinceStartup;

            if (currentSetRecord != null && currentSetRecord.endTimeSeconds <= 0f)
            {
                currentSetRecord.endTimeSeconds = Time.realtimeSinceStartup;
                currentSetRecord.endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                currentSetRecord.CalculateSummary();
            }

            CurrentSession.CalculateOverallSummary();
            FlushExports();

            Debug.Log($"<color=green>====================================================</color>\n" +
                      $"<color=green>[TrialDataLogger] TRIAL STUDY COMPLETED!</color>\n" +
                      $"Participant: <b>{CurrentSession.subjectId}</b> | Total Active Time: <b>{CurrentSession.totalActiveTrialDurationSeconds:F1}s</b>\n" +
                      $"Overall Accuracy: <b>{CurrentSession.overallAccuracyPercent:F1}%</b> ({CurrentSession.totalFullyCorrectOrders}/{CurrentSession.totalOrdersDispatched})\n" +
                      $"Avg Prep Time: <b>{CurrentSession.overallAvgTimeToDispatchSeconds:F1}s</b> | Avg Delivery: <b>{CurrentSession.overallAvgDeliveryDurationSeconds:F1}s</b>\n" +
                      $"Reports saved to: <b>{GetSessionFolder()}</b>\n" +
                      $"<color=green>====================================================</color>");

            if (openFolderOnStudyComplete)
            {
                OpenLogFolder();
            }
        }

        public void FlushExports()
        {
            try
            {
                string dir = GetSessionFolder();
                TrialReportExporter.SaveAllExports(CurrentSession, dir);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TrialDataLogger] Error exporting trial reports: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [ContextMenu("Open Trial Logs Folder")]
        public void OpenLogFolder()
        {
            string dir = GetSessionFolder();
            if (Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                Debug.LogWarning($"[TrialDataLogger] Directory does not exist yet: {dir}");
            }
        }

        [ContextMenu("Export Current Session Now")]
        public void ExportNow()
        {
            FlushExports();
            Debug.Log($"<color=green>[TrialDataLogger]</color> Manual export completed for session {activeSessionId}.");
        }
    }
}
