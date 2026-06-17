using System.Collections;
using UnityEngine;
using TMPro;

public class RobotSpeech : MonoBehaviour
{
    [System.Serializable]
    public class SpeechLine
    {
        [TextArea(2, 4)]
        public string text;

        public AudioClip audioClip;
    }

    [Header("Speech Bubble")]
    public GameObject bubbleRoot;
    public TextMeshProUGUI bubbleText;
    public float visibleSeconds = 4f;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Speech Lines")]
    public SpeechLine[] speechLines;

    [Header("Runtime TTS")]
    public RuntimeTtsClient runtimeTtsClient;
    public bool useRuntimeTtsForMissingClips = true;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);

        if (runtimeTtsClient == null)
            runtimeTtsClient = GetComponent<RuntimeTtsClient>();

    }

    public void Say(int index)
    {
        if (speechLines == null || index < 0 || index >= speechLines.Length)
        {
            Debug.LogWarning("Speech index not found: " + index);
            return;
        }

        Say(speechLines[index].text, speechLines[index].audioClip);
    }

    public void Say(string text)
    {
        Say(text, null);
    }

    public void Say(string text, AudioClip clip)
    {
        ShowBubble(text);

        if (clip != null)
        {
            PlayAudio(clip);
        }
        else if (useRuntimeTtsForMissingClips && runtimeTtsClient != null)
        {
            StartCoroutine(SpeakWithRuntimeTts(text));
        }
        else
        {
            Debug.LogWarning("RobotSpeech: No audio clip and no runtime TTS client available.");
        }

        Debug.Log("Robot says: " + text);
    }

    private void ShowBubble(string text)
    {
        if (bubbleRoot != null)
            bubbleRoot.SetActive(true);

        if (bubbleText != null)
            bubbleText.text = text;

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    private void PlayAudio(AudioClip clip)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    private IEnumerator SpeakWithRuntimeTts(string text)
    {
        yield return runtimeTtsClient.SynthesizeToClip(text, clip =>
        {
            if (clip != null)
                PlayAudio(clip);
        });
    }

    private IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSeconds(visibleSeconds);

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);
    }
}