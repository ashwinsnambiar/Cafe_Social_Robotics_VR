using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class RobotKeyboardOperator : MonoBehaviour
{
    [Header("Movement")]
    public NavMeshAgent agent;
    public float manualMoveSpeed = 1.2f;
    public float manualTurnSpeed = 90f;

    [Header("Robot References")]
    public RobotSpeech speech;
    public RobotArmController armController;
    public RobotBodyController bodyController;
    public RobotCustomSpeechInput customSpeechInput;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        // Important: we manually rotate the robot with the keyboard.
        if (agent != null)
            agent.updateRotation = false;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (customSpeechInput != null && customSpeechInput.IsTyping)
            return;

        HandleMovementAndRotation();
        HandleSpeechKeys();
        HandleActionKeys();
    }

    private void HandleMovementAndRotation()
    {
        if (agent == null)
            return;

        Vector3 move = Vector3.zero;

        // Forward / backward
        if (Keyboard.current.iKey.isPressed)
            move += transform.forward;

        if (Keyboard.current.kKey.isPressed)
            move -= transform.forward;

        // Rotate left / right
        float turn = 0f;

        if (Keyboard.current.jKey.isPressed)
            turn -= 1f;

        if (Keyboard.current.lKey.isPressed)
            turn += 1f;

        if (move.sqrMagnitude > 0.001f)
        {
            agent.ResetPath();
            agent.Move(move.normalized * manualMoveSpeed * Time.deltaTime);
        }

        if (Mathf.Abs(turn) > 0.001f)
        {
            agent.ResetPath();
            transform.Rotate(Vector3.up, turn * manualTurnSpeed * Time.deltaTime);
        }
    }

    private void HandleSpeechKeys()
    {
        if (speech == null)
        {
            Debug.LogWarning("No RobotSpeech assigned.");
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            speech.Say(0);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            speech.Say(1);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            speech.Say(2);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            speech.Say(3);
    }

    private void HandleActionKeys()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("Robot action triggered.");
        }
    }

    private void Say(string text)
    {
        if (speech != null)
            speech.Say(text);
        else
            Debug.LogWarning("No RobotSpeech assigned. Text was: " + text);
    }
}