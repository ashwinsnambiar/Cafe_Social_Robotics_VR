using UnityEngine;

public class OperatorCameraFollow : MonoBehaviour
{
    public Transform target;

    public Vector3 offset = new Vector3(0f, 4f, -5f);
    public Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);

    public float followSmoothness = 8f;
    public float rotationSmoothness = 8f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime
        );

        Vector3 lookTarget = target.position + lookOffset;
        Vector3 direction = lookTarget - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothness * Time.deltaTime
            );
        }
    }
}