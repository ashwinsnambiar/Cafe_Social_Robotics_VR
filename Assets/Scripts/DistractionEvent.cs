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

        if (!Application.isFocused)
        {
            Debug.Log("DistractionEvent: Application not focused. Click the Game view and press keys while in Play mode.");
        }

        if (!hasTriggered)
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
        Debug.Log("DistractionEvent: TriggerSpill called.");
        if (hasTriggered) return;

        hasTriggered = true;

        // If intact object has a Rigidbody, release it to fall to the ground and apply X/Z push
        if (intactObject != null)
        {
            var rb = intactObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log($"DistractionEvent: Releasing intact object '{intactObject.name}' with Rigidbody. Kinematic -> false, useGravity -> true.");
                rb.isKinematic = false;
                rb.useGravity = true;

                // Apply push in X-Z plane
                Vector3 push = new Vector3(pushForceX, 0f, pushForceZ);
                if (push != Vector3.zero)
                {
                    rb.AddForce(push, ForceMode.VelocityChange);
                    Debug.Log($"DistractionEvent: Applied push force {push} to '{intactObject.name}'.");
                }

                // Attach an ImpactNotifier to trigger explosion immediately on hitting the ground
                var notifier = intactObject.AddComponent<ImpactNotifier>();
                notifier.groundY = groundY;
                notifier.onImpact = HandleImpact;
                Debug.Log($"DistractionEvent: Attached ImpactNotifier to '{intactObject.name}' (groundY={groundY}).");
            }
            else
            {
                // No rigidbody — try to explode via ExplosiveObject, otherwise destroy
                Debug.Log($"DistractionEvent: No Rigidbody found on '{intactObject.name}'. Using fallback destruction/explosion.");
                var explosiveComp = intactObject.GetComponent<ExpObj.ExplosiveObject>();
                if (explosiveComp != null)
                {
                    Debug.Log($"DistractionEvent: Found ExplosiveObject on '{intactObject.name}', calling Explode().");
                    explosiveComp.Explode();
                }
                else
                {
                    Debug.Log($"DistractionEvent: Destroying intact object '{intactObject.name}'.");
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

        Debug.Log($"DistractionEvent: HandleImpact invoked for '{t.gameObject.name}' at position {t.position}.");
        // Notify any listeners (e.g. the robot cleanup sequence) that a crash occurred here
        Debug.Log("DistractionEvent: Invoking onCrashOccurred UnityEvent.");
        onCrashOccurred?.Invoke(t);

        // Fallback: directly find a RobotCleanupSequence in the scene and call it so the robot hears the crash
        var robot = FindObjectOfType<RobotCleanupSequence>();
        if (robot != null)
        {
            Debug.Log($"DistractionEvent: Directly notifying RobotCleanupSequence on '{robot.gameObject.name}' as a fallback.");
            if (!robot.enabled)
            {
                Debug.Log("DistractionEvent: RobotCleanupSequence component was disabled - enabling so it can process the event.");
                robot.enabled = true;
            }
            robot.OnHeardCrash(t);
            Debug.Log("DistractionEvent: Fallback notification to robot complete.");
        }

        var explosiveComp = t.GetComponent<ExpObj.ExplosiveObject>();
        if (explosiveComp != null)
        {
            Debug.Log($"DistractionEvent: ExplosiveObject found on '{t.gameObject.name}', calling Explode().");
            explosiveComp.Explode();
        }
        else
        {
            Debug.Log($"DistractionEvent: No ExplosiveObject on '{t.gameObject.name}', destroying GameObject.");
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
            Debug.Log($"ImpactNotifier: OnCollisionEnter detected collision with '{collision.gameObject.name}'. Current Y={transform.position.y}, groundY={groundY}.");
            if (transform.position.y <= groundY + 0.1f)
            {
                Debug.Log("ImpactNotifier: Position at or below ground threshold, invoking onImpact.");
                onImpact?.Invoke(transform);
                Destroy(this);
            }
        }

        void Update()
        {
            if (transform.position.y <= groundY + 0.05f)
            {
                Debug.Log($"ImpactNotifier: Update detected Y={transform.position.y} <= groundY+0.05 ({groundY + 0.05f}), invoking onImpact.");
                onImpact?.Invoke(transform);
                Destroy(this);
            }
        }
    }

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
