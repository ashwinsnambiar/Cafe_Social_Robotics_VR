using System.Collections;
using UnityEngine;
using UnityEngine.AI;

using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System.Text.RegularExpressions;

public class DeliveryRobot : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform barPoint;
    public Transform[] tables; // Will be auto-populated

    [Header("UI Elements")]
    public GameObject tableSelectionCanvas; // Drag the popup Canvas here

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

        // Auto-populate tables array with all GameObjects named WaypointTable<number>
        var allTables = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("WaypointTable"))
            .Select(t => new
            {
                Transform = t,
                Index = ParseTableIndex(t.name)
            })
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .Select(x => x.Transform)
            .ToArray();
        tables = allTables;

        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);

        StartCoroutine(InitAndGoToBar());
    }

    private int ParseTableIndex(string name)
    {
        // Extracts the number from "WaypointTable<number>"
        var match = Regex.Match(name, @"WaypointTable(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int idx))
            return idx;
        return -1;
    }

    private IEnumerator InitAndGoToBar()
    {
        bool armsReady = false;
        bool bodyReady = false;

        float[] restPose = { 0f, -90f, 0f, 85f, 0f, 0f, 0f };
        armController.MoveBothArms(restPose, restPose, () => armsReady = true);
        bodyController.MoveBodyAndHead(0.55f, 0f, 0f, () => bodyReady = true);

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
        if (tableSelectionCanvas != null)
        {
            tableSelectionCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Table Selection Canvas is missing! Defaulting to Table 0.");
            SelectTable(0);
        }
    }

    // Optional: If the tray is removed before selecting a table
    public void OnTrayRemoved()
    {
        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);
    }

    // Called by UI Buttons (On Click events)
    public void SelectTable(int tableIndex)
    {
        if (tableIndex < 0 || tableIndex >= tables.Length)
        {
            Debug.LogError("Invalid Table Index!");
            return;
        }

        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);

        StartCoroutine(PrepareAndGoToTable(tableIndex));
    }

    private IEnumerator PrepareAndGoToTable(int tableIndex)
    {
        bool armsReady = false;
        armController.MoveBothArms(carryPose, carryPose, () => armsReady = true);
        yield return new WaitUntil(() => armsReady);

        GoToTable(tableIndex);
    }

    private void GoToTable(int tableIndex)
    {
        currentTarget = tables[tableIndex];
        agent.SetDestination(tables[tableIndex].position);
    }

    // Call this to reset the robot
    public void GoToBar()
    {
        currentTarget = barPoint;
        agent.SetDestination(barPoint.position);
    }
}
