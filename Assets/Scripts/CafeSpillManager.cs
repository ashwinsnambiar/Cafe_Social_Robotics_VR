using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 1. Added the new Input System namespace

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
    public Key triggerKey = Key.Space; // 2. Changed from KeyCode to Key

    private bool hasSpilled = false;

    void Update()
    {
        // 3. Updated the input check for the New Input System
        if (Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame && !hasSpilled && availableCups.Count > 0)
        {
            StartCoroutine(TriggerRandomSpill());
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
        Instantiate(selectedTarget.spilledCupPrefab, spillPosition, spillRotation);

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
            Instantiate(vrIndicatorPrefab, indicatorPos, Quaternion.identity);
        }
    }
}