using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class OrderCreationScreenController : MonoBehaviour
{

    public List<InventorySlot> InventoryItems = new List<InventorySlot>();

    private VisualElement m_Root;
    private VisualElement m_SlotContainer;

    private void Start()
    {
        m_Root = GetComponent<UIDocument>().rootVisualElement;

        m_SlotContainer = m_Root.Q<VisualElement>("slot-container");
        for (int i = 0; i < InventorySystem.Instance.m_numInventorySlotsPerPlayer; i++)
        {
            InventorySlot item = new InventorySlot();
            
            InventoryItems.Add(item);
            m_SlotContainer.Add(item);
        }

        InventorySystem.Instance.onInventoryChanged += OnInventoryChanged;
    }

    private void OnInventoryChanged(int inventoryNum, bool isPlayer, InventoryChangeType change)
    {
        
        //TODO:
    }
}
