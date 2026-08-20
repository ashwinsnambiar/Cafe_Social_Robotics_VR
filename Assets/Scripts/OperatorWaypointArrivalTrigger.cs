using UnityEngine;

public class OperatorWaypointArrivalTrigger : MonoBehaviour
{
    public DeliveryRobot deliveryRobot;
    public Transform pickupWaypoint;

    public float triggerDistance = 0.6f;
    public float resetDistance = 1.0f;

    private bool isInsideTriggerZone = false;

    private void Update()
    {
        if (deliveryRobot == null || pickupWaypoint == null)
            return;

        Vector3 robotPos = transform.position;
        Vector3 waypointPos = pickupWaypoint.position;

        robotPos.y = 0f;
        waypointPos.y = 0f;

        float distance = Vector3.Distance(robotPos, waypointPos);

        if (!isInsideTriggerZone && distance <= triggerDistance)
        {
            isInsideTriggerZone = true;
            deliveryRobot.BeginOperatorPickup();
            Debug.Log("Operator reached pickup waypoint. Delivery pickup armed.");
        }

        if (isInsideTriggerZone && distance >= resetDistance)
        {
            isInsideTriggerZone = false;
        }
    }
}