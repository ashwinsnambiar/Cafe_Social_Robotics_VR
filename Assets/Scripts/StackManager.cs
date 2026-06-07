using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class StackManager : MonoBehaviour
{
    [SerializeField] private bool autoPopulateFromChildren = true;
    [SerializeField] private List<XRGrabInteractable> stackItems = new List<XRGrabInteractable>();
    [SerializeField] private bool disableSocketsForStackedItems = true;
    [Header("Rigidbody Kinematic Management")]
    [Tooltip("If enabled, all non-top and non-grabbed items will be set kinematic to steady the stack. Top/grabbed items will be non-kinematic.")]
    [SerializeField] private bool manageRigidbodyKinematic = true;
    [Header("Activation Delay")]
    [Tooltip("Delay in seconds before enabling the next grabbable item / sockets after a grab/release. Helps avoid registration recursion when stacked items have sockets.")]
    [SerializeField] private float activationDelay = 10f;
    [Header("Stack Area")]
    [Tooltip("Only trays within this XZ radius from the stack origin are considered part of the stack.")]
    [SerializeField] private float stackAreaRadius = 0.25f;
    [SerializeField] private Transform stackOrigin;

    private readonly HashSet<XRGrabInteractable> grabbedItems = new HashSet<XRGrabInteractable>();
    private bool isInitialized;
    private Coroutine delayedUpdateCoroutine;

    private void OnEnable()
    {
        if (!isInitialized)
        {
            InitializeStack();
        }

        RegisterListeners();
        UpdateGrabbableState();
    }

    private void OnDisable()
    {
        UnregisterListeners();
        if (delayedUpdateCoroutine != null)
        {
            StopCoroutine(delayedUpdateCoroutine);
            delayedUpdateCoroutine = null;
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            InitializeStack();
            UpdateGrabbableState();
        }
    }

    private void InitializeStack()
    {
        if (autoPopulateFromChildren)
        {
            stackItems = new List<XRGrabInteractable>(GetComponentsInChildren<XRGrabInteractable>(true));
        }

        if (stackOrigin == null)
        {
            stackOrigin = transform;
        }

        stackItems.RemoveAll(item => item == null);
        stackItems.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
        isInitialized = true;
    }

    private void RegisterListeners()
    {
        foreach (var item in stackItems)
        {
            if (item == null)
            {
                continue;
            }

            item.selectEntered.RemoveListener(OnItemSelected);
            item.selectExited.RemoveListener(OnItemReleased);
            item.selectEntered.AddListener(OnItemSelected);
            item.selectExited.AddListener(OnItemReleased);
        }
    }

    private void UnregisterListeners()
    {
        foreach (var item in stackItems)
        {
            if (item == null)
            {
                continue;
            }

            item.selectEntered.RemoveListener(OnItemSelected);
            item.selectExited.RemoveListener(OnItemReleased);
        }
    }

    private void OnItemSelected(SelectEnterEventArgs args)
    {
        if (args.interactableObject is XRGrabInteractable grabInteractable)
        {
            grabbedItems.Add(grabInteractable);
            ScheduleUpdateWithDelay();
        }
    }

    private void OnItemReleased(SelectExitEventArgs args)
    {
        if (args.interactableObject is XRGrabInteractable grabInteractable)
        {
            grabbedItems.Remove(grabInteractable);
            ScheduleUpdateWithDelay();
        }
    }

    private void ScheduleUpdateWithDelay()
    {
        if (activationDelay <= 0f)
        {
            UpdateGrabbableState();
            return;
        }

        if (delayedUpdateCoroutine != null)
            StopCoroutine(delayedUpdateCoroutine);

        delayedUpdateCoroutine = StartCoroutine(DelayedUpdate());
    }

    private IEnumerator DelayedUpdate()
    {
        yield return new WaitForSeconds(activationDelay);
        UpdateGrabbableState();
        delayedUpdateCoroutine = null;
    }

    private void UpdateGrabbableState()
    {
        stackItems.RemoveAll(item => item == null);
        if (stackItems.Count == 0)
        {
            return;
        }

        XRGrabInteractable topItem = null;
        float topHeight = float.MinValue;

        foreach (var item in stackItems)
        {
            if (item == null || grabbedItems.Contains(item))
            {
                continue;
            }

            if (!IsInStackArea(item.transform))
            {
                continue;
            }

            var itemHeight = item.transform.position.y;
            if (itemHeight > topHeight)
            {
                topHeight = itemHeight;
                topItem = item;
            }
        }

        foreach (var item in stackItems)
        {
            if (item == null)
            {
                continue;
            }

            var inStackArea = IsInStackArea(item.transform);
            var shouldEnable = !inStackArea || grabbedItems.Contains(item) || item == topItem;
            if (item.enabled != shouldEnable)
            {
                item.enabled = shouldEnable;
            }

            if (disableSocketsForStackedItems)
            {
                SetSocketState(item, !inStackArea || grabbedItems.Contains(item));
            }

            if (manageRigidbodyKinematic)
            {
                // When shouldEnable is true (top item, grabbed, or outside stack), make non-kinematic so it can be grabbed/moved.
                // All others are made kinematic to steady the stack.
                SetRigidbodyKinematic(item, !shouldEnable);
            }
        }
    }

    private bool IsInStackArea(Transform itemTransform)
    {
        if (stackOrigin == null)
        {
            return true;
        }

        var originPosition = stackOrigin.position;
        var itemPosition = itemTransform.position;
        var deltaX = itemPosition.x - originPosition.x;
        var deltaZ = itemPosition.z - originPosition.z;
        return (deltaX * deltaX + deltaZ * deltaZ) <= (stackAreaRadius * stackAreaRadius);
    }

    private static void SetSocketState(Component item, bool enabledState)
    {
        var sockets = item.GetComponentsInChildren<XRSocketInteractor>(true);
        foreach (var socket in sockets)
        {
            if (socket == null || socket.enabled == enabledState)
            {
                continue;
            }

            socket.enabled = enabledState;
        }
    }

    private static void SetRigidbodyKinematic(Component item, bool isKinematic)
    {
        if (item == null)
            return;

        // Prefer Rigidbody on the same object, fall back to parent
        var rb = item.GetComponent<Rigidbody>() ?? item.GetComponentInParent<Rigidbody>();
        if (rb == null)
            return;

        if (rb.isKinematic == isKinematic)
            return;

        rb.isKinematic = isKinematic;
    }
}
