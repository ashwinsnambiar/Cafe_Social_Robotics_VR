using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DeliveryRobot : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform barPoint;
    public Transform tablePoint;

    [Header("Controllers")]
    [SerializeField] private RobotArmController armController;
    [SerializeField] private RobotBodyController bodyController;

    [Header("Arm Poses")]
    [SerializeField] private float[] carryPose = { -45f, -90f, 0f, 85f, 0f, -60f, 0f };

    private NavMeshAgent agent;
    private Transform currentTarget;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;

        StartCoroutine(InitAndGoToBar());
    }

    private IEnumerator InitAndGoToBar()
    {
        bool armsReady = false;
        bool bodyReady = false;

        float[] restPose = { 0f, -90f, 0f, 85f, 0f, 0f, 0f };
        armController.MoveBothArms(restPose, restPose, () => armsReady = true);
        bodyController.MoveBodyAndHead(0.55f, 0f, 0f,   () => bodyReady = true);

        yield return new WaitUntil(() => armsReady && bodyReady);

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

    // Called by GripperSocketController once the tray is secured
    public void OnTraySecured()
    {
        StartCoroutine(PrepareAndGoToTable());
    }

    private IEnumerator PrepareAndGoToTable()
    {
        bool armsReady = false;
        armController.MoveBothArms(carryPose, carryPose, () => armsReady = true);
        yield return new WaitUntil(() => armsReady);

        GoToTable();
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
