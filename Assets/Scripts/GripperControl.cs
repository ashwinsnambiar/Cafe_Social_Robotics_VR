using UnityEngine;

public class GripperControl : MonoBehaviour
{
    public ArticulationBody actuatedJoint;
    public ArticulationBody[] slaveJoints;
    public float[] multipliers;
    public float[] offsets;

    [Header("Joint Settings")]
    public float stiffness = 10000f;
    public float damping = 100f;
    public float forceLimit = 1000f;

    [Header("Positions (Degrees)")]
    public float openPosition = 0f;
    public float closedPosition = 45f;

    public bool autoTest = false;
    private float testInterval = 2f;
    private float nextTestTime;

    void Start()
    {
        nextTestTime = Time.time + testInterval;

        // Safety check to prevent IndexOutOfRange errors
        if (multipliers.Length < slaveJoints.Length || offsets.Length < slaveJoints.Length)
        {
            Debug.LogError("GripperControl: Please ensure Multipliers and Offsets arrays match the size of Slave Joints.");
        }

        // Set gripper to open position initially
        OpenGripper();
    }

    void Update()
    {
        if (autoTest && Time.time >= nextTestTime)
        {
            if (Mathf.Approximately(actuatedJoint.xDrive.target, openPosition))
                CloseGripper();
            else
                OpenGripper();

            nextTestTime = Time.time + testInterval;
        }
    }

    void FixedUpdate()
    {
        if (actuatedJoint == null || slaveJoints == null) return;

        // Convert the Master joint's current internal position (Radians) to Degrees
        float currentMasterPosDeg = actuatedJoint.jointPosition[0] * Mathf.Rad2Deg;

        for (int i = 0; i < slaveJoints.Length; i++)
        {
            if (slaveJoints[i] == null) continue;

            var drive = slaveJoints[i].xDrive;

            // Calculate the target for this specific slave
            float targetPos = (currentMasterPosDeg * multipliers[i]) + offsets[i];

            drive.target = targetPos;
            drive.stiffness = stiffness;
            drive.damping = damping;
            drive.forceLimit = forceLimit;

            // Explicitly set the drive to move toward the target
            slaveJoints[i].xDrive = drive;
        }
    }

    public void CloseGripper() => SetGripperPosition(closedPosition);
    public void OpenGripper() => SetGripperPosition(openPosition);

    private void SetGripperPosition(float position)
    {
        var drive = actuatedJoint.xDrive;
        drive.target = position;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        actuatedJoint.xDrive = drive;
    }
}