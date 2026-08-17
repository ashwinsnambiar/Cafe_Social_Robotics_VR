using UnityEngine;

public enum ItemType
{
    Burger,
    Espresso,
    Donut,
    Cappuccino,
    Croissant,
    Flatbread,
    Water,
    Schokobrotchen,
    Milch,
    Latte,
    CinnamonRoll,
    // Add any additional cafe items here
}

public class DishItem : MonoBehaviour
{
    public ItemType itemType;
}