using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum WorkloadCondition
{
    LowIntensity,
    HighIntensity
}

public class ExperimentSessionManager : MonoBehaviour
{
    public static ExperimentSessionManager Instance { get; private set; }

    [Header("Subject & Trial Settings")]
    [Tooltip("Unique ID for the participant, used for logging and tracking.")]
    public string subjectId = "Subject_01";

    [Tooltip("Current active order set (0 = Set 1, 1 = Set 2, etc.)")]
    public int currentSetIndex = 0;

    [Tooltip("Save and resume set index across Unity Editor play sessions (Default is false).")]
    public bool saveToPlayerPrefs = false;

    [Header("Workload Condition")]
    [Tooltip("Select LowIntensity (Calorie intake alone) or HighIntensity (Calorie, Sugar, and Fat breakdown).")]
    public WorkloadCondition workloadCondition = WorkloadCondition.LowIntensity;

    [Header("Food Details UI References (Optional - Auto-detected)")]
    [Tooltip("CalorieIntakeAlone TextMeshPro GameObject under FoodDetailsCanvas (auto-found if unassigned).")]
    public GameObject calorieAloneTextObject;

    [Tooltip("NutritionBreakdown TextMeshPro GameObject under FoodDetailsCanvas (auto-found if unassigned).")]
    public GameObject nutritionBreakdownTextObject;

    [Header("Transition & Break Settings")]
    [Tooltip("Duration in seconds for screen to fade to black and fade back in.")]
    public float fadeDuration = 1.0f;

    [Tooltip("Minimum rest duration in seconds in VR between sets.")]
    public float minBreakDuration = 3.0f;

    [Tooltip("Require the participant (VR Trigger) or experimenter (Spacebar) to confirm before starting the next set.")]
    public bool requireInputToProceed = true;

    [Header("Events")]
    public UnityEvent<int> onSetTransitionStarted;
    public UnityEvent onAllSetsFinished;

    private bool isTransitioning = false;
    private bool isWaitingForConfirmation = false;
    private bool userPressedProceed = false;

    private const string PREF_KEY_SET_INDEX = "HRI_CurrentSetIndex";
    private const string PREF_KEY_SUBJECT_ID = "HRI_SubjectId";

