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
    public static RoadblockActivateDelegate OnRoadblockActivate;

    public static RoadblockDeactivateDelegate OnRoadblockDeactivate;

    private static RoadblockSystem _instance;

    public static RoadblockSystem Instance
    {
        get { return _instance; }
    }

    public NetworkVariable<NetworkSerializableIntArray> activeRoadblocks =
        new NetworkVariable<NetworkSerializableIntArray>();

    public NetworkVariable<NetworkSerializableRoadblockArray> roadblocks =
        new NetworkVariable<NetworkSerializableRoadblockArray>();

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

    void OnDestroy()
    {
        activeRoadblocks.OnValueChanged -= OnActiveRoadblocksChangedClientSide;

        if (OrderSystem.Instance != null)
        {
            OrderSystem.Instance.onOrderSent -= OnOrderSent;
            OrderSystem.Instance.onOrderComplete -= OnOrderComplete;
        }
    }

    public void OnGameStart()
    {
        List<Roadblock> configRoadblocks = GameRoot.Instance.configData.Roadblocks;

        NetworkSerializableIntArray active = new NetworkSerializableIntArray();

        active.arr = new int[configRoadblocks.Count];

        NetworkSerializableRoadblockArray roadblockArray = new NetworkSerializableRoadblockArray();

        NetworkSerializableRoadblock[] roadblocksArr = new NetworkSerializableRoadblock[configRoadblocks.Count];
        for (int i = 0; i < configRoadblocks.Count; i++)
        {
            roadblocksArr[i] = new NetworkSerializableRoadblock();
            List<(int, int)> affectedTiles = new List<(int, int)>();

            foreach (List<int> affectedTileSet in configRoadblocks[i].blockedTiles)
            {
                affectedTiles.Add((affectedTileSet[0], affectedTileSet[1]));
            }

            roadblocksArr[i].affectedTiles = affectedTiles;

            roadblocksArr[i].informedPlayer = configRoadblocks[i].informedPlayer;

            active.arr[i] = 0;
        }

        roadblockArray.roadblocks = roadblocksArr;

        roadblocks.Value = roadblockArray;

        activeRoadblocks.Value = active;
    }

    public override void OnNetworkSpawn()
    {
        if (!this.IsServer)
        {
            activeRoadblocks.OnValueChanged -= OnActiveRoadblocksChangedClientSide;
            activeRoadblocks.OnValueChanged += OnActiveRoadblocksChangedClientSide;
        }
        else
        {
            OrderSystem.Instance.onOrderSent -= OnOrderSent;
            OrderSystem.Instance.onOrderSent += OnOrderSent;
            
            OrderSystem.Instance.onOrderComplete -= OnOrderComplete;
            OrderSystem.Instance.onOrderComplete += OnOrderComplete;
        }
    }

    //should only be called from server
    private void OnOrderSent(int orderIndex)
    {
        if (IsServer)
        {
            for (int i = 0; i < GameRoot.Instance.configData.Roadblocks.Count; i++)
            {
                Roadblock roadblock = GameRoot.Instance.configData.Roadblocks[i];

                if (roadblock.autoActivateOnOrder == orderIndex)
                {
                    RoadblockSystem.Instance.ActivateRoadblock(i);
                }
            }
        }
    }

    //should only be called from server
    private void OnOrderComplete(int orderIndex)
    {
        if (IsServer)
        {
            for (int i = 0; i < GameRoot.Instance.configData.Roadblocks.Count; i++)
            {
                Roadblock roadblock = GameRoot.Instance.configData.Roadblocks[i];

                if (roadblock.autoDeactivateOnCompleteOrder == orderIndex)
                {
                    RoadblockSystem.Instance.DeactivateRoadblock(i);
                }
            }
        }
    }

    private void OnActiveRoadblocksChangedClientSide(NetworkSerializableIntArray oldValue, 
        NetworkSerializableIntArray newValue) 
    {
        for (int i = 0; i < oldValue.arr.Length; i++)
        {
            if (oldValue.arr[i] != newValue.arr[i])
            {
                if (oldValue.arr[i] == 0)
                {
                    OnRoadblockActivate?.Invoke(i);
                }
                else
                {
                    OnRoadblockDeactivate?.Invoke(i);
                }
            }
        }    
    }


//should only be activated from server
    public void ActivateRoadblock(int roadblockNum)
    {
        Debug.Log($"activating roadblock {roadblockNum}. is active: {activeRoadblocks.Value.arr[roadblockNum]}");
        if (activeRoadblocks.Value.arr[roadblockNum] == 0)
        {
            activeRoadblocks.Value.arr[roadblockNum] = 1;
            OnRoadblockActivate?.Invoke(roadblockNum);
        }
    }

    //should only be activated from server
    public void DeactivateRoadblock(int roadblockNum)
    {
        if (activeRoadblocks.Value.arr[roadblockNum] == 1)
        {
            activeRoadblocks.Value.arr[roadblockNum] = 0;
            OnRoadblockDeactivate?.Invoke(roadblockNum);
        }
    }

    //should only be activated from server
    public bool IsRoadblockActive(int roadblockNum)
    {
        return activeRoadblocks.Value.arr[roadblockNum] != 0;
    }

    public bool IsThisRoadblockInformedPlayer(int roadblockNum)
    {
        if (IsServer)
        {
            return false;
        }
        else
        {
            return roadblocks.Value.roadblocks[roadblockNum].informedPlayer ==
                   ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
        }
    }

    public int GetRoadblockInformedPlayer(int roadblockNum)
    {
        return roadblocks.Value.roadblocks[roadblockNum].informedPlayer;
    }

    public List<(int, int)> GetRoadblockAffectedTiles(int roadblockNum)
    {
        return roadblocks.Value.roadblocks[roadblockNum].affectedTiles;
    }

    public List<(int, int)> GetAllDisabledTiles()
    {
        List<(int, int)> disabledTiles = new();
        for (int i = 0; i < activeRoadblocks.Value.arr.Length; i++)
        {
            if (activeRoadblocks.Value.arr[i] != 0)
            {
                disabledTiles.AddRange(GetRoadblockAffectedTiles(i));
            }
        }

        return disabledTiles;
    }
}