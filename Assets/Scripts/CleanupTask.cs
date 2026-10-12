using UnityEngine;

/// <summary>
/// Defines the type and data for a pending robot cleanup task.
/// Used by RobotTaskScheduler and RobotCleanupSequence to coordinate cleanup.
/// </summary>
public enum CleanupType
{
    BrokenBottle,
    SpilledCoffee
}

public class CleanupTask
{
    public CleanupType Type;
    public Vector3 Position;
    public Quaternion Rotation;

    /// <summary>Reference to the spawned spilled cup instance (SpilledCoffee only).</summary>
    public GameObject SpilledCupInstance;

    /// <summary>Reference to the spawned VR indicator / exclamation mark (SpilledCoffee only).</summary>
    public GameObject VrIndicatorInstance;
}
