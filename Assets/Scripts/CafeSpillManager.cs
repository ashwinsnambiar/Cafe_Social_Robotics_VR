using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[System.Serializable]
public class CoffeeTarget
{
    public GameObject filledCup;
    public GameObject spilledCupPrefab;
}

public class CafeSpillManager : MonoBehaviour
{
    [Header("Study Targets")]
    public List<CoffeeTarget> availableCups = new List<CoffeeTarget>();

    [Header("VR Operator Visuals")]
    public GameObject vrIndicatorPrefab;
    public float indicatorHeightOffset = 0.5f;

    [Header("Audio Setup")]
    public AudioClip spillSound;
    public AudioClip npcShoutSound;

    [Header("Trigger Settings")]
    [Tooltip("The keyboard key to trigger the spill")]
    public Key triggerKey = Key.Space;

    [Header("Robot Integration")]
    [Tooltip("Fired after the spill is fully set up (cup + indicator spawned). Passes the spilled cup's Transform.")]
    public UnityEvent<Transform> onSpillOccurred;

    private bool hasSpilled = false;
    private bool isLocked = false;

    /// <summary>The instantiated spilled cup object (set after TriggerRandomSpill).</summary>
    public GameObject LastSpilledCupInstance { get; private set; }

    /// <summary>The instantiated VR indicator / exclamation mark (set after TriggerRandomSpill).</summary>
    public GameObject LastVrIndicatorInstance { get; private set; }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            if (hasSpilled)
            {
                Debug.LogWarning($"CafeSpillManager: Key {triggerKey} pressed, but spill has already occurred.");
            }
            else if (isLocked)
            {
                Debug.LogWarning($"CafeSpillManager: Key {triggerKey} pressed, but event is currently locked by RobotTaskScheduler.");
            }
            else if (availableCups.Count == 0)
            {
                Debug.LogWarning($"CafeSpillManager: Key {triggerKey} pressed, but no cups are in availableCups list.");
            }
            else
            {
                Debug.Log($"CafeSpillManager: Key {triggerKey} pressed! Triggering random coffee spill...");
                StartCoroutine(TriggerRandomSpill());
            }
        }
    }

    public System.Collections.IEnumerator TriggerRandomSpill()
    {
        hasSpilled = true;

        int randomIndex = Random.Range(0, availableCups.Count);
        CoffeeTarget selectedTarget = availableCups[randomIndex];

        if (selectedTarget.filledCup == null || selectedTarget.spilledCupPrefab == null)
        {
            Debug.LogError("A cup or prefab is missing from the list!");
            yield break;
        }

        Vector3 spillPosition = selectedTarget.filledCup.transform.position;
        Quaternion spillRotation = selectedTarget.filledCup.transform.rotation;

        selectedTarget.filledCup.SetActive(false);
        LastSpilledCupInstance = Instantiate(selectedTarget.spilledCupPrefab, spillPosition, spillRotation);

        if (spillSound != null)
        {
            AudioSource.PlayClipAtPoint(spillSound, spillPosition);
        }
        if (npcShoutSound != null)
        {
            AudioSource.PlayClipAtPoint(npcShoutSound, spillPosition);
        }

        yield return new WaitForSeconds(1f);

        if (vrIndicatorPrefab != null)
        {
            Vector3 indicatorPos = spillPosition + new Vector3(0, indicatorHeightOffset, 0);
            LastVrIndicatorInstance = Instantiate(vrIndicatorPrefab, indicatorPos, Quaternion.identity);
        }

        // Notify robot system that a spill has occurred and is ready for cleanup
        onSpillOccurred?.Invoke(LastSpilledCupInstance.transform);
    }

    /// <summary>Prevents this spill event from being triggered (called by RobotTaskScheduler).</summary>
    public void Lock() { isLocked = true; }

    /// <summary>Re-enables this spill event after cleanup is complete.</summary>
    public void Unlock() 
    { 
        isLocked = false; 
        hasSpilled = false;
    }
}