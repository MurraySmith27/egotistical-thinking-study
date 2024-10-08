using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public delegate void OnRoadblockActivate(int roadblockNum);

public delegate void OnRoadblockDeactivate(int roadblockNum);

public class RoadblockSystem : NetworkBehaviour
{
    private static RoadblockSystem _instance;

    public static RoadblockSystem Instance
    {
        get
        {
            return _instance;
        }
    }

    public NetworkVariable<NetworkSerializableIntArray> activeRoadblocks = new NetworkVariable<NetworkSerializableIntArray>();
    
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

        }
    }

    public void ActivateRoadblock(int roadblockNum)
    {
        
    }

    public void DeactivateRoadblock(int roadblockNum)
    {
        
    }

    public bool IsRoadblockActive(int roadblockNum)
    {
        return false;
    }
}