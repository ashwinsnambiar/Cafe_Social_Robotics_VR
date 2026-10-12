using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrialLogging
{
    [Serializable]
    public class OrderLogRecord
    {
        public string subjectId = "";
        public string sessionId = "";
        public int setIndex = 0;
        public int setNumber = 1;
        public string setName = "Set 1";
        public int orderIndexInSet = 0;
        public int orderNumberInSet = 1;
        public int overallOrderNumber = 1;
        public string workloadCondition = "LowIntensity";
        public string orderTitle = "";
        public string displayDescription = "";

        public int calorieLimit = 0;
        public int sugarLimit = 0;
        public int fatLimit = 0;

        public int targetTableIndex = 0;
        public int targetTableNumber = 1;
        public int selectedTableIndex = -1;
        public int selectedTableNumber = -1;
        public bool isTableMatch = false;

        public List<string> requiredItems = new List<string>();
        public List<string> assembledItems = new List<string>();
        public bool isDishesMatch = false;
        public bool isOrderFullyCorrect = false;

        public List<string> missingItems = new List<string>();
        public List<string> extraItems = new List<string>();

        public bool wasTickedOnBoard = false;

        public string spawnTimestamp = "";
        public float spawnTimeSeconds = 0f;

        public string tickedTimestamp = "";
        public float tickedTimeSeconds = -1f;

        public string traySecuredTimestamp = "";
        public float traySecuredTimeSeconds = -1f;

        public string dispatchTimestamp = "";
        public float dispatchTimeSeconds = -1f;

        public string deliveryCompleteTimestamp = "";
        public float deliveryCompleteTimeSeconds = -1f;

        public float timeToDispatchSeconds = 0f;
        public float timeToTraySecuredSeconds = 0f;
        public float deliveryDurationSeconds = 0f;
        public float totalTurnaroundTimeSeconds = 0f;

        public void CalculateMetrics()
        {
            targetTableNumber = targetTableIndex + 1;
            selectedTableNumber = selectedTableIndex >= 0 ? (selectedTableIndex + 1) : -1;
            isTableMatch = (selectedTableIndex >= 0 && selectedTableIndex == targetTableIndex);

            // Compare required vs assembled items
            var expectedPool = new List<string>(requiredItems);
            var assembledPool = new List<string>(assembledItems);

            missingItems = new List<string>();
            extraItems = new List<string>();

            foreach (var item in expectedPool)
            {
                if (assembledPool.Contains(item))
                {
                    assembledPool.Remove(item);
                }
                else
                {
                    missingItems.Add(item);
                }
            }
            extraItems = new List<string>(assembledPool);

            isDishesMatch = (missingItems.Count == 0 && extraItems.Count == 0);
            isOrderFullyCorrect = (isTableMatch && isDishesMatch);

            // Durations
            if (dispatchTimeSeconds >= 0f && spawnTimeSeconds >= 0f)
            {
                timeToDispatchSeconds = (float)Math.Max(0.0, dispatchTimeSeconds - spawnTimeSeconds);
            }

            if (traySecuredTimeSeconds >= 0f && spawnTimeSeconds >= 0f)
            {
                timeToTraySecuredSeconds = (float)Math.Max(0.0, traySecuredTimeSeconds - spawnTimeSeconds);
            }

            if (deliveryCompleteTimeSeconds >= 0f && dispatchTimeSeconds >= 0f)
            {
                deliveryDurationSeconds = (float)Math.Max(0.0, deliveryCompleteTimeSeconds - dispatchTimeSeconds);
            }

            if (deliveryCompleteTimeSeconds >= 0f && spawnTimeSeconds >= 0f)
            {
                totalTurnaroundTimeSeconds = (float)Math.Max(0.0, deliveryCompleteTimeSeconds - spawnTimeSeconds);
            }
            else if (dispatchTimeSeconds >= 0f && spawnTimeSeconds >= 0f)
            {
                totalTurnaroundTimeSeconds = timeToDispatchSeconds;
            }
        }
    }

    [Serializable]
    public class SetLogRecord
    {
        public string subjectId = "";
        public string sessionId = "";
        public int setIndex = 0;
        public int setNumber = 1;
        public string setName = "Set 1";

        public string startTimestamp = "";
        public string endTimestamp = "";
        public float startTimeSeconds = 0f;
        public float endTimeSeconds = 0f;
        public float totalSetDurationSeconds = 0f;

        public int totalOrders = 0;
        public int completedOrders = 0;
        public int correctTableOrders = 0;
        public int correctDishOrders = 0;
        public int fullyCorrectOrders = 0;

        public float tableAccuracyPercent = 0f;
        public float dishAccuracyPercent = 0f;
        public float overallAccuracyPercent = 0f;

        public float avgTimeToDispatchSeconds = 0f;
        public float avgDeliveryDurationSeconds = 0f;
        public float avgTurnaroundSeconds = 0f;

        public List<OrderLogRecord> orders = new List<OrderLogRecord>();

        public void CalculateSummary()
        {
            if (endTimeSeconds > startTimeSeconds)
            {
                totalSetDurationSeconds = endTimeSeconds - startTimeSeconds;
            }

            totalOrders = orders.Count;
            completedOrders = orders.Count(o => o.dispatchTimeSeconds >= 0f);
            correctTableOrders = orders.Count(o => o.isTableMatch);
            correctDishOrders = orders.Count(o => o.isDishesMatch);
            fullyCorrectOrders = orders.Count(o => o.isOrderFullyCorrect);

            if (completedOrders > 0)
            {
                tableAccuracyPercent = ((float)correctTableOrders / completedOrders) * 100f;
                dishAccuracyPercent = ((float)correctDishOrders / completedOrders) * 100f;
                overallAccuracyPercent = ((float)fullyCorrectOrders / completedOrders) * 100f;

                var dispatched = orders.Where(o => o.dispatchTimeSeconds >= 0f).ToList();
                avgTimeToDispatchSeconds = dispatched.Count > 0 ? dispatched.Average(o => o.timeToDispatchSeconds) : 0f;

                var delivered = orders.Where(o => o.deliveryCompleteTimeSeconds >= 0f).ToList();
                avgDeliveryDurationSeconds = delivered.Count > 0 ? delivered.Average(o => o.deliveryDurationSeconds) : 0f;
                avgTurnaroundSeconds = delivered.Count > 0 ? delivered.Average(o => o.totalTurnaroundTimeSeconds) : avgTimeToDispatchSeconds;
            }
            else
            {
                tableAccuracyPercent = 0f;
                dishAccuracyPercent = 0f;
                overallAccuracyPercent = 0f;
                avgTimeToDispatchSeconds = 0f;
                avgDeliveryDurationSeconds = 0f;
                avgTurnaroundSeconds = 0f;
            }
        }
    }

    [Serializable]
    public class TrialSessionRecord
    {
        public string subjectId = "Subject_01";
        public string sessionId = "";
        public string sessionStart = "";
        public string sessionEnd = "";
        public float sessionStartTimeSeconds = 0f;
        public float sessionEndTimeSeconds = 0f;

        public int totalSetsConfigured = 0;
        public int completedSetsCount = 0;
        public int totalOrdersDispatched = 0;
        public int totalFullyCorrectOrders = 0;

        public float overallTableAccuracyPercent = 0f;
        public float overallDishAccuracyPercent = 0f;
        public float overallAccuracyPercent = 0f;

        public float totalActiveTrialDurationSeconds = 0f;
        public float totalWallClockDurationSeconds = 0f;

        public float overallAvgTimeToDispatchSeconds = 0f;
        public float overallAvgDeliveryDurationSeconds = 0f;
        public float overallAvgTurnaroundSeconds = 0f;

        public List<SetLogRecord> sets = new List<SetLogRecord>();

        public void CalculateOverallSummary()
        {
            completedSetsCount = sets.Count(s => s.endTimeSeconds > s.startTimeSeconds);

            var allOrders = sets.SelectMany(s => s.orders).ToList();
            var dispatchedOrders = allOrders.Where(o => o.dispatchTimeSeconds >= 0f).ToList();
            var deliveredOrders = allOrders.Where(o => o.deliveryCompleteTimeSeconds >= 0f).ToList();

            totalOrdersDispatched = dispatchedOrders.Count;
            totalFullyCorrectOrders = dispatchedOrders.Count(o => o.isOrderFullyCorrect);

            if (totalOrdersDispatched > 0)
            {
                int correctTables = dispatchedOrders.Count(o => o.isTableMatch);
                int correctDishes = dispatchedOrders.Count(o => o.isDishesMatch);

                overallTableAccuracyPercent = ((float)correctTables / totalOrdersDispatched) * 100f;
                overallDishAccuracyPercent = ((float)correctDishes / totalOrdersDispatched) * 100f;
                overallAccuracyPercent = ((float)totalFullyCorrectOrders / totalOrdersDispatched) * 100f;

                overallAvgTimeToDispatchSeconds = dispatchedOrders.Average(o => o.timeToDispatchSeconds);
            }
            else
            {
                overallTableAccuracyPercent = 0f;
                overallDishAccuracyPercent = 0f;
                overallAccuracyPercent = 0f;
                overallAvgTimeToDispatchSeconds = 0f;
            }

            if (deliveredOrders.Count > 0)
            {
                overallAvgDeliveryDurationSeconds = deliveredOrders.Average(o => o.deliveryDurationSeconds);
                overallAvgTurnaroundSeconds = deliveredOrders.Average(o => o.totalTurnaroundTimeSeconds);
            }
            else
            {
                overallAvgDeliveryDurationSeconds = 0f;
                overallAvgTurnaroundSeconds = overallAvgTimeToDispatchSeconds;
            }

            totalActiveTrialDurationSeconds = sets.Sum(s => s.totalSetDurationSeconds);

            if (sessionEndTimeSeconds > sessionStartTimeSeconds)
            {
                totalWallClockDurationSeconds = sessionEndTimeSeconds - sessionStartTimeSeconds;
            }
            else
            {
                totalWallClockDurationSeconds = totalActiveTrialDurationSeconds;
            }
        }
    }
}
