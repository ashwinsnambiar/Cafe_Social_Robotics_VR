using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class VoiceRecorder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonoBehaviour whisperComponent;
    [SerializeField] private DeliveryRobot deliveryRobot;
    [SerializeField] private InputActionReference pushToTalkAction;

    [Header("Recording")]
    [SerializeField] private string microphoneDeviceName = "";
    [SerializeField] private int maxRecordSeconds = 10;
    [SerializeField] private int sampleRate = 16000;

    private AudioClip recordingClip;
    private bool isRecording;
    private string activeDeviceName;

    private MethodInfo transcribeMethod;
    private UnityEvent<string> transcriptionEvent;

    private void Awake()
    {
        if (deliveryRobot == null)
            deliveryRobot = FindFirstObjectByType<DeliveryRobot>();

        CacheTranscribeMethod();
        CacheTranscriptionEvent();

        if (transcribeMethod == null)
            Debug.LogError("VoiceRecorder: Assign the exact RunWhisper component used for robot command routing.");

        if (transcriptionEvent == null)
            Debug.LogWarning("VoiceRecorder: Could not bind to OnTranscriptionResult on whisper component.");

        if (deliveryRobot == null)
            Debug.LogWarning("VoiceRecorder: DeliveryRobot reference is missing.");
    }

    private void OnEnable()
    {
        if (pushToTalkAction == null || pushToTalkAction.action == null)
        {
            Debug.LogWarning("VoiceRecorder is missing Push-To-Talk action reference.");
            return;
        }

        pushToTalkAction.action.started += OnPushToTalkStarted;
        pushToTalkAction.action.canceled += OnPushToTalkCanceled;
        pushToTalkAction.action.Enable();

        RegisterTranscriptionForwarder();
    }

    private void OnDisable()
    {
        if (pushToTalkAction != null && pushToTalkAction.action != null)
        {
            pushToTalkAction.action.started -= OnPushToTalkStarted;
            pushToTalkAction.action.canceled -= OnPushToTalkCanceled;
            pushToTalkAction.action.Disable();
        }

        UnregisterTranscriptionForwarder();

        if (isRecording)
            StopRecordingAndTranscribe();
    }

    private void OnPushToTalkStarted(InputAction.CallbackContext context)
    {
        StartRecording();
    }

    private void OnPushToTalkCanceled(InputAction.CallbackContext context)
    {
        StopRecordingAndTranscribe();
    }

    private void StartRecording()
    {
        if (isRecording)
            return;

        if (!HasValidTranscribeTarget())
        {
            Debug.LogWarning("VoiceRecorder could not find RunWhisper.Transcribe(AudioClip). Assign Whisper Component in Inspector.");
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone devices available.");
            return;
        }

        activeDeviceName = ResolveMicrophoneDevice();
        recordingClip = Microphone.Start(activeDeviceName, false, maxRecordSeconds, sampleRate);

        if (recordingClip == null)
        {
            Debug.LogWarning($"Failed to start microphone recording on device '{activeDeviceName}'.");
            return;
        }

        isRecording = true;
    }

    private void StopRecordingAndTranscribe()
    {
        if (!isRecording)
            return;

        int samplePosition = Microphone.GetPosition(activeDeviceName);
        Microphone.End(activeDeviceName);
        isRecording = false;

        if (recordingClip == null || samplePosition <= 0)
        {
            Debug.LogWarning("No voice data captured.");
            return;
        }

        samplePosition = Mathf.Min(samplePosition, recordingClip.samples);

        float[] samples = new float[samplePosition];
        recordingClip.GetData(samples, 0);

        AudioClip trimmedClip = AudioClip.Create("VoiceCommand", samplePosition, 1, sampleRate, false);
        trimmedClip.SetData(samples, 0);

        try
        {
            transcribeMethod.Invoke(whisperComponent, new object[] { trimmedClip });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"VoiceRecorder failed to invoke Transcribe: {ex.Message}");
        }
    }

    private void OnWhisperTranscription(string text)
    {
        if (deliveryRobot == null)
            return;

        deliveryRobot.ProcessVoiceCommand(text);
    }

    private void RegisterTranscriptionForwarder()
    {
        if (transcriptionEvent == null || deliveryRobot == null)
            return;

        transcriptionEvent.RemoveListener(OnWhisperTranscription);
        transcriptionEvent.AddListener(OnWhisperTranscription);
    }

    private void UnregisterTranscriptionForwarder()
    {
        if (transcriptionEvent == null)
            return;

        transcriptionEvent.RemoveListener(OnWhisperTranscription);
    }

    private bool HasValidTranscribeTarget()
    {
        if (whisperComponent == null)
            return false;

        if (transcribeMethod == null)
            CacheTranscribeMethod();

        return transcribeMethod != null;
    }

    private void CacheTranscribeMethod()
    {
        transcribeMethod = whisperComponent != null
            ? whisperComponent.GetType().GetMethod("Transcribe", BindingFlags.Instance | BindingFlags.Public)
            : null;
    }

    private void CacheTranscriptionEvent()
    {
        transcriptionEvent = null;

        if (whisperComponent == null)
            return;

        var type = whisperComponent.GetType();
        var eventField = type.GetField("OnTranscriptionResult", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (eventField != null)
            transcriptionEvent = eventField.GetValue(whisperComponent) as UnityEvent<string>;
    }

    private string ResolveMicrophoneDevice()
    {
        if (!string.IsNullOrWhiteSpace(microphoneDeviceName))
        {
            foreach (string device in Microphone.devices)
            {
                if (device == microphoneDeviceName)
                    return device;
            }

            Debug.LogWarning($"Requested microphone '{microphoneDeviceName}' not found. Falling back to default device.");
        }

        return null;
    }
}
