using System;
using System.Collections;
using UnityEngine;

public class RobotBodyController : MonoBehaviour
{
    [Header("Settings")]
    public float jointSpeed = 45f; // degrees per second

    private ArticulationBody _linkUpDownBody;
    private ArticulationBody _linkPitchBody;
    private ArticulationBody _linkYawHead;

    private Coroutine _bodyRoutine;

    void Start()
    {
        // Automatically find the articulation bodies
        _linkUpDownBody = FindArticulationBodyRecursive(transform, "link_up_down_body");
        _linkPitchBody = FindArticulationBodyRecursive(transform, "link_pitch_body");
        _linkYawHead = FindArticulationBodyRecursive(transform, "link_yaw_head");

        if (_linkUpDownBody == null || _linkPitchBody == null || _linkYawHead == null)
        {
            Debug.LogError("[RobotBodyController] Failed to find one or more body/head articulation bodies.");
            return;
        }

    }

    /// <summary>
    /// Smoothly moves the body and head to the specified angles.
    /// onComplete is called once the movement has finished.
    /// </summary>
    public void MoveBodyAndHead(float upDownAngle, float pitchAngle, float yawAngle, Action onComplete = null)
    {
        if (_bodyRoutine != null) StopCoroutine(_bodyRoutine);
        _bodyRoutine = StartCoroutine(DriveBodyAndHead(upDownAngle, pitchAngle, yawAngle, onComplete));
    }

    private IEnumerator DriveBodyAndHead(float upDownAngle, float pitchAngle, float yawAngle, Action onComplete = null)
    {
        float startUpDown = _linkUpDownBody.zDrive.target;
        float startPitch = _linkPitchBody.xDrive.target;
        float startYaw = _linkYawHead.xDrive.target;

        float maxDelta = Mathf.Max(
            Mathf.Abs(upDownAngle - startUpDown),
            Mathf.Abs(pitchAngle - startPitch),
            Mathf.Abs(yawAngle - startYaw)
        );

        if (maxDelta < 0.01f) yield break;

        float duration = maxDelta / jointSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            SetJointTarget(_linkUpDownBody, Mathf.Lerp(startUpDown, upDownAngle, t), true);
            SetJointTarget(_linkPitchBody, Mathf.Lerp(startPitch, pitchAngle, t));
            SetJointTarget(_linkYawHead, Mathf.Lerp(startYaw, yawAngle, t));

            yield return null;
        }

        SetJointTarget(_linkUpDownBody, upDownAngle, true);
        SetJointTarget(_linkPitchBody, pitchAngle);
        SetJointTarget(_linkYawHead, yawAngle);

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
}