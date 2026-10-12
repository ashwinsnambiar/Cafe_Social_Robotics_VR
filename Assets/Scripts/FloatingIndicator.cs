using UnityEngine;

public class FloatingIndicator : MonoBehaviour
{
    public float floatSpeed = 2f;
    public float floatHeight = 0.1f;
    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Makes the object bob up and down based on a sine wave
        float newY = startPos.y + (Mathf.Sin(Time.time * floatSpeed) * floatHeight);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // Optional: Make it always face the VR Camera (Billboard effect)
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
        }
    }
}