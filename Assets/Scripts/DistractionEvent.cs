using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class DistractionEvent : MonoBehaviour
{
    [Header("Event Assets")]
    [Tooltip("The unspilled drink/plate sitting on the table.")]
    public GameObject intactObject;
    
    [Header("Operator Controls")]
    [Tooltip("Press this key on the host PC to trigger the event.")]
    public KeyCode operatorTriggerKey = KeyCode.Space;

    private bool hasTriggered = false;
    private bool isLocked = false;
    [Header("Fall Settings")]
    [Tooltip("Y position of the ground. The script will detect when the object reaches this Y to trigger the explosion.")]
    public float groundY = 0f;

    [Header("Push Forces")]
    [Tooltip("Initial push force applied on the X axis (world space) when the object is released.")]
    public float pushForceX = 0f;

    [Tooltip("Initial push force applied on the Z axis (world space) when the object is released.")]
    public float pushForceZ = 0f;

    [Header("Robot Integration")]
    public UnityEvent<Transform> onCrashOccurred;

    void Start()
    {
        // Ensure the correct starting state
        if (intactObject != null) intactObject.SetActive(true);

        // If an intact object has a Rigidbody, keep it kinematic at start so it sits on the table
        if (intactObject != null)
        {
            var rb = intactObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    void Update()
    {
        // Use the new Input System for keyboard input (match ExplosionController)
        if (Keyboard.current == null)
        {
            Debug.LogWarning("DistractionEvent: Keyboard.current is null. Ensure the Input System package is installed and 'Active Input Handling' is set to 'Input System Package (New)'.");
            return;
        }

        if (!hasTriggered && !isLocked)
        {
            // Only support a small set of keys directly via the Input System
            if (operatorTriggerKey == KeyCode.Space && Keyboard.current.spaceKey.wasPressedThisFrame)
                TriggerSpill();
            else if (operatorTriggerKey == KeyCode.E && Keyboard.current.eKey.wasPressedThisFrame)
                TriggerSpill();
            else if (operatorTriggerKey == KeyCode.R && Keyboard.current.rKey.wasPressedThisFrame)
                TriggerSpill();
        }
    }

    // The [ContextMenu] attribute allows the operator to right-click this script 
    // in the Unity Inspector and click "Trigger Spill Event" to fire it manually.
    [ContextMenu("Trigger Spill Event")]
    public void TriggerSpill()
    {
        if (hasTriggered || isLocked) return;

        hasTriggered = true;

        // If intact object has a Rigidbody, release it to fall to the ground and apply X/Z push
        if (intactObject != null)
        {
            var rb = intactObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;

                // Apply push in X-Z plane
                Vector3 push = new Vector3(pushForceX, 0f, pushForceZ);
                if (push != Vector3.zero)
                {
                    rb.AddForce(push, ForceMode.VelocityChange);
                }

                // Attach an ImpactNotifier to trigger explosion immediately on hitting the ground
                var notifier = intactObject.AddComponent<ImpactNotifier>();
                notifier.groundY = groundY;
                notifier.onImpact = HandleImpact;
            }
            else
            {
                var explosiveComp = intactObject.GetComponent<ExpObj.ExplosiveObject>();
                if (explosiveComp != null)
                {
                    explosiveComp.Explode();
                }
                else
                {
                    Destroy(intactObject);
                }
            }
        }
        else
        {
            Debug.LogWarning("DistractionEvent: No intactObject assigned when triggering spill.");
        }

        Debug.Log($"[Study Data] Distraction triggered at Time: {Time.time}");
    }

    // Called by ImpactNotifier when the falling object hits the ground
    void HandleImpact(Transform t)
    {
        if (t == null)
        {
            Debug.LogWarning("DistractionEvent: HandleImpact called with null Transform.");
            return;
        }

        // Notify any listeners (e.g. the robot cleanup sequence) that a crash occurred here
        onCrashOccurred?.Invoke(t);

        var explosiveComp = t.GetComponent<ExpObj.ExplosiveObject>();
        if (explosiveComp != null)
        {
            explosiveComp.Explode();
        }
        else
        {
            Destroy(t.gameObject);
        }
    }

    // A small helper component that watches for the object's Y position or collision
    // and invokes a callback immediately when the object hits the groundY level.
    class ImpactNotifier : MonoBehaviour
    {
        public float groundY = 0f;
        public Action<Transform> onImpact;

        void OnCollisionEnter(Collision collision)
        {
            // If we collide with something at or below groundY, trigger immediately
            if (transform.position.y <= groundY + 0.1f)
            {
                onImpact?.Invoke(transform);
                Destroy(this);
            }
        }

        void Update()
        {
            if (transform.position.y <= groundY + 0.05f)
            {
                onImpact?.Invoke(transform);
                Destroy(this);
            }
        }
    }

    /// <summary>Prevents this event from being triggered (called by RobotTaskScheduler).</summary>
    public void Lock() { isLocked = true; }

    /// <summary>Re-enables this event after cleanup is complete.</summary>
    public void Unlock() { isLocked = false; }

    [ContextMenu("Reset Event")]
    public void ResetEvent()
    {
        hasTriggered = false;
        if (intactObject != null)
        {
            intactObject.SetActive(true);
            var rb = intactObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
