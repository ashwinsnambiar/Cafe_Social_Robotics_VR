using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class CoffeeMachineManager : MonoBehaviour
{
    [Header("VR Setup")]
    [Tooltip("The socket where the empty cup is placed.")]
    public XRSocketInteractor cupSocket;

    [Header("Drink Prefabs")]
    public GameObject espressoPrefab;
    public GameObject lattePrefab;
    public GameObject cappucinoPrefab;
    public GameObject milkPrefab;
    public GameObject waterPrefab;
    // Add more as needed

    [Header("Effects (Optional for later)")]
    public float brewTime = 3.0f;
    public AudioSource brewSound;
    public ParticleSystem steamEffect;

    // Called by the VR buttons
    public void StartBrewing(string drinkType)
    {
        Debug.Log("Starting brewing");
        // 1. Check if there is an item in the socket
        IXRSelectInteractable currentItem = cupSocket.GetOldestInteractableSelected();

        if (currentItem != null && currentItem.transform.CompareTag("EmptyCup"))
        {
            // Start the delay sequence
            StartCoroutine(BrewRoutine(currentItem.transform.gameObject, drinkType));
        }
        else
        {
            Debug.Log("Waiting for an empty cup!");
        }
    }

    private IEnumerator BrewRoutine(GameObject emptyCup, string drinkType)
    {
        // Play effects if you have them assigned
        if (brewSound) brewSound.Play();
        if (steamEffect) steamEffect.Play();

        // Wait for the coffee to "brew"
        yield return new WaitForSeconds(brewTime);

        // Determine which prefab to spawn based on the button pressed
        GameObject prefabToSpawn = null;
        switch (drinkType)
        {
            case "Espresso": prefabToSpawn = espressoPrefab; break;
            case "Latte": prefabToSpawn = lattePrefab; break;
            case "Cappucino": prefabToSpawn = cappucinoPrefab; break;
            case "Milk": prefabToSpawn = milkPrefab; break;
            case "Water": prefabToSpawn = waterPrefab; break;
        }

        if (prefabToSpawn != null)
        {
            // Spawn the new filled cup exactly where the empty one is
            Instantiate(prefabToSpawn, emptyCup.transform.position, emptyCup.transform.rotation);

            // Destroy the empty cup so it is "replaced"
            Destroy(emptyCup);
        }

        // Stop effects
        if (steamEffect) steamEffect.Stop();
    }
}