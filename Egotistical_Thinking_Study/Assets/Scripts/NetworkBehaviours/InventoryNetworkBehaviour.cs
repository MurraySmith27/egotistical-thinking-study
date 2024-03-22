using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public delegate void InventoryUpdatedEvent();
public class InventoryNetworkBehaviour : NetworkBehaviour
{
    public InventoryUpdatedEvent m_inventoryUpdated;
    // each item type has an integer index associated with it, and this variable tells us how much of each
    //  item is in the inventory.
    public NetworkVariable<NetworkSerializableIntArray> m_itemQuantities = new NetworkVariable<NetworkSerializableIntArray>();

    public NetworkVariable<NetworkSerializableIntArray> m_itemPlacements = new NetworkVariable<NetworkSerializableIntArray>();

    private int m_numItemsInInventory {
        get {
            int numTotalItems = 0;
            foreach (int quantity in m_itemQuantities.Value.arr)
            {
                numTotalItems += quantity;
            }

            return numTotalItems;
        }
    }
    
    public NetworkVariable<int> m_maxInventorySlots = new NetworkVariable<int>();

    private NetworkVariable<int> m_numItems = new NetworkVariable<int>();

    private NetworkVariable<int> m_inventoryChangeAlert = new NetworkVariable<int>();


    public override void OnNetworkSpawn()
    {
        if (!this.IsServer)
        {
            m_inventoryChangeAlert.OnValueChanged += (int prevousValue, int newValue) =>
            {
                if (m_inventoryUpdated != null && m_inventoryUpdated.GetInvocationList().Length > 0)
                {
                    m_inventoryUpdated();
                }
            };
        }
    }
    
    public List<(int, int)> GetInventory()
    {
        List<(int, int)> inventory = new List<(int, int)>();
        for (int i = 0; i < m_maxInventorySlots.Value; i++)
        {
            int itemInThisSlot = -1;
            for (int j = 0; j < m_numItems.Value; j++)
            {
                if (m_itemPlacements.Value.arr[j] == i)
                {
                    itemInThisSlot = j;
                    break;
                }
            }

            if (itemInThisSlot != -1)
            {
                inventory.Add((itemInThisSlot, m_itemQuantities.Value.arr[itemInThisSlot]));
            }
            else
            {
                inventory.Add((-1, -1));
            }
        }

        return inventory;
    }
    
    public void SetMaxInventorySlots(int maxInventorySlots)
    {
        m_maxInventorySlots.Value = maxInventorySlots;
        m_inventoryChangeAlert.Value++;
    }

    public void InitializeEmpty(int numItems)
    {
        m_numItems.Value = numItems;
        m_itemQuantities.Value = new NetworkSerializableIntArray();
        m_itemQuantities.Value.arr = new int[numItems];
        m_itemPlacements.Value = new NetworkSerializableIntArray();
        m_itemPlacements.Value.arr = new int[numItems];
        for (int i = 0; i < m_numItems.Value; i++)
        {
            m_itemQuantities.Value.arr[i] = 0;
            m_itemPlacements.Value.arr[i] = -1;
        }
        m_inventoryChangeAlert.Value++;
    }
    
    public int FindSlotForItem(int itemIdx)
    {
        if (m_itemPlacements.Value.arr[itemIdx] == -1)
        {
            for (int i = 0; i < m_maxInventorySlots.Value; i++)
            {
                bool works = true;
                for (int j = 0; j < m_maxInventorySlots.Value; j++)
                {
                    if (m_itemPlacements.Value.arr[j] == i)
                    {
                        works = false;
                        break;
                    }
                }

                if (works)
                {
                    return i;
                    break;
                }
            }

        }
        else
        {
            return m_itemPlacements.Value.arr[itemIdx];
        }

        return -1;
    }

    public bool SetItemPlacement(int itemIndex, int inventoryIndex)
    {
        foreach (int placement in m_itemPlacements.Value.arr)
        {
            if (placement == inventoryIndex)
            {
                
                Debug.LogError(
                    $"Cannot set placement for item with index {itemIndex} to inventory slot: {inventoryIndex}! This inventory slot is already occupied!.");
                return false;       
            }
        }
        m_itemPlacements.Value.arr[itemIndex] = inventoryIndex;
        m_inventoryChangeAlert.Value++;
        return true;
    }

    public bool AddItem(int itemIndex)
    {
        if (m_itemPlacements.Value.arr[itemIndex] == -1)
        {
            Debug.LogError(
                $"Cannot add item with index {itemIndex}! Set a placement for this item in the inventory first.");
            return false;
        }
        
        if (m_itemQuantities.Value.arr[itemIndex] > 0)
        {
            m_itemQuantities.Value.arr[itemIndex]++;
            m_inventoryChangeAlert.Value++;
            return true;
        }
        else if (m_numItemsInInventory < m_maxInventorySlots.Value)
        {
            m_itemQuantities.Value.arr[itemIndex]++;
            m_inventoryChangeAlert.Value++;
            return true;
        }
        
        return false;
    }

    public void RemoveItem(int itemIndex)
    {
        if (m_itemQuantities.Value.arr[itemIndex] == 0)
        {
            Debug.LogError($"Tried to remove an item from inventory when there aren't any left. Item index: {itemIndex}");
        }
        m_itemQuantities.Value.arr[itemIndex]--;

        if (m_itemQuantities.Value.arr[itemIndex] == 0)
        {
            //free the inventory slot
            m_itemPlacements.Value.arr[itemIndex] = -1;
        }
        
        m_inventoryChangeAlert.Value++;
    }
}
