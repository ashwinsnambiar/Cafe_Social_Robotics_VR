using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;

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

    private static readonly Regex TableNumberRegex = new Regex(@"\btable(?:\s+number)?\s*(\d+|zero|one|two|three|four|five|six|seven|eight|nine|ten|to|too)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AnyNumberRegex = new Regex(@"\b(\d+|zero|one|two|three|four|five|six|seven|eight|nine|ten)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, int> NumberWordMap = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "zero", 0 },
        { "one", 1 },
        { "two", 2 },
        { "to", 2 },
        { "too", 2 },
        { "three", 3 },
        { "four", 4 },
        { "five", 5 },
        { "six", 6 },
        { "seven", 7 },
        { "eight", 8 },
        { "nine", 9 },
        { "ten", 10 }
    };

    private NavMeshAgent agent;
    private Transform currentTarget;
    private bool canAcceptTableSelection;
    private int pendingVoiceTableIndex = -1;

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
        canAcceptTableSelection = true;

        if (pendingVoiceTableIndex >= 0)
        {
            int queuedIndex = pendingVoiceTableIndex;
            pendingVoiceTableIndex = -1;
            SelectTable(queuedIndex);
            return;
        }

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
        canAcceptTableSelection = false;
        pendingVoiceTableIndex = -1;
        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);
    }

    public void ProcessVoiceCommand(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return;

        if (!TryExtractSpokenTableNumber(rawText, out int spokenTableNumber))
            return;

        int tableIndex = ResolveTableIndex(spokenTableNumber);
        if (tableIndex < 0)
            return;

        if (canAcceptTableSelection)
        {
            SelectTable(tableIndex);
            return;
        }

        pendingVoiceTableIndex = tableIndex;
    }

    // Called by UI Buttons (On Click events)
    public void SelectTable(int tableIndex)
    {
        if (!canAcceptTableSelection)
        {
            Debug.LogWarning("Ignoring table selection because tray is not secured yet.");
            return;
        }

        if (tableIndex < 0 || tableIndex >= tables.Length)
        {
            Debug.LogError("Invalid Table Index!");
            return;
        }

        canAcceptTableSelection = false;

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
        canAcceptTableSelection = false;
        pendingVoiceTableIndex = -1;
        if (tableSelectionCanvas != null) tableSelectionCanvas.SetActive(false);

        currentTarget = barPoint;
        agent.SetDestination(barPoint.position);
    }

    private bool TryExtractSpokenTableNumber(string rawText, out int tableNumber)
    {
        var tableMatch = TableNumberRegex.Match(rawText);
        if (tableMatch.Success && TryParseSpokenNumber(tableMatch.Groups[1].Value, out tableNumber))
            return true;

        var anyNumberMatch = AnyNumberRegex.Match(rawText);
        if (anyNumberMatch.Success && TryParseSpokenNumber(anyNumberMatch.Groups[1].Value, out tableNumber))
            return true;

        tableNumber = -1;
        return false;
    }

    private bool TryParseSpokenNumber(string token, out int value)
    {
        token = token.Trim().ToLowerInvariant();

        if (int.TryParse(token, out value))
            return true;

        return NumberWordMap.TryGetValue(token, out value);
    }

    private int ResolveTableIndex(int spokenTableNumber)
    {
        for (int i = 0; i < tables.Length; i++)
        {
            if (ParseTableIndex(tables[i].name) == spokenTableNumber)
                return i;
        }

        int oneBasedIndex = spokenTableNumber - 1;
        if (oneBasedIndex >= 0 && oneBasedIndex < tables.Length)
            return oneBasedIndex;

        if (spokenTableNumber >= 0 && spokenTableNumber < tables.Length)
            return spokenTableNumber;

        return -1;
    }
}
