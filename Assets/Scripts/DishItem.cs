using UnityEngine;

public enum ItemType
{
    //Burger,
    //Espresso,
    Donut,
    
    Croissant,
    //Flatbread,
    //Water,
    Schokobrotchen,
    //Milch,
    //Latte,
    //CinnamonRoll,
    Reis,
    Pasta,
    Kartoffeln,
    Lachs,
    Schnitzel,
    Steak,
    Brokkoli,
    Karotten,
    Blumenkohl,
    Cappuccino,
    Kaffee,
    Tee
    // Add any additional cafe items here
}

public class DishItem : MonoBehaviour
{
    public ItemType itemType;
}