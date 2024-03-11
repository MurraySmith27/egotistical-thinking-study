using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

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
    
    public NetworkVariable<NetworkSerializableOrderArray> orders;

    public NetworkVariable<NetworkSerializableIntArray> activeOrders;

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
        if (this.IsServer)
        {
            // orders = new NetworkVariable<NetworkSerializableOrderArray>();
            // activeOrders = new NetworkVariable<NetworkSerializableIntArray>();
        }

        base.OnNetworkSpawn();
    }
    
    public void OnGameStart()
    {
        
        if (this.IsServer)
        {
            Debug.Log($"setting up order system: Num orders: {GameRoot.Instance.configData.Orders.Length}");

            activeOrders.Value = new NetworkSerializableIntArray();
            orders.Value = new NetworkSerializableOrderArray();
            List<NetworkSerializableOrder> newOrders = new List<NetworkSerializableOrder>();
            foreach (Order order in GameRoot.Instance.configData.Orders)
            {
                NetworkSerializableOrder newOrder = new NetworkSerializableOrder();
                newOrder.receivingPlayer = order.RecievingPlayer;
                newOrder.mapDestination = order.MapDestination;
                newOrder.requiredItems = order.RequiredItems;
                newOrder.textDescription = order.TextDescription;
                
                newOrders.Add(newOrder);
            }

            orders.Value.arr = newOrders.ToArray();

            activeOrders.Value.arr = new int[GameRoot.Instance.configData.Orders.Length];

            for (int i = 0; i < GameRoot.Instance.configData.Orders.Length; i++)
            {
                activeOrders.Value.arr[i] = 0;
            }
        }
    }

    public NetworkSerializableOrder GetOrder(int orderIndex)
    {
        return orders.Value.arr[orderIndex];
    }

    public void SendOrder(int orderIndex)
    {
        activeOrders.Value.arr[orderIndex] = 1;
        activeOrders.SetDirty(true);
    }
    
}