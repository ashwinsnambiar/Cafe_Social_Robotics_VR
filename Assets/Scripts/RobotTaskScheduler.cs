using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RobotTaskScheduler : MonoBehaviour
{
    [Header("Robot Modules")]
    public DeliveryRobot deliveryModule;
    public RobotCleanupSequence cleanupModule;

    [Header("Distraction Events — Broken Bottles")]
    [Tooltip("Assign every DistractionEvent instance in the scene.")]
    public List<DistractionEvent> distractionEvents = new List<DistractionEvent>();

    [Header("Distraction Events — Coffee Spills")]
    [Tooltip("Assign every CafeSpillManager instance in the scene.")]
    public List<CafeSpillManager> spillManagers = new List<CafeSpillManager>();

    [Header("Cleanup Settings")]
    [Tooltip("Seconds to wait between event occurrence and the robot starting its cleanup response.")]
    public float cleanupDelay = 5f;

    // ── Internal state ──────────────────────────────────────────────
    private CleanupTask pendingTask;
    private bool isCleanupActive = false;

    // Store delegates so we can properly unsubscribe in OnDestroy
    private Dictionary<CafeSpillManager, UnityAction<Transform>> spillCallbacks
        = new Dictionary<CafeSpillManager, UnityAction<Transform>>();

    void Start()
    {
        // Subscribe to every broken-bottle event
        foreach (var de in distractionEvents)
        {
            if (de != null)
                de.onCrashOccurred.AddListener(OnBottleCrashDetected);
        }

        // Subscribe to every coffee-spill event (capture manager via closure)
        foreach (var sm in spillManagers)
        {
            if (sm != null)
            {
                var manager = sm; // closure capture
                UnityAction<Transform> callback = (t) => OnCoffeeSpillDetected(t, manager);
                spillCallbacks[manager] = callback;
                sm.onSpillOccurred.AddListener(callback);
            }
        }

        // Listen for when modules finish their tasks
        deliveryModule.OnDeliveryFinished += CheckPendingTasks;
        cleanupModule.OnCleanupComplete += OnCleanupFinished;

        // Start normal café operations
        deliveryModule.EnableDelivery();
    }

    void OnDestroy()
    {
        foreach (var de in distractionEvents)
        {
            if (de != null)
                de.onCrashOccurred.RemoveListener(OnBottleCrashDetected);
        }

        foreach (var kvp in spillCallbacks)
        {
            if (kvp.Key != null)
                kvp.Key.onSpillOccurred.RemoveListener(kvp.Value);
        }
        spillCallbacks.Clear();

        if (deliveryModule != null)
            deliveryModule.OnDeliveryFinished -= CheckPendingTasks;
        if (cleanupModule != null)
            cleanupModule.OnCleanupComplete -= OnCleanupFinished;
    }

    // ── Lock / Unlock all distraction triggers ──────────────────────

    private void LockAllEvents()
    {
        foreach (var de in distractionEvents)
            if (de != null) de.Lock();
        foreach (var sm in spillManagers)
            if (sm != null) sm.Lock();
    }

    private void UnlockAllEvents()
    {
        foreach (var de in distractionEvents)
            if (de != null) de.Unlock();
        foreach (var sm in spillManagers)
            if (sm != null) sm.Unlock();
    }

    // ── Event handlers ──────────────────────────────────────────────

    /// <summary>Triggered by any DistractionEvent.onCrashOccurred.</summary>
    public void OnBottleCrashDetected(Transform crashLocation)
    {
        if (isCleanupActive)
        {
            Debug.LogWarning("RobotTaskScheduler: Ignoring bottle crash — a cleanup is already active.");
            return;
        }

        Debug.Log("[RobotTaskScheduler] Broken bottle detected! Queuing cleanup.");
        isCleanupActive = true;
        LockAllEvents();

        pendingTask = new CleanupTask
        {
            Type = CleanupType.BrokenBottle,
            Position = crashLocation.position,
            Rotation = crashLocation.rotation
        };

        // Stop delivery; when it finishes, CheckPendingTasks will fire
        deliveryModule.StopDeliverySafely();
    }

    /// <summary>Triggered by any CafeSpillManager.onSpillOccurred.</summary>
    public void OnCoffeeSpillDetected(Transform spillLocation, CafeSpillManager manager)
    {
        if (isCleanupActive)
        {
            Debug.LogWarning("RobotTaskScheduler: Ignoring coffee spill — a cleanup is already active.");
            return;
        }

        Debug.Log("[RobotTaskScheduler] Coffee spill detected! Queuing cleanup.");
        isCleanupActive = true;
        LockAllEvents();

        pendingTask = new CleanupTask
        {
            Type = CleanupType.SpilledCoffee,
            Position = spillLocation.position,
            Rotation = spillLocation.rotation,
            SpilledCupInstance = manager.LastSpilledCupInstance,
            VrIndicatorInstance = manager.LastVrIndicatorInstance
        };

        // Stop delivery; when it finishes, CheckPendingTasks will fire
        deliveryModule.StopDeliverySafely();
    }

    // ── Task flow ───────────────────────────────────────────────────

    /// <summary>
    /// Called automatically by DeliveryRobot when it finishes its current order (or was idling).
    /// Waits the configured delay, then starts cleanup.
    /// </summary>
    private void CheckPendingTasks()
    {
        if (pendingTask != null)
        {
            Debug.Log($"Delivery stopped. Waiting {cleanupDelay}s before starting cleanup...");
            StartCoroutine(DelayedCleanupStart());
        }
        else
        {
            // Nothing pending — keep taking delivery orders
            deliveryModule.EnableDelivery();
        }
    }

    private IEnumerator DelayedCleanupStart()
    {
        yield return new WaitForSeconds(cleanupDelay);

        if (pendingTask != null)
        {
            Debug.Log("Starting cleanup sequence now.");
            CleanupTask task = pendingTask;
            pendingTask = null;
            cleanupModule.StartCleanupSequence(task);
        }
    }

    /// <summary>
    /// Called automatically by RobotCleanupSequence when the mess is cleaned.
    /// Unlocks all events and resumes delivery.
    /// </summary>
    private void OnCleanupFinished()
    {
        Debug.Log("Cleanup complete. Unlocking events and resuming delivery.");
        isCleanupActive = false;
        UnlockAllEvents();
        deliveryModule.EnableDelivery();
    }
}