using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

[Serializable]
public class ItemDetails
{
    public string Name;
    public string GUID;
    public Sprite Icon;
    public bool CanDrop;
}

public enum InventoryChangeType
{
    Pickup,
    Drop,
    Add,
    Remove
}


public delegate void InventoryChangedEvent(int inventoryNum, bool isPlayer, InventoryChangeType changeType);
public class InventorySystem : NetworkBehaviour
{
    private static InventorySystem _instance;
    public static InventorySystem Instance
    {
        get { return _instance; }
    }
    
    public InventoryChangedEvent onInventoryChanged;

    public int m_numInventorySlotsPerPlayer = 10;
    
    public int m_numInventorySlotsPerWarehouse = 20;
    
    public List<ItemDetails> m_items;
    
    private void Awake()
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
            foreach (Guid guid in ClientConnectionHandler.Instance.serverSideClientList.Keys)
            {
                ClientConnectionHandler.PlayerSessionInfo sessionInfo =
                    ClientConnectionHandler.Instance.serverSideClientList[guid];
                GameObject playerObj = NetworkManager.Singleton.ConnectedClients[sessionInfo.clientId].PlayerObject.gameObject;
                InventoryNetworkBehaviour inventory = playerObj.GetComponent<InventoryNetworkBehaviour>();
                
                inventory.InitializeEmpty(m_items.Count);
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnNewClientConnected;
        }
    }
    
    

    private void OnNewClientConnected(ulong clientId)
    {
        if (this.IsServer)
        {
            GameObject playerObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.gameObject;
            InventoryNetworkBehaviour inventory = playerObj.GetComponent<InventoryNetworkBehaviour>();
                
            inventory.InitializeEmpty(m_items.Count);
        }
    }

    private ulong GetClientId(int playerNum)
    {
        foreach (ClientConnectionHandler.PlayerSessionInfo sessionInfo in ClientConnectionHandler.Instance
                     .serverSideClientList.Values)
        {
            if (sessionInfo.playerNum == playerNum)
            {
                return sessionInfo.clientId;
            }
        }

        return 0;
    }

    public List<int> GetInventory(int inventoryNum, bool isPlayer)
    {
        if (isPlayer)
        {
            ulong clientId = GetClientId(inventoryNum);
            return NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject
                .GetComponent<InventoryNetworkBehaviour>().m_inventory;
        }
        else
        {
            return MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>().m_inventory;
        }
    }
    

    public void AddItemToInventory(int inventoryNum, string itemGuid, bool isPlayer)
    {
        int itemIdx = -1;
        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i].GUID.ToString() == itemGuid)
            {
                itemIdx = i;
                break;
            }
        }

        if (itemIdx == -1)
        {
            Debug.LogError($"Could not find item with GUID: {itemGuid} in items list!");
        }
        else
        {
            if (isPlayer)
            {
                ulong clientId = GetClientId(inventoryNum);
                NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject
                            .GetComponent<InventoryNetworkBehaviour>().AddItem(itemIdx);
                 
            }
            else
            {
                //warehouse
                MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                    .AddItem(itemIdx);
            }
            
            
            onInventoryChanged(inventoryNum, isPlayer,  InventoryChangeType.Add);
        }
    }
    
    public void RemoveItemFromInventory(int inventoryNum, string itemGuid, bool isPlayer)
    {
        int itemIdx = -1;
        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i].GUID.ToString() == itemGuid)
            {
                itemIdx = i;
                break;
            }
        }

        if (itemIdx == -1)
        {
            Debug.LogError($"Could not find item with GUID: {itemGuid} in items list!");
        }
        else
        {
            if (isPlayer)
            {
                foreach (ClientConnectionHandler.PlayerSessionInfo sessionInfo in ClientConnectionHandler.Instance
                             .serverSideClientList.Values)
                {
                    if (sessionInfo.playerNum == inventoryNum)
                    {
                        NetworkManager.Singleton.ConnectedClients[sessionInfo.clientId].PlayerObject
                            .GetComponent<InventoryNetworkBehaviour>().RemoveItem(itemIdx);
                    }
                }
            }
            else
            {
                //warehouse
                MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>().RemoveItem(itemIdx);
            }
            
            onInventoryChanged(inventoryNum, isPlayer, InventoryChangeType.Remove);
        }
    }
}
