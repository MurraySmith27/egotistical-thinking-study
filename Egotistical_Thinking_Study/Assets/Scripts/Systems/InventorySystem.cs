using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

public enum InventoryType
{
    Destination,
    Warehouse,
    Player
}


public delegate void InventoryChangedEvent(int inventoryNum, InventoryType inventoryType, InventoryChangeType changeType);
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

    public NetworkVariable<int> m_inventoryCapacityPerPlayer = new NetworkVariable<int>();
    
    public List<ItemDetails> m_items;

    public NetworkVariable<NetworkSerializableIntArray> m_warehousePlayerOwners;

    private const string ITEM_IMAGES_DIRECTORY_PATH = "ItemIcons";
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            if (!Application.isEditor)
            {
                OverrideItemImagesFromDisk();
            }
            _instance = this;
        }
        
        m_warehousePlayerOwners = new NetworkVariable<NetworkSerializableIntArray>();
    }

    private void OverrideItemImagesFromDisk()
    {
        string path = Application.dataPath;
        
        if (Application.platform == RuntimePlatform.OSXPlayer) {
            path += "/../../";
        }
        else if (Application.platform == RuntimePlatform.WindowsPlayer) {
            path += "/../";
        }

        if (!Directory.Exists(path + ITEM_IMAGES_DIRECTORY_PATH))
        {
            return;
        }
        
        string[] filenames = Directory.GetFiles(path + ITEM_IMAGES_DIRECTORY_PATH);

        Array.Sort(filenames);

        List<Sprite> itemSprites = new List<Sprite>();

        foreach (string file in filenames)
        {
            Sprite itemSprite;
            if (ImageLoadingUtils.LoadImageAsSprite(file, out itemSprite))
            {
                itemSprites.Add(itemSprite);
            }
        }

        for (int i = 0; i < itemSprites.Count; i++)
        {
            m_items[i].Icon = itemSprites[i];
        }

        m_items = m_items.Take(itemSprites.Count).ToList();
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
    
    public void RegisterDestinationInventoryChangedCallback(int destinationNum, InventoryUpdatedEvent callback)
    {
        ulong destinationNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfDestination(destinationNum);
        
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == destinationNetworkObjectId)
            {
                GameObject destinationObj = networkObject.gameObject;
                destinationObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated -= callback;
                destinationObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated += callback;
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
    
    public void DeregisterDestinationInventoryChangedCallback(int destinationNum, InventoryUpdatedEvent callback)
    {
        ulong destinationNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfDestination(destinationNum);
        
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == destinationNetworkObjectId)
            {
                GameObject destinationObj = networkObject.gameObject;
                destinationObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated -= callback;
            }
        }
    }
    
    public void DeregisterPlayerInventoryChangedCallback(int playerNum, InventoryUpdatedEvent callback)
    {
        ulong playerNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(playerNum);
        
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == playerNetworkObjectId)
            {
                GameObject playerObj = networkObject.gameObject;
                playerObj.GetComponent<InventoryNetworkBehaviour>().m_inventoryUpdated -= callback;
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
            
            //initialize destination inventories
            for (int i = 0; i < MapGenerator.Instance.destinations.Count; i++)
            {
                GameObject destination = MapGenerator.Instance.destinations[i];
                InventoryNetworkBehaviour inventory = destination.GetComponent<InventoryNetworkBehaviour>();
                inventory.SetMaxInventorySlots(m_numInventorySlotsPerWarehouse);
                inventory.InitializeEmpty(m_numInventorySlotsPerWarehouse);
                
                int numItems = 0;
                foreach (string key in GameRoot.Instance.configData.Destinations[i].Contents.Keys)
                {
                    int itemIndex = Int32.Parse(key);
                    if (GameRoot.Instance.configData.Destinations[i].Contents[key] > 0)
                    {
                        inventory.SetItemPlacement(itemIndex, numItems++);
                    }

                    for (int j = 0; j < GameRoot.Instance.configData.Destinations[i].Contents[key]; j++)
                    {
                        inventory.AddItem(itemIndex);
                    }
                }
            }

            m_inventoryCapacityPerPlayer.Value = GameRoot.Instance.configData.InventoryCapacityPerPlayer;
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
    

    public List<(int, int)> GetInventory(int inventoryNum, InventoryType inventoryType)
    {
        if (this.IsServer)
        {
            if (inventoryType == InventoryType.Player)
            {
                ulong clientId = GetSessionInfo(inventoryNum).clientId;
                return NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject
                    .GetComponent<InventoryNetworkBehaviour>().GetInventory();
            }
            else if (inventoryType == InventoryType.Warehouse)
            {
                return MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                    .GetInventory();
            }
            else if (inventoryType == InventoryType.Destination)
            {
                return MapGenerator.Instance.destinations[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                    .GetInventory();
            }
        }
        else
        {
            if (inventoryType == InventoryType.Player)
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
            else if (inventoryType == InventoryType.Warehouse)
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
            else if (inventoryType == InventoryType.Destination)
            {
                ulong destinationNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfDestination(inventoryNum);
                foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
                {
                    if (networkObject.NetworkObjectId == destinationNetworkObjectId)
                    {
                        return networkObject.GetComponent<InventoryNetworkBehaviour>().GetInventory();
                    }
                }
            }
        }
        
        return new();
    }

    public int GetNumItemsInInventory(int inventoryNum, InventoryType inventoryType)
    {
        List<(int, int)> inventory = GetInventory(inventoryNum, inventoryType);

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
    public void BroadCastInventoryChangedEvent_ClientRpc(int inventoryNum, InventoryType inventoryType, InventoryChangeType changeType) 
    {
        if (onInventoryChanged != null && onInventoryChanged.GetInvocationList().Length > 0)
        {
            onInventoryChanged(inventoryNum, inventoryType, InventoryChangeType.Add);
        }
    }

    public void AddItemToInventory(int inventoryNum, InventoryType inventoryType, string itemGuid, int quantity, int inventorySlot = -1)
    {
        AddItemToInventory_ServerRpc(inventoryNum, inventoryType, itemGuid, quantity, inventorySlot);
    }

    public void TransferItem(int sourceInventoryNum, InventoryType sourceInventoryType, int destinationInventoryNum,
        InventoryType destinationInventoryType, string itemGuid, int quantity, int destinationInventoryItemSlot = -1)
    {
        TransferItem_ServerRpc(sourceInventoryNum, sourceInventoryType, destinationInventoryNum, destinationInventoryType, itemGuid, quantity, destinationInventoryItemSlot);   
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransferItem_ServerRpc(int sourceInventoryNum, InventoryType sourceInventoryType, int destinationInventoryNum,
        InventoryType destinationInventoryType, string itemGuid, int quantity, int destinationInventoryItemSlot = -1)
    {
        Debug.Log("transfer initiated. Adding item");
        if (DoAddItemToInventory(destinationInventoryNum, destinationInventoryType, itemGuid, quantity,
                destinationInventoryItemSlot))
        {
            Debug.Log("add successful. Removing item from inventory");
            DoRemoveItemFromInventory(sourceInventoryNum, sourceInventoryType, itemGuid, quantity);
        }
    }
    
    [ServerRpc (RequireOwnership = false)]
    private void AddItemToInventory_ServerRpc(int inventoryNum, InventoryType inventoryType, string itemGuid, int quantity, int inventorySlot = -1, ServerRpcParams serverRpcParams = default)
    {
        DoAddItemToInventory(inventoryNum, inventoryType, itemGuid, quantity, inventorySlot, serverRpcParams);
    }

    private bool DoAddItemToInventory(int inventoryNum, InventoryType inventoryType, string itemGuid, int quantity, int inventorySlot = -1, ServerRpcParams serverRpcParams = default)
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

        bool success = false;

        if (itemIdx == -1)
        {
            Debug.LogError($"Could not find item with GUID: {itemGuid} in items list!");
        }
        else
        {
            if (inventoryType == InventoryType.Player)
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
            else if (inventoryType == InventoryType.Warehouse)
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
            else if (inventoryType == InventoryType.Destination)
            {
                InventoryNetworkBehaviour destinationInventory = MapGenerator.Instance.destinations[inventoryNum]
                    .GetComponent<InventoryNetworkBehaviour>();

                if (inventorySlot == -1)
                {
                    inventorySlot = destinationInventory.FindSlotForItem(itemIdx);
                }

                destinationInventory.SetItemPlacement(itemIdx, inventorySlot);
                
                for (int i = 0; i < quantity; i++)
                {
                    success = destinationInventory.AddItem(itemIdx);
                }
            }

            if (success)
            {
                if (onInventoryChanged != null && onInventoryChanged.GetInvocationList().Length > 0)
                {
                    onInventoryChanged(inventoryNum, inventoryType, InventoryChangeType.Add);
                }
                BroadCastInventoryChangedEvent_ClientRpc(inventoryNum, inventoryType, InventoryChangeType.Add);
            }

        }

        return success;
    }

    public void RemoveItemFromInventory(int inventoryNum, InventoryType inventoryType, string itemGuid, int quantity)
    {
        RemoveItemFromInventory_ServerRpc(inventoryNum, inventoryType, itemGuid, quantity);
    }
    
    [ServerRpc (RequireOwnership = false)]
    private void RemoveItemFromInventory_ServerRpc(int inventoryNum, InventoryType inventoryType, string itemGuid, int quantity, ServerRpcParams serverRpcParams = default)
    {
        DoRemoveItemFromInventory(inventoryNum, inventoryType, itemGuid, quantity);
    }

    private void DoRemoveItemFromInventory(int inventoryNum, InventoryType inventoryType, string itemGuid, int quantity)
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
                if (inventoryType == InventoryType.Player)
                {
                    MapGenerator.Instance.playerObjects[inventoryNum]
                        .GetComponent<InventoryNetworkBehaviour>().RemoveItem(itemIdx);
                }
                else if (inventoryType == InventoryType.Warehouse)
                {
                    //warehouse
                    MapGenerator.Instance.warehouses[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                        .RemoveItem(itemIdx);
                }
                else if (inventoryType == InventoryType.Destination)
                {
                    MapGenerator.Instance.destinations[inventoryNum].GetComponent<InventoryNetworkBehaviour>()
                        .RemoveItem(itemIdx);
                }
            }

            if (onInventoryChanged != null && onInventoryChanged.GetInvocationList().Length > 0)
            {
                onInventoryChanged(inventoryNum, inventoryType, InventoryChangeType.Remove);
            }
            BroadCastInventoryChangedEvent_ClientRpc(inventoryNum, inventoryType, InventoryChangeType.Remove);
        }
    }
}
