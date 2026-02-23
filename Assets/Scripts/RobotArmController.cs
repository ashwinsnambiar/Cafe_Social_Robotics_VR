using System;
using System.Collections;
using UnityEngine;

public class RobotArmController : MonoBehaviour
{
    public enum ArmSide { Left, Right }

    [Header("Settings")]
    public float jointSpeed = 45f; // degrees per second

    // Initial rest pose: Link1=0, Link2=-90, Link3=0, Link4=85, Link5/6/7=0
    private static readonly float[] RestPose = { 0f, -90f, 0f, 85f, 0f, 0f, 0f };

    private ArticulationBody[] _leftArm;
    private ArticulationBody[] _rightArm;

    private Coroutine _leftArmRoutine;
    private Coroutine _rightArmRoutine;

    private ArticulationBody FindArticulationBodyRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child.GetComponent<ArticulationBody>();
            }

            ArticulationBody found = FindArticulationBodyRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    void Start()
    {
        // Automatically populate left and right arm links based on naming convention
        _leftArm = new ArticulationBody[7];
        _rightArm = new ArticulationBody[7];

        for (int i = 0; i < 7; i++)
        {
            string leftLinkName = $"Link{i + 1}_l";
            string rightLinkName = $"Link{i + 1}_r";

            _leftArm[i] = FindArticulationBodyRecursive(transform, leftLinkName);
            _rightArm[i] = FindArticulationBodyRecursive(transform, rightLinkName);

            if (_leftArm[i] == null || _rightArm[i] == null)
            {
                Debug.LogError($"[RobotArmController] Failed to find articulation body for {leftLinkName} or {rightLinkName}.");
            }
        }

        //// Set arms to rest pose
        //SetArmImmediate(_leftArm, RestPose);
        //SetArmImmediate(_rightArm, RestPose);

        // Test: Move Link1 to -45 and Link6 to -70
        //float[] testLeftAngles = { -45f, -90f, 0f, 85f, 0f, -70f, 0f };
        //float[] testRightAngles = { -45f, -90f, 0f, 85f, 0f, -70f, 0f };
        //MoveBothArms(testLeftAngles, testRightAngles);
    }

    /// <summary>
    /// Smoothly moves one arm to the given joint angles (degrees, 7 values).
    /// </summary>
    public void MoveArm(ArmSide side, float[] targetAngles, Action onComplete = null)
    {
        if (targetAngles.Length != 7)
        {
            Debug.LogWarning("[RobotArmController] targetAngles must have exactly 7 values.");
            return;
        }

        if (side == ArmSide.Left)
        {
            if (_leftArmRoutine != null) StopCoroutine(_leftArmRoutine);
            _leftArmRoutine = StartCoroutine(DriveArm(_leftArm, targetAngles, onComplete));
        }
        else
        {
            if (_rightArmRoutine != null) StopCoroutine(_rightArmRoutine);
            _rightArmRoutine = StartCoroutine(DriveArm(_rightArm, targetAngles, onComplete));
        }
    }

    /// <summary>
    /// Smoothly moves both arms simultaneously to their respective target joint angles (degrees, 7 values each).
    /// onComplete is called once both arms have finished moving.
    /// </summary>
    public void MoveBothArms(float[] leftTargetAngles, float[] rightTargetAngles, Action onComplete = null)
    {
        if (leftTargetAngles.Length != 7 || rightTargetAngles.Length != 7)
        {
            Debug.LogWarning("[RobotArmController] Both targetAngles arrays must have exactly 7 values.");
            return;
        }

        if (_leftArmRoutine  != null) StopCoroutine(_leftArmRoutine);
        if (_rightArmRoutine != null) StopCoroutine(_rightArmRoutine);

        int remaining = 2;
        void OnOneDone() { if (--remaining == 0) onComplete?.Invoke(); }

        _leftArmRoutine  = StartCoroutine(DriveArm(_leftArm,  leftTargetAngles,  OnOneDone));
        _rightArmRoutine = StartCoroutine(DriveArm(_rightArm, rightTargetAngles, OnOneDone));
    }

    private IEnumerator DriveArm(ArticulationBody[] arm, float[] targetAngles, Action onComplete = null)
    {
        float[] startAngles = GetDriveTargets(arm);

        float maxDelta = 0f;
        for (int i = 0; i < arm.Length; i++)
            maxDelta = Mathf.Max(maxDelta, Mathf.Abs(targetAngles[i] - startAngles[i]));

        if (maxDelta < 0.01f) yield break;

        float duration = maxDelta / jointSpeed;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < arm.Length; i++)
                SetJointTarget(arm[i], Mathf.Lerp(startAngles[i], targetAngles[i], t));

            yield return null;
        }

        for (int i = 0; i < arm.Length; i++)
            SetJointTarget(arm[i], targetAngles[i]);

        onComplete?.Invoke();
    }

    private float[] GetDriveTargets(ArticulationBody[] arm)
    {
        float[] angles = new float[arm.Length];
        for (int i = 0; i < arm.Length; i++)
        {
            angles[i] = arm[i].xDrive.target;
        }
        return angles;
    }

    private void SetJointTarget(ArticulationBody joint, float angleDegrees)
    {
        ArticulationDrive drive = joint.xDrive;
        drive.target = angleDegrees;
        joint.xDrive = drive;
    }

    private void SetArmImmediate(ArticulationBody[] arm, float[] angles)
    {
        for (int i = 0; i < arm.Length; i++)
            SetJointTarget(arm[i], angles[i]);
    }
}
