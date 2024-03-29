
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class MapDataNetworkBehaviour : NetworkBehaviour
{
    
    private static MapDataNetworkBehaviour _instance;

    public static MapDataNetworkBehaviour Instance
    {
        get
        {
            return _instance;
        }
    }

    public NetworkVariable<NetworkSerializableUlongArray> warehouseNetworkObjectIds { get; private set; }
    
    public NetworkVariable<NetworkSerializableUlongArray> playerNetworkObjectIds { get; private set; }

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

        warehouseNetworkObjectIds = new NetworkVariable<NetworkSerializableUlongArray>();
        playerNetworkObjectIds = new NetworkVariable<NetworkSerializableUlongArray>();
    }

    public ulong GetNetworkIdOfWarehouse(int warehouseNum)
    {
        return warehouseNetworkObjectIds.Value.arr[warehouseNum];
    }

    public ulong GetNetworkIdOfPlayer(int playerNum)
    {
        return playerNetworkObjectIds.Value.arr[playerNum];
    }

    public List<ulong> GetAllWarehouseNetworkObjectIds()
    {
        return new List<ulong>(warehouseNetworkObjectIds.Value.arr);
    }

    public override void OnNetworkSpawn()
    {
        if (this.IsServer)
        {
            warehouseNetworkObjectIds.Value = new NetworkSerializableUlongArray();
            warehouseNetworkObjectIds.Value.arr = new ulong[0];
            
            playerNetworkObjectIds.Value = new NetworkSerializableUlongArray();
            playerNetworkObjectIds.Value.arr = new ulong[0];
        }
        else
        {
        }
    }

    public void RegisterWareHouseNetworkObjectIds(List<GameObject> warehouses)
    {
        warehouseNetworkObjectIds.Value.arr = new ulong[warehouses.Count];

        for (int i = 0; i < warehouses.Count; i++)
        {
            warehouseNetworkObjectIds.Value.arr[i] = warehouses[i].GetComponent<NetworkObject>().NetworkObjectId;
        }
    }

    public void RegisterPlayerNetworkObjectIds(List<GameObject> players)
    {
        playerNetworkObjectIds.Value.arr = new ulong[players.Count];

        for (int i = 0; i < players.Count; i++)
        {
            playerNetworkObjectIds.Value.arr[i] = players[i].GetComponent<NetworkObject>().NetworkObjectId;
        }
    }
    
    

}