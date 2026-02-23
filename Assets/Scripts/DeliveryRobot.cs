using UnityEngine;
using UnityEngine.AI;

public class DeliveryRobot : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform barPoint;
    public Transform tablePoint;

    private NavMeshAgent agent;
    private Transform currentTarget;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        
        // Start by going to the bar to get the tray
        GoToBar();
    }

    // Update is called once per frame
    void Update()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // Rotate towards target rotation
            if (currentTarget != null)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, currentTarget.rotation, agent.angularSpeed * Time.deltaTime);
            }
        }
        else
        {
            // While moving, rotate towards velocity direction
            if (agent.velocity.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(agent.velocity.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, agent.angularSpeed * Time.deltaTime);
            }
        }
    }

    // Call this when the Tray is placed on the robot
    public void GoToTable()
    {
        currentTarget = tablePoint;
        agent.SetDestination(tablePoint.position);
    }

    // Call this to reset the robot
    public void GoToBar()
    {
        currentTarget = barPoint;
        agent.SetDestination(barPoint.position);
    }
}
