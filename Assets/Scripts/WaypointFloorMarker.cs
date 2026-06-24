using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaypointFloorMarker : MonoBehaviour
{
    public float radius = 0.45f;
    public float yOffset = 0.03f;
    public int segments = 64;
    public float lineWidth = 0.04f;
    public Color color = Color.green;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();

        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = segments;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        // Simple material so it is visible.
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;

        DrawRing();
    }

    private void OnValidate()
    {
        if (line == null)
            line = GetComponent<LineRenderer>();

        if (line != null)
        {
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = segments;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = color;
            line.endColor = color;

            DrawRing();
        }
    }

    private void DrawRing()
    {
        if (line == null)
            return;

        for (int i = 0; i < segments; i++)
        {
            float angle = ((float)i / segments) * Mathf.PI * 2f;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            line.SetPosition(i, new Vector3(x, yOffset, z));
        }
    }
}