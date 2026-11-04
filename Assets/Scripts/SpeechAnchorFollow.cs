using UnityEngine;

public class SpeechAnchorFollow : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;

    [Header("Position Offset")]
    public Vector3 offset = new Vector3(0f, 0.4f, 0f);

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
    }
}
