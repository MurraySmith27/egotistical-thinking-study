using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.PackageManager;
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

            NetworkManager.Singleton.OnClientConnectedCallback += OnNewClientConnected;

            //initialize warehouse inventories
            for (int i = 0; i < MapGenerator.Instance.warehouses.Count; i++)
            {
                GameObject warehouse = MapGenerator.Instance.warehouses[i];
                InventoryNetworkBehaviour inventory = warehouse.GetComponent<InventoryNetworkBehaviour>();
                
                inventory.InitializeEmpty(m_numInventorySlotsPerWarehouse);

                foreach (int item in GameRoot.Instance.configData.WarehouseContents[i])
                {
                    inventory.AddItem(item);
                }
            }
        }
    }

    private void OnNewClientConnected(ulong clientId)
    {
        if (this.IsServer)
        {
            int playerNum = GetSessionInfo(clientId).playerNum;
            GameObject playerObj =
                MapGenerator.Instance.playerObjects[playerNum];
            InventoryNetworkBehaviour inventory = playerObj.GetComponent<InventoryNetworkBehaviour>();
            inventory.SetMaxInventorySlots(m_numInventorySlotsPerPlayer);
            
            inventory.InitializeEmpty(m_items.Count);
        }
    }
    
    private ClientConnectionHandler.PlayerSessionInfo GetSessionInfo(ulong clientId)
    {
        ClientConnectionHandler.PlayerSessionInfo _sessionInfo = new ClientConnectionHandler.PlayerSessionInfo();
        foreach (ClientConnectionHandler.PlayerSessionInfo sessionInfo in ClientConnectionHandler.Instance
                     .serverSideClientList.Values)
        {
            if (sessionInfo.clientId == clientId)
            {
                _sessionInfo = sessionInfo;
                break;
            }
        }

        return _sessionInfo;
    }

    private ClientConnectionHandler.PlayerSessionInfo GetSessionInfo(int playerNum)
    {
        ClientConnectionHandler.PlayerSessionInfo _sessionInfo = new ClientConnectionHandler.PlayerSessionInfo();
        foreach (ClientConnectionHandler.PlayerSessionInfo sessionInfo in ClientConnectionHandler.Instance
                     .serverSideClientList.Values)
        {
            if (sessionInfo.playerNum == playerNum)
            {
                _sessionInfo = sessionInfo;
                break;
            }
        }

        return _sessionInfo;
    }
    

    public List<int> GetInventory(int inventoryNum, bool isPlayer)
    {
        if (this.IsServer)
        {
            if (isPlayer)
            {
                ulong clientId = GetSessionInfo(inventoryNum).clientId;
                return NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject
                    .GetComponent<InventoryNetworkBehaviour>().GetInventory();
            }
            else
            {
                return MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                    .GetInventory();
            }
        }
        else if (this.IsClient)
        {
            if (isPlayer)
            {
                ulong playerNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(inventoryNum);
                foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
                {
                    if (networkObject.NetworkObjectId == playerNetworkObjectId)
                    {
                        Debug.Log($"player object id: {playerNetworkObjectId}");
                        return networkObject.GetComponent<InventoryNetworkBehaviour>().GetInventory();
                    }
                }
            }
            else
            {
                ulong warehouseNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(inventoryNum);
                foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
                {
                    if (networkObject.NetworkObjectId == warehouseNetworkObjectId)
                    {
                        return networkObject.GetComponent<InventoryNetworkBehaviour>().GetInventory();
                    }
                }
            }
        }
        return new();
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
                ulong clientId = GetSessionInfo(inventoryNum).clientId;
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
