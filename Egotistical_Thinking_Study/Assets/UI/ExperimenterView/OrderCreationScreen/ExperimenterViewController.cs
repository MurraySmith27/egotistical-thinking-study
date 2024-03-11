using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ExperimenterViewController : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset m_orderElement;

    private VisualElement m_root;
    private VisualElement m_orderContainer;

    private void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement;

        m_orderContainer = m_root.Q<VisualElement>("order-container");

        Order[] orders = GameRoot.Instance.configData.Orders;
        for (int i = 0; i < orders.Length; i++)
        {
            VisualElement orderElement = m_orderElement.Instantiate();
            orderElement.AddToClassList("order");
            orderElement.Q<Label>("order-number-label").text = $"Order {i + 1}:";
            orderElement.Q<Label>("order-description").text = orders[i].TextDescription;
            orderElement.Q<Label>("send-to-player-label").text = $"Receiving Player: {orders[i].RecievingPlayer}";
            VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
            foreach (string key in orders[i].RequiredItems.Keys)
            {
                int itemNum = Int32.Parse(key);
                InventorySlot slot = new InventorySlot(false);
                slot.HoldItem(InventorySystem.Instance.m_items[itemNum], orders[i].RequiredItems[key]);
                itemsContainer.Add(slot);
            }

            string mapDestinationText = $"Destination Coords: ({orders[i].MapDestination[0]}, {orders[i].MapDestination[1]})";
            orderElement.Q<Label>("map-destination-label").text = mapDestinationText;

            int x = i;
            orderElement.Q<Button>("send-order-button").clicked += () =>
            {
                OnOrderSent(x);
            };

            m_orderContainer.Add(orderElement);            
        }
    }

    private void OnOrderSent(int orderIndex)
    {
        OrderSystem.Instance.SendOrder(orderIndex);
    }
}
