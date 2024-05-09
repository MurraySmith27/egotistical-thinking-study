using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

    public int m_inventoryCapacityPerPlayer = 5;
    
    public List<ItemDetails> m_items;

    public NetworkVariable<NetworkSerializableIntArray> m_warehousePlayerOwners;
    
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
        
        m_warehousePlayerOwners = new NetworkVariable<NetworkSerializableIntArray>();
    }

    public override void OnNetworkSpawn()
    {
        if (this.IsServer)
        {
            m_warehousePlayerOwners.Value = new NetworkSerializableIntArray();
            m_warehousePlayerOwners.Value.arr = new int[0];
        }
    }

    public void RegisterPlayerInventoryChangedCallback(int playerNum, InventoryUpdatedEvent callback)
    {

        ulong playerNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(playerNum);
        
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == playerNetworkObjectId)
            {
                GameObject playerObj = networkObject.gameObject;
                playerObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated += callback;
            }
        }
    }

    public void RegisterWarehouseInventoryChangedCallback(int warehouseNum, InventoryUpdatedEvent callback)
    {
        ulong warehouseNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(warehouseNum);
        
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == warehouseNetworkObjectId)
            {
                GameObject warehouseObj = networkObject.gameObject;
                warehouseObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated -= callback;
                warehouseObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated += callback;
            }
        }
    }
    
    public void DeregisterWarehouseInventoryChangedCallback(int warehouseNum, InventoryUpdatedEvent callback)
    {
        ulong warehouseNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(warehouseNum);
        
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == warehouseNetworkObjectId)
            {
                GameObject warehouseObj = networkObject.gameObject;
                warehouseObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated -= callback;
            }
        }
    }
    
    public void OnGameStart() 
    {
        if (this.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnNewClientConnected;

            m_warehousePlayerOwners.Value.arr = new int[MapGenerator.Instance.warehouses.Count];

            //initialize warehouse inventories
            for (int i = 0; i < MapGenerator.Instance.warehouses.Count; i++)
            {
                GameObject warehouse = MapGenerator.Instance.warehouses[i];
                InventoryNetworkBehaviour inventory = warehouse.GetComponent<InventoryNetworkBehaviour>();
                inventory.SetMaxInventorySlots(m_numInventorySlotsPerWarehouse);
                inventory.InitializeEmpty(m_numInventorySlotsPerWarehouse);

                m_warehousePlayerOwners.Value.arr[i] = GameRoot.Instance.configData.Warehouses[i].PlayerOwner;
                
                int numItems = 0;
                foreach (string key in GameRoot.Instance.configData.Warehouses[i].Contents.Keys)
                {
                    int itemIndex = Int32.Parse(key);
                    Debug.Log($"key: {key}, num items: {GameRoot.Instance.configData.Warehouses[i].Contents[key]}");
                    if (GameRoot.Instance.configData.Warehouses[i].Contents[key] > 0)
                    {
                        inventory.SetItemPlacement(itemIndex, numItems++);
                    }

                    for (int j = 0; j < GameRoot.Instance.configData.Warehouses[i].Contents[key]; j++)
                    {
                        inventory.AddItem(itemIndex);
                    }
                }
            }
        }
    }

    public int GetOwnerOfWarehouse(int warehouseNum)
    {
        if (warehouseNum < 0 || warehouseNum > m_warehousePlayerOwners.Value.arr.Length)
        {
            return -1;
        }
        return m_warehousePlayerOwners.Value.arr[warehouseNum];
    }

    private void OnNewClientConnected(ulong clientId)
    {
        if (this.IsServer)
        {
            int playerNum = GetSessionInfoFromClientId(clientId).playerNum;
            GameObject playerObj =
                MapGenerator.Instance.playerObjects[playerNum];
            InventoryNetworkBehaviour inventory = playerObj.GetComponent<InventoryNetworkBehaviour>();
            inventory.SetMaxInventorySlots(m_numInventorySlotsPerPlayer);
            
            inventory.InitializeEmpty(m_items.Count);
        }
    }
    
    private ClientConnectionHandler.PlayerSessionInfo GetSessionInfoFromClientId(ulong clientId)
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
    

    public List<(int, int)> GetInventory(int inventoryNum, bool isPlayer)
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
        else
        {
            if (isPlayer)
            {
                ulong playerNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(inventoryNum);
                
                foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
                {
                    if (networkObject.NetworkObjectId == playerNetworkObjectId)
                    {
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

    public int GetNumItemsInInventory(int inventoryNum, bool isPlayer)
    {
        List<(int, int)> inventory = GetInventory(inventoryNum, isPlayer);

        int inventoryCount = 0;
        for (int i = 0; i < inventory.Count; i++)
        {
            if (inventory[i].Item1 != -1)
            {
                inventoryCount += inventory[i].Item2;
            }
        }

        return inventoryCount;
    }
    
    [ClientRpc]
    public void BroadCastInventoryChangedEvent_ClientRpc(int inventoryNum, bool isPlayer, InventoryChangeType changeType) 
    {
        if (onInventoryChanged != null && onInventoryChanged.GetInvocationList().Length > 0)
        {
            onInventoryChanged(inventoryNum, isPlayer, InventoryChangeType.Add);
        }
    }

    public void AddItemToInventory(int inventoryNum, bool isPlayer, string itemGuid, int quantity, int inventorySlot = -1)
    {
        AddItemToInventory_ServerRpc(inventoryNum, isPlayer, itemGuid, quantity, inventorySlot);
    }

    public void TransferItem(int sourceInventoryNum, bool sourceInventoryIsPlayer, int destinationInventoryNum,
        bool destinationInventoryIsPlayer, string itemGuid, int quantity, int destinationInventoryItemSlot = -1)
    {
        TransferItem_ServerRpc(sourceInventoryNum, sourceInventoryIsPlayer, destinationInventoryNum, destinationInventoryIsPlayer, itemGuid, quantity, destinationInventoryItemSlot);   
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransferItem_ServerRpc(int sourceInventoryNum, bool sourceInventoryIsPlayer, int destinationInventoryNum,
        bool destinationInventoryIsPlayer, string itemGuid, int quantity, int destinationInventoryItemSlot = -1)
    {
        if (DoAddItemToInventory(destinationInventoryNum, destinationInventoryIsPlayer, itemGuid, quantity,
                destinationInventoryItemSlot))
        {
            DoRemoveItemFromInventory(sourceInventoryNum, sourceInventoryIsPlayer, itemGuid, quantity);
        }
    }
    
    [ServerRpc (RequireOwnership = false)]
    private void AddItemToInventory_ServerRpc(int inventoryNum, bool isPlayer, string itemGuid, int quantity, int inventorySlot = -1, ServerRpcParams serverRpcParams = default)
    {
        DoAddItemToInventory(inventoryNum, isPlayer, itemGuid, quantity, inventorySlot, serverRpcParams);
    }

    private bool DoAddItemToInventory(int inventoryNum, bool isPlayer, string itemGuid, int quantity, int inventorySlot = -1, ServerRpcParams serverRpcParams = default)
    {
        Debug.Log($"doing add item to inventory {itemGuid}");
        int itemIdx = -1;
        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i].GUID.ToString() == itemGuid)
            {
                itemIdx = i;
                break;
            }
        }

        bool success = false;

        if (itemIdx == -1)
        {
            Debug.LogError($"Could not find item with GUID: {itemGuid} in items list!");
        }
        else
        {
            if (isPlayer)
            {
                InventoryNetworkBehaviour playerInventory = MapGenerator.Instance.playerObjects[inventoryNum]
                    .GetComponent<InventoryNetworkBehaviour>();

                if (inventorySlot == -1)
                {
                    inventorySlot = playerInventory.FindSlotForItem(itemIdx);
                }
                
                playerInventory.SetItemPlacement(itemIdx, inventorySlot);
                for (int i = 0; i < quantity; i++)
                {
                    success = playerInventory.AddItem(itemIdx);
                }
            }
            else
            {
                //warehouse
                InventoryNetworkBehaviour warehouseInventory = MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>();
                
                if (inventorySlot == -1)
                {
                    inventorySlot = warehouseInventory.FindSlotForItem(itemIdx);
                }
                
                warehouseInventory.SetItemPlacement(itemIdx, inventorySlot);
                for (int i = 0; i < quantity; i++)
                {
                    success = warehouseInventory.AddItem(itemIdx);
                }
            }

            if (success)
            {
                if (onInventoryChanged != null && onInventoryChanged.GetInvocationList().Length > 0)
                {
                    onInventoryChanged(inventoryNum, isPlayer, InventoryChangeType.Add);
                }
                BroadCastInventoryChangedEvent_ClientRpc(inventoryNum, isPlayer, InventoryChangeType.Add);
            }

        }

        return success;
    }

    public void RemoveItemFromInventory(int inventoryNum, bool isPlayer, string itemGuid, int quantity)
    {
        RemoveItemFromInventory_ServerRpc(inventoryNum, isPlayer, itemGuid, quantity);
    }
    
    [ServerRpc (RequireOwnership = false)]
    private void RemoveItemFromInventory_ServerRpc(int inventoryNum, bool isPlayer, string itemGuid, int quantity, ServerRpcParams serverRpcParams = default)
    {
        DoRemoveItemFromInventory(inventoryNum, isPlayer, itemGuid, quantity);
    }

    private void DoRemoveItemFromInventory(int inventoryNum, bool isPlayer, string itemGuid, int quantity)
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

            for (int i = 0; i < quantity; i++)
            {
                if (isPlayer)
                {
                    MapGenerator.Instance.playerObjects[inventoryNum]
                        .GetComponent<InventoryNetworkBehaviour>().RemoveItem(itemIdx);
                }
                else
                {
                    //warehouse
                    MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                        .RemoveItem(itemIdx);
                }
            }

            if (onInventoryChanged != null && onInventoryChanged.GetInvocationList().Length > 0)
            {
                onInventoryChanged(inventoryNum, isPlayer, InventoryChangeType.Remove);
            }
            BroadCastInventoryChangedEvent_ClientRpc(inventoryNum, isPlayer, InventoryChangeType.Remove);
        }
    }
}
