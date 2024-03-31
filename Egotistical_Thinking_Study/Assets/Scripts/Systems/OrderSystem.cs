using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;


public delegate void OrderCompleteEvent(int orderIndex);

public class OrderSystem : NetworkBehaviour
{

    
    private static OrderSystem _instance;

    public static OrderSystem Instance
    {
        get
        {
            return _instance;
        }
    }

    public OrderCompleteEvent onOrderComplete;
    
    public NetworkVariable<NetworkSerializableOrderArray> orders = new NetworkVariable<NetworkSerializableOrderArray>();

    public NetworkVariable<NetworkSerializableIntArray> activeOrders = new NetworkVariable<NetworkSerializableIntArray>();
    
    public NetworkVariable<NetworkSerializableIntArray> completeOrders = new NetworkVariable<NetworkSerializableIntArray>();

    [FormerlySerializedAs("m_currentScore")] public NetworkVariable<int> currentScore = new NetworkVariable<int>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
        
    }

    public override void OnNetworkSpawn()
    {
        if (!this.IsServer)
        {
            //print all the orders:
            // foreach (NetworkSerializableOrder order in orders.Value.arr)
            // {
            //     Debug.Log($"order. to player: {order.receivingPlayer}");
            // }

            currentScore.Value = 0;

        }
        InventorySystem.Instance.onInventoryChanged += OnInventoryChanged;
    }

    private void OnInventoryChanged(int inventoryNum, bool isPlayer, InventoryChangeType changeType)
    {
        if (!isPlayer)
        {
            List<(int, int)> inventory = InventorySystem.Instance.GetInventory(inventoryNum, isPlayer);
            for (int i = 0; i < activeOrders.Value.arr.Length; i++)
            {
                if (activeOrders.Value.arr[i] == 1)
                {
                    bool complete = true;
                    foreach (string key in orders.Value.orders[i].requiredItems.Keys)
                    {
                        int numOfThisItem = 0;
                        for (int j = 0; j < inventory.Count; j++)
                        {
                            if (inventory[j].Item1 == Int32.Parse(key))
                            {
                                numOfThisItem = inventory[j].Item2;
                            }
                        }
                        if (orders.Value.orders[i].requiredItems[key] > numOfThisItem)
                        {
                            complete = false;
                        }
                    }
                    
                    if (complete)
                    {
                        if (this.IsServer)
                        {
                            completeOrders.Value.arr[i] = 1;
                            completeOrders.SetDirty(true);
                        }

                        if (onOrderComplete != null && onOrderComplete.GetInvocationList().Length > 0)
                        {
                            onOrderComplete(i);
                        }

                        currentScore.Value += orders.Value.orders[i].scoreReward;
                    }
                }
            }

        }
    }



    public void OnGameStart()
    {
        if (this.IsServer)
        {
            activeOrders.Value = new NetworkSerializableIntArray();
            completeOrders.Value = new NetworkSerializableIntArray();
            orders.Value = new NetworkSerializableOrderArray();
            List<NetworkSerializableOrder> newOrders = new List<NetworkSerializableOrder>();
            foreach (Order order in GameRoot.Instance.configData.Orders)
            {
                NetworkSerializableOrder newOrder = new NetworkSerializableOrder();
                newOrder.receivingPlayer = order.RecievingPlayer;
                newOrder.destinationWarehouse = order.DestinationWarehouse;
                newOrder.requiredItems = order.RequiredItems;
                newOrder.textDescription = (FixedString64Bytes)order.TextDescription;
                newOrder.scoreReward = order.ScoreReward;
                
                newOrders.Add(newOrder);
            }
            
            orders.Value.orders = newOrders.ToArray();

            activeOrders.Value.arr = new int[GameRoot.Instance.configData.Orders.Length];
            completeOrders.Value.arr = new int[GameRoot.Instance.configData.Orders.Length];

            for (int i = 0; i < GameRoot.Instance.configData.Orders.Length; i++)
            {
                activeOrders.Value.arr[i] = 0;
            }
        }
    }

    public NetworkSerializableOrder GetOrder(int orderIndex)
    {
        return orders.Value.orders[orderIndex];
    }

    public void SendOrder(int orderIndex)
    {
        activeOrders.Value.arr[orderIndex] = 1;
        activeOrders.SetDirty(true);
    }
    
}