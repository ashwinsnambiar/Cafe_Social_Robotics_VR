using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GripperSocketController : MonoBehaviour
{
    public GripperControl gripperControl;
    public XRSocketInteractor socketInteractor;
    public DeliveryRobot deliveryRobot;

    [Header("Detection Settings")]
    public float failThresholdDegrees = 40f; // If joints exceed this, we missed the tray
    public float closeDelay = 1.0f;          // Delay before closing gripper after tray placement
    public float checkFailureDelay = 1.0f;   // Wait for physics to settle before checking failure

    private bool isWaitingToClose = false;
    private bool isClosing = false;
    private float timer = 0f;

    void OnEnable()
    {
        socketInteractor.selectEntered.AddListener(OnTrayPlaced);
        socketInteractor.selectExited.AddListener(OnTrayRemoved);
    }

    void OnDisable()
    {
        socketInteractor.selectEntered.RemoveListener(OnTrayPlaced);
        socketInteractor.selectExited.RemoveListener(OnTrayRemoved);
    }

    private void OnTrayPlaced(SelectEnterEventArgs args)
    {
        Debug.Log("Tray detected in socket. Waiting to close gripper...");
        isWaitingToClose = true;
        timer = 0f;
    }

    private void OnTrayRemoved(SelectExitEventArgs args)
    {
        Debug.Log("Tray removed. Opening gripper.");
        gripperControl.OpenGripper();
        isClosing = false;
        isWaitingToClose = false;
    }

    void Update()
    {
        if (isWaitingToClose)
        {
            timer += Time.deltaTime;

            if (timer > closeDelay)
            {
                Debug.Log("Closing gripper...");
                gripperControl.CloseGripper();
                isWaitingToClose = false;
                isClosing = true;
                timer = 0f;
            }
        }
        else if (isClosing)
        {
            timer += Time.deltaTime;

            // Give the gripper time to move before checking if it "missed"
            if (timer > checkFailureDelay)
            {
                CheckForMiss();
            }
        }
    }

    private void CheckForMiss()
    {
        // Check the current angle of the actuated joint (converted to degrees)
        float currentAngle = gripperControl.actuatedJoint.jointPosition[0] * Mathf.Rad2Deg;

        // If the angle is too high, it means the fingers didn't hit the tray's colliders
        if (currentAngle >= failThresholdDegrees)
        {
            Debug.LogWarning("Gripper missed tray! Re-opening.");
            gripperControl.OpenGripper();
            isClosing = false;

            // Optional: Eject the tray from the socket so the user can try again
            // socketInteractor.interactionManager.SelectExit(socketInteractor, socketInteractor.interactablesSelected[0]);
        }
        else
        {
            // Successfully closed on tray, go to table
            deliveryRobot.GoToTable();
            isClosing = false;
        }
    }
}