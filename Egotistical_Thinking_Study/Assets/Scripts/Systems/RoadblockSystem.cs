using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public delegate void RoadblockActivateDelegate(int roadblockNum);

public delegate void RoadblockDeactivateDelegate(int roadblockNum);

public class RoadblockSystem : NetworkBehaviour
{
    public static RoadblockDeactivateDelegate OnRoadblockActivate;

    public static RoadblockDeactivateDelegate OnRoadblockDeactivate;
    
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

    
    //should only be activated from server
    public void ActivateRoadblock(int roadblockNum)
    {
        if (activeRoadblocks.Value.arr[roadblockNum] == 0)
        {
            activeRoadblocks.Value.arr[roadblockNum] = 1;
            OnRoadblockActivate(roadblockNum);
        }
    }

    //should only be activated from server
    public void DeactivateRoadblock(int roadblockNum)
    {
        if (activeRoadblocks.Value.arr[roadblockNum] == 0)
        {
            activeRoadblocks.Value.arr[roadblockNum] = 1;
            OnRoadblockActivate(roadblockNum);
        }
    }

    //should only be activated from server
    public bool IsRoadblockActive(int roadblockNum)
    {
        return false;
    }
}