// TrayController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TrayController : MonoBehaviour
{
    [Header("Sockets on Tray")]
    public List<XRSocketInteractor> dishSockets = new List<XRSocketInteractor>();

    public List<ItemType> GetCurrentDishTypes()
    {
        List<ItemType> placedDishes = new List<ItemType>();

        foreach (var socket in dishSockets)
        {
            if (socket != null && socket.hasSelection)
            {
                var interactable = socket.interactablesSelected[0];
                DishItem dish = interactable.transform.GetComponent<DishItem>()
                             ?? interactable.transform.GetComponentInChildren<DishItem>()
                             ?? interactable.transform.GetComponentInParent<DishItem>();
                if (dish != null)
                {
                    placedDishes.Add(dish.itemType);
                }
            }
        }
        return placedDishes;
    }
}