using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WaypointFloorMarker : MonoBehaviour
{
    public float radius = 0.5f;
    public float yOffset = 0.03f;
    public Color color = Color.green;
    public string label = "";

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 pos = transform.position + Vector3.up * yOffset;

        Handles.color = color;
        Handles.DrawWireDisc(pos, Vector3.up, radius);

        if (!string.IsNullOrEmpty(label))
        {
            Handles.Label(pos + Vector3.up * 0.15f, label);
        }
    }
#endif
}