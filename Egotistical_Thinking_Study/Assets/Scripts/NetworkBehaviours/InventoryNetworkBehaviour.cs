using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class InventoryNetworkBehaviour : NetworkBehaviour
{
    // each item type has an integer index associated with it, and this variable tells us how much of each
    //  item is in the inventory.
    public List<int> m_inventory;

    private int m_numItems = 0;
    void Awake()
    {
        m_inventory = new List<int>();
    }

    public void InitializeEmpty(int numItems)
    {
        m_numItems = numItems;
        for (int i = 0; i < m_numItems; i++)
        {
            m_inventory.Add(0);
        }
    }

    public void AddItem(int itemIndex)
    {
        m_inventory[itemIndex] += 1;
    }

    public void RemoveItem(int itemIndex)
    {
        if (m_inventory[itemIndex] > 0)
        {
            Debug.LogError($"Tried to remove an item from inventory when there aren't any left. Item index: {itemIndex}");
        }
        m_inventory[itemIndex] -= 1;
    }
}
