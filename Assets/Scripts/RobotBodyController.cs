using System;
using System.Collections;
using UnityEngine;

public class RobotBodyController : MonoBehaviour
{
    [Header("Settings")]
    public float revoluteJointSpeed = 45f; // degrees per second
    public float linearJointSpeed = 1f; // metre per second

    [Header("Body/Head Links")]
    public ArticulationBody link_up_down_body;
    public ArticulationBody link_pitch_body;
    public ArticulationBody link_yaw_head;

    private Coroutine _bodyRoutine;
    public bool IsMoving { get; private set; }

    void Start()
    {


    }

    /// <summary>
    /// Smoothly moves the body and head to the specified angles.
    /// onComplete is called once the movement has finished.
    /// </summary>
    public void MoveBodyAndHead(float upDownAngle, float pitchAngle, float yawAngle, Action onComplete = null)
    {
        if (_bodyRoutine != null) StopCoroutine(_bodyRoutine);
        IsMoving = true;
        _bodyRoutine = StartCoroutine(DriveBodyAndHead(upDownAngle, pitchAngle, yawAngle, onComplete));
    }

    private IEnumerator DriveBodyAndHead(float upDownAngle, float pitchAngle, float yawAngle, Action onComplete = null)
    {
        float startUpDown = link_up_down_body.zDrive.target;
        float startPitch = link_pitch_body.xDrive.target;
        float startYaw = link_yaw_head.xDrive.target;

        float maxDelta = Mathf.Max(
            Mathf.Abs(upDownAngle - startUpDown),
            Mathf.Abs(pitchAngle - startPitch),
            Mathf.Abs(yawAngle - startYaw)
        );

        if (maxDelta < 0.01f)
        {
            IsMoving = false; // Reset if skipping
            onComplete?.Invoke();
            yield break;
        }

        float durationUpDown = Mathf.Abs(upDownAngle - startUpDown) / linearJointSpeed;
        float durationPitch = Mathf.Abs(pitchAngle - startPitch) / revoluteJointSpeed;
        float durationYaw = Mathf.Abs(yawAngle - startYaw) / revoluteJointSpeed;

        float duration = Mathf.Max(durationUpDown, durationPitch, durationYaw);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            SetJointTarget(link_up_down_body, Mathf.Lerp(startUpDown, upDownAngle, t), true);
            SetJointTarget(link_pitch_body, Mathf.Lerp(startPitch, pitchAngle, t));
            SetJointTarget(link_yaw_head, Mathf.Lerp(startYaw, yawAngle, t));

            yield return null;
        }

        SetJointTarget(link_up_down_body, upDownAngle, true);
        SetJointTarget(link_pitch_body, pitchAngle);
        SetJointTarget(link_yaw_head, yawAngle);
        
        IsMoving = false;
        _bodyRoutine = null;
        onComplete?.Invoke();
    }

    private void SetJointTarget(ArticulationBody joint, float value, bool isTranslation = false)
    {
        ArticulationDrive drive = isTranslation ? joint.zDrive : joint.xDrive;
        drive.target = value;

        if (isTranslation)
        {
            joint.zDrive = drive;
        }
        else
        {
            joint.xDrive = drive;
        }
    }

}