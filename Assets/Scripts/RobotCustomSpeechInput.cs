using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class RobotCustomSpeechInput : MonoBehaviour
{
    [Header("References")]
    public RobotSpeech speech;
    public GameObject inputRoot;
    public TMP_InputField inputField;

    [Header("Keys")]
    public Key openKey = Key.T;

    public bool IsTyping { get; private set; }

    private void Awake()
    {
        CloseInput();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!IsTyping && Keyboard.current[openKey].wasPressedThisFrame)
        {
            OpenInput();
            return;
        }

        if (!IsTyping)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseInput();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            SubmitText();
        }
    }

    public void OpenInput()
    {
        IsTyping = true;

        if (inputRoot != null)
            inputRoot.SetActive(true);

        if (inputField != null)
        {
            inputField.text = "";
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    public void CloseInput()
    {
        IsTyping = false;

        if (inputRoot != null)
            inputRoot.SetActive(false);
    }

    public void SubmitText()
    {
        if (inputField == null)
            return;

        string text = inputField.text.Trim();

        if (!string.IsNullOrEmpty(text))
        {
            if (speech != null)
                speech.Say(text);
            else
                Debug.LogWarning("RobotCustomSpeechInput: No RobotSpeech assigned.");
        }

        CloseInput();
    }
}