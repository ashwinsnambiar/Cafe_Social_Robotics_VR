using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class OrderManager : MonoBehaviour
{
    public GameObject orderPrefab; // Your order UI asset (with TMP and a Button)
    public Transform contentPanel; // The Panel with Vertical Layout Group
    public float spawnInterval = 10f; // Time between orders

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip newOrderSound;
    public AudioClip completeOrderSound;


    // Preset order list
    private List<string> presetOrders = new List<string>
    {
        "Burger - 1x\nEspresso - 1x",
        "Donut - 2x\nCappuccino - 1x",
        "Croissant - 2x\nCappuccino - 1x",
        "Flatbread - 2x\nWater - 1x",
        "Schokobrotchen - 2x\nMilch - 1x"
        // Add more orders as needed
    };
    private int currentOrderIndex = 0;

    void Start()
    {
        // This loops through any cards you manually placed in the editor and deletes them
        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        StartCoroutine(SpawnOrdersRoutine());
    }

    IEnumerator SpawnOrdersRoutine()
    {
        while (currentOrderIndex < presetOrders.Count)
        {
            yield return new WaitForSeconds(spawnInterval);
            CreateNewOrder();
        }
    }

    void CreateNewOrder()
    {
        if (currentOrderIndex >= presetOrders.Count)
            return;

        // 1. Instantiate the prefab
        GameObject newOrder = Instantiate(orderPrefab, contentPanel);

        // Play "New Order" sound
        if (audioSource && newOrderSound)
            audioSource.PlayOneShot(newOrderSound);

        // 2. Setup the text (e.g., "Order #1:\nBurger - 1x\nCola - 1x")
        TMP_Text orderText = newOrder.GetComponentInChildren<TMP_Text>();
        if (orderText != null)
        {
            string formattedOrder = $"Order #{currentOrderIndex + 1}:\n" + presetOrders[currentOrderIndex];
            orderText.text = formattedOrder;
        }

        // 3. Link the button to remove the order
        Button btn = newOrder.GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => CompleteOrder(newOrder));
        }

        // Force UI Refresh to prevent overlaps
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel.GetComponent<RectTransform>());

        currentOrderIndex++;
    }

    public void CompleteOrder(GameObject orderObj)
    {
        // Play "Order Complete" sound
        if (audioSource && completeOrderSound)
            audioSource.PlayOneShot(completeOrderSound);

        // Logic for finishing the order (play sound, add score, etc.)
        Destroy(orderObj);
    }
}
