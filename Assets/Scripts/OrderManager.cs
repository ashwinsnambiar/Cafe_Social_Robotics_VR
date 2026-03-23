using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class OrderManager : MonoBehaviour
{
    public GameObject orderPrefab; // Your order UI asset (with TMP and a Button)
    public Transform contentPanel; // The Panel with Vertical Layout Group
    public float spawnInterval = 10f; // Time between orders


    // Preset order list
    private List<string> presetOrders = new List<string>
    {
        "Burger - 1x\nCola - 1x",
        "Burger - 2x\nCola - 2x",
        "Croissant - 2x\nCoffee - 2x",
        "Flatbread - 2x\nTea - 2x",
        "Burger - 1x\nNuggets - 4x"
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

        // 2. Setup the text (e.g., "Order #1:\nBurger - 1x\nCola - 1x")
        TMP_Text orderText = newOrder.GetComponentInChildren<TMP_Text>();
        if (orderText != null)
        {
            string formattedOrder = $"Order #{currentOrderIndex + 1}:\n" + presetOrders[currentOrderIndex];
            orderText.text = formattedOrder;
        }

        // 3. Link the button to remove the order
        UnityEngine.UI.Button btn = newOrder.GetComponentInChildren<UnityEngine.UI.Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => CompleteOrder(newOrder));
        }

        currentOrderIndex++;
    }

    public void CompleteOrder(GameObject orderObj)
    {
        // Logic for finishing the order (play sound, add score, etc.)
        Destroy(orderObj);
    }
}
