using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class InventoryNetworkBehaviour : NetworkBehaviour
{
    // each item type has an integer index associated with it, and this variable tells us how much of each
    //  item is in the inventory.
    public NetworkVariable<NetworkSerializableIntArray> m_inventory;

    private NetworkVariable<int> m_numOccupiedSlots;
    
    public NetworkVariable<int> m_maxInventorySlots;

    private NetworkVariable<int> m_numItems;
    void Awake()
    {
        m_inventory = new NetworkVariable<NetworkSerializableIntArray>();
        m_numOccupiedSlots = new NetworkVariable<int>();
        m_maxInventorySlots = new NetworkVariable<int>();
        m_numItems = new NetworkVariable<int>();
    }

    public List<int> GetInventory()
    {
        return new List<int>(m_inventory.Value.arr);
    }
    
    public void SetMaxInventorySlots(int maxInventorySlots)
    {
        m_maxInventorySlots.Value = maxInventorySlots;
    }

    public void InitializeEmpty(int numItems)
    {
        m_numItems.Value = numItems;
        m_inventory.Value = new NetworkSerializableIntArray();
        m_inventory.Value.arr = new int[numItems];
        for (int i = 0; i < m_numItems.Value; i++)
        {
            m_inventory.Value.arr[i] = 0;
        }

        m_numOccupiedSlots.Value = 0;
    }

    public bool AddItem(int itemIndex)
    {
        if (m_inventory.Value.arr[itemIndex] > 0)
        {
            m_inventory.Value.arr[itemIndex]++;
            return true;
        }
        else if (m_numOccupiedSlots != m_maxInventorySlots)
        {
            m_numOccupiedSlots.Value++;
            m_inventory.Value.arr[itemIndex]++;
            return true;
        }
        
        return false;
    }

    public void RemoveItem(int itemIndex)
    {
        if (m_inventory.Value.arr[itemIndex] == 0)
        {
            Debug.LogError($"Tried to remove an item from inventory when there aren't any left. Item index: {itemIndex}");
        }
        m_inventory.Value.arr[itemIndex]--;

        if (m_inventory.Value.arr[itemIndex] == 0)
        {
            m_numOccupiedSlots.Value--;
        }
        
    }
}
