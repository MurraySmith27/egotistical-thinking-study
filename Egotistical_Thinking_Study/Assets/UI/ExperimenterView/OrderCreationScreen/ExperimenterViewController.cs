using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ExperimenterViewController : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset m_orderElement;

    private VisualElement m_root;
    private VisualElement m_orderContainer;

    private List<VisualElement> m_orderElements;

    private void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement;

        m_orderElements = new List<VisualElement>();

        m_orderContainer = m_root.Q<VisualElement>("order-container");

        Order[] orders = GameRoot.Instance.configData.Orders;
        for (int i = 0; i < orders.Length; i++)
        {
            VisualElement orderElement = m_orderElement.Instantiate();
            orderElement.AddToClassList("order");
            ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
            if (orders[i].TimeLimitSeconds != -1)
            {
                orderTimer.style.visibility = Visibility.Visible;
                orderTimer.lowValue = 100;
                orderTimer.title = $"{orders[i].TimeLimitSeconds}s";
            }

            orderElement.Q<Label>("order-number-label").text = $"Order {i + 1}:";
            orderElement.Q<Label>("order-description").text = orders[i].TextDescription;
            orderElement.Q<Label>("send-to-player-label").text = $"Receiving Player: {orders[i].RecievingPlayer}";
            orderElement.Q<Label>("score-reward-label").text = $"Reward: {orders[i].ScoreReward}G";
            VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
            foreach (string key in orders[i].RequiredItems.Keys)
            {
                int itemNum = Int32.Parse(key);
                InventorySlot slot = new InventorySlot(false);
                slot.HoldItem(InventorySystem.Instance.m_items[itemNum], orders[i].RequiredItems[key]);
                itemsContainer.Add(slot);
            }

            string mapDestinationText = $"Destination Warehouse:";
            
            //get map sprite
            ulong warehouseNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(orders[i].DestinationWarehouse);

            Sprite destinationSprite = null;
            foreach (NetworkBehaviour networkBehaviour in FindObjectsOfType<NetworkBehaviour>())
            {
                if (networkBehaviour.NetworkObjectId == warehouseNetworkObjectId)
                {
                    destinationSprite = networkBehaviour.GetComponentInChildren<SpriteRenderer>().sprite;
                    break;
                }
            }

            orderElement.Q<VisualElement>("map-destination-image").style.backgroundImage = destinationSprite.texture;
            
            orderElement.Q<Label>("map-destination-label").text = mapDestinationText;

            int x = i;
            orderElement.Q<Button>("send-order-button").clicked += () =>
            {
                OnOrderSent(x);
            };

            orderElement.MarkDirtyRepaint();
            m_orderContainer.Add(orderElement);
            m_orderElements.Add(orderElement);
            m_orderContainer.MarkDirtyRepaint();
        }
        
        
        m_root.Q<Label>("score-label").text = "0G";

        OrderSystem.Instance.currentScorePerPlayer.OnValueChanged += OnScoreChanged;

        OrderSystem.Instance.onOrderChanged += OnOrderValueChanged;
    }

    private void OnScoreChanged(NetworkSerializableIntArray prev, NetworkSerializableIntArray current)
    {
        m_root.Q<Label>("score-label").text = $"Total Score: {current.arr.Sum()}G";
    }

    private void OnOrderValueChanged(int orderIndex)
    {
        Debug.Log("order value changed");
        VisualElement orderElement = m_orderElements[orderIndex];
        ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
        NetworkSerializableOrder order = OrderSystem.Instance.orders.Value.orders[orderIndex];
        if (order.orderTimeLimit != -1)
        {
            orderTimer.lowValue = 100f * order.orderTimeRemaining /
                                  (float)order.orderTimeLimit;
            orderTimer.title = $"{order.orderTimeRemaining}s";
            Debug.Log($"marking dirty: {orderTimer.title}");
            orderTimer.MarkDirtyRepaint();
        }
    }

    private void OnOrderSent(int orderIndex)
    {
        OrderSystem.Instance.SendOrder(orderIndex);
    }
}