    public int CurrentSetIndex
    {
        get => currentSetIndex;
        set
        {
            currentSetIndex = value;
            if (saveToPlayerPrefs) PlayerPrefs.SetInt(PREF_KEY_SET_INDEX, currentSetIndex);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (saveToPlayerPrefs && PlayerPrefs.HasKey(PREF_KEY_SET_INDEX))
            {
                currentSetIndex = PlayerPrefs.GetInt(PREF_KEY_SET_INDEX, currentSetIndex);
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        EnsureLogger();
        SyncAndBindOrderManager();
        EnsureFader();
        ApplyWorkloadCondition();
        VRScreenFader.Instance.FadeIn(fadeDuration);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isTransitioning = false;
        isWaitingForConfirmation = false;
        userPressedProceed = false;

        EnsureLogger();
        SyncAndBindOrderManager();
        EnsureFader();
        ApplyWorkloadCondition();

        // Fade in from black cleanly on new scene load
        VRScreenFader.Instance.FadeIn(fadeDuration);
    }

    public void ApplyWorkloadCondition()
    {
        if (calorieAloneTextObject == null || nutritionBreakdownTextObject == null)
        {
            var foodCanvas = GameObject.Find("FoodDetailsCanvas");
            if (foodCanvas != null)
            {
                foreach (Transform child in foodCanvas.transform)
                {
                    if (child.name.IndexOf("Calorie", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        child.name.IndexOf("Alone", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        calorieAloneTextObject = child.gameObject;
                    }
                    else if (child.name.IndexOf("Nutrition", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                             child.name.IndexOf("Breakdown", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        nutritionBreakdownTextObject = child.gameObject;
                    }
                }
            }
        }

        if (calorieAloneTextObject != null)
        {
            calorieAloneTextObject.SetActive(workloadCondition == WorkloadCondition.LowIntensity);
        }

        if (nutritionBreakdownTextObject != null)
        {
            nutritionBreakdownTextObject.SetActive(workloadCondition == WorkloadCondition.HighIntensity);
        }
    }

    private void EnsureLogger()
    {
        if (TrialLogging.TrialDataLogger.Instance == null)
        {
            GameObject loggerObj = new GameObject("TrialDataLogger");
            var logger = loggerObj.AddComponent<TrialLogging.TrialDataLogger>();
            logger.subjectId = subjectId;
        }
        else
        {
            TrialLogging.TrialDataLogger.Instance.subjectId = subjectId;
            TrialLogging.TrialDataLogger.Instance.SyncSubjectId();
        }
    }

    private void EnsureFader()
    {
        if (VRScreenFader.Instance == null)
        {
            GameObject faderObj = new GameObject("VRScreenFader");
            faderObj.AddComponent<VRScreenFader>();
        }
        else
        {
            VRScreenFader.Instance.EnsureFadeObjects();
        }
    }

    private void SyncAndBindOrderManager()
    {
        if (OrderManager.Instance != null)
        {
            OrderManager.Instance.CurrentSetIndex = currentSetIndex;
            OrderManager.Instance.onSetCompleted.RemoveListener(OnSetCompletedHandler);
            OrderManager.Instance.onSetCompleted.AddListener(OnSetCompletedHandler);
            OrderManager.Instance.onStudyCompleted.RemoveListener(OnStudyCompletedHandler);
            OrderManager.Instance.onStudyCompleted.AddListener(OnStudyCompletedHandler);
        }
    }

    private void Update()
    {
        HandleKeyboardShortcuts();

        if (isWaitingForConfirmation)
        {
            if (CheckVRTriggerPressed())
            {
                userPressedProceed = true;
            }
        }
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null) return;

        // [Space] or [N] -> Confirm proceed during break, or advance set
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.nKey.wasPressedThisFrame)
        {
            if (isWaitingForConfirmation)
            {
                userPressedProceed = true;
            }
        }

        // [Ctrl] + [N] -> Force advance to next set during gameplay
        if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.nKey.wasPressedThisFrame)
        {
            Debug.Log("[Experimenter] Force advancing to next set.");
            AdvanceToNextSet(currentSetIndex + 1);
        }

        // [R] -> Restart current set cleanly
        if (Keyboard.current.rKey.wasPressedThisFrame && !isTransitioning)
        {
            Debug.Log($"[Experimenter] Reloading Set {currentSetIndex + 1}.");
            ReloadCurrentSet();
        }

        // [Backspace] or [Delete] -> Reset subject progress to Set 0 (Set 1)
        if (Keyboard.current.backspaceKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            Debug.Log("[Experimenter] Resetting trial progress to Set 1 (Index 0).");
            ResetProgressToStart();
        }

        // [Ctrl] + [1-9] -> Jump to specific set
        if (Keyboard.current.ctrlKey.isPressed)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 1."); AdvanceToNextSet(0); }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 2."); AdvanceToNextSet(1); }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 3."); AdvanceToNextSet(2); }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 4."); AdvanceToNextSet(3); }
            else if (Keyboard.current.digit5Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 5."); AdvanceToNextSet(4); }
            else if (Keyboard.current.digit6Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 6."); AdvanceToNextSet(5); }
            else if (Keyboard.current.digit7Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 7."); AdvanceToNextSet(6); }
            else if (Keyboard.current.digit8Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 8."); AdvanceToNextSet(7); }
            else if (Keyboard.current.digit9Key.wasPressedThisFrame) { Debug.Log("[Experimenter] Jumping to Set 9."); AdvanceToNextSet(8); }
        }
    }

    private bool CheckVRTriggerPressed()
    {
        var rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rightPressed) && rightPressed)
            return true;

        var leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftHand.isValid && leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool leftPressed) && leftPressed)
            return true;

        return false;
    }

    private void OnSetCompletedHandler(int finishedSetIndex)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToNextSetRoutine(finishedSetIndex));
    }

    private void OnStudyCompletedHandler()
    {
        if (isTransitioning) return;
        StartCoroutine(StudyCompletedRoutine());
    }

    private IEnumerator TransitionToNextSetRoutine(int finishedSetIndex)
    {
        isTransitioning = true;
        onSetTransitionStarted?.Invoke(finishedSetIndex);

        int totalSets = OrderManager.Instance != null ? OrderManager.Instance.orderSets.Count : 5;
        int nextSetNumber = finishedSetIndex + 2;

        // Check if all sets finished
        if (finishedSetIndex + 1 >= totalSets)
        {
            yield return StartCoroutine(StudyCompletedRoutine());
            yield break;
        }

        // 1. Fade to Black
        EnsureFader();
        VRScreenFader.Instance.FadeOut(fadeDuration, $"<b>Set {finishedSetIndex + 1} Complete!</b>\n\nTake a short rest.");

        yield return new WaitForSecondsRealtime(fadeDuration);

        // 2. Minimum Rest Period
        if (minBreakDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(minBreakDuration);
        }

        // 3. Prompt user / experimenter to confirm
        if (requireInputToProceed)
        {
            isWaitingForConfirmation = true;
            userPressedProceed = false;

            VRScreenFader.Instance.SetMessage($"<b>Set {finishedSetIndex + 1} Complete!</b>\n\nPress <b>[Trigger]</b> or <b>[Space]</b> when ready for <b>Set {nextSetNumber}</b>.");

            yield return new WaitUntil(() => userPressedProceed);
            isWaitingForConfirmation = false;
        }

        // 4. Update Set Index and Reload Scene
        CurrentSetIndex = finishedSetIndex + 1;
        VRScreenFader.Instance.SetMessage($"<b>Loading Set {nextSetNumber}...</b>");

        yield return new WaitForSecondsRealtime(0.5f);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator StudyCompletedRoutine()
    {
        isTransitioning = true;
        EnsureFader();
        VRScreenFader.Instance.FadeOut(fadeDuration, "<b>All Trial Sets Completed!</b>\n\nThank you for participating.");

        if (TrialLogging.TrialDataLogger.Instance != null)
        {
            TrialLogging.TrialDataLogger.Instance.OnStudyCompleted();
        }

        onAllSetsFinished?.Invoke();
        yield break;
    }

    public void ReloadCurrentSet()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        EnsureFader();
        VRScreenFader.Instance.FadeOut(0.5f, $"<b>Resetting Set {currentSetIndex + 1}...</b>", () =>
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }

    public void AdvanceToNextSet(int targetSetIndex)
    {
        if (isTransitioning) return;
        isTransitioning = true;
        CurrentSetIndex = targetSetIndex;
        EnsureFader();
        VRScreenFader.Instance.FadeOut(0.5f, $"<b>Loading Set {targetSetIndex + 1}...</b>", () =>
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }

    [ContextMenu("Reset Progress to Set 1 (Index 0)")]
    public void ResetProgressToStart()
    {
        CurrentSetIndex = 0;
        PlayerPrefs.DeleteKey(PREF_KEY_SET_INDEX);
        PlayerPrefs.Save();
        Debug.Log("<color=yellow>[ExperimentSessionManager] Progress reset to Set 1 (Index 0).</color>");
        ReloadCurrentSet();
    }

    [ContextMenu("Open Trial Logs Folder")]
    public void OpenTrialLogsFolder()
    {
        if (TrialLogging.TrialDataLogger.Instance != null)
        {
            TrialLogging.TrialDataLogger.Instance.OpenLogFolder();
        }
        else
        {
            string rootDir = System.IO.Path.Combine(Application.dataPath, "..", "TrialLogs");
            if (System.IO.Directory.Exists(rootDir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = rootDir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }
    }
}
