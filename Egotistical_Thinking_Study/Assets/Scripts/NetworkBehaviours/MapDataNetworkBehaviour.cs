
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public NetworkVariable<NetworkSerializableUlongArray> destinationNetworkObjectIds { get; private set; }
    public NetworkVariable<NetworkSerializableUlongArray> playerNetworkObjectIds { get; private set; }
    
    public NetworkVariable<NetworkSerializableIntArray> totalScoreVisiblePerPlayer { get; private set; }
    public NetworkVariable<NetworkSerializableIntArray> revenueVisiblePerPlayer { get; private set; }
    public NetworkVariable<NetworkSerializableIntArray> deductionsVisiblePerPlayer { get; private set; }
    
    public NetworkVariable<int> maxGasPerPlayer = new NetworkVariable<int>();

    public NetworkVariable<bool> isScoreShared = new NetworkVariable<bool>();

    public NetworkVariable<int> mapWidth = new NetworkVariable<int>();
    
    public NetworkVariable<int> mapHeight = new NetworkVariable<int>();
    
    private const string DESTINATION_IMAGES_DIRECTORY_PATH = "DestinationIcons";

    private const string WAREHOUSE_IMAGES_DIRECTORY_PATH = "WarehouseIcons";
    
    private const string TRUCK_IMAGES_DIRECTORY_PATH = "TruckIcons";

    private List<Coroutine> roadblockTimerCountdownCoroutines = new List<Coroutine>();
    
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
        destinationNetworkObjectIds = new NetworkVariable<NetworkSerializableUlongArray>();
        playerNetworkObjectIds = new NetworkVariable<NetworkSerializableUlongArray>();

        totalScoreVisiblePerPlayer = new NetworkVariable<NetworkSerializableIntArray>();
        revenueVisiblePerPlayer = new NetworkVariable<NetworkSerializableIntArray>();
        deductionsVisiblePerPlayer = new NetworkVariable<NetworkSerializableIntArray>();
    }

    void Start()
    {
        if (!Application.isEditor && this.IsClient)
        {
            OverrideDestinationIconsFromDisk();
            OverrideWarehouseIconsFromDisk();
            OverrideTruckIconsFromDisk();
        }
    }

    public void OnGameStart()
    {
        if (this.IsClient)
        {
            if (!Application.isEditor)
            {
                OverrideDestinationIconsFromDisk();
                OverrideWarehouseIconsFromDisk();
                OverrideTruckIconsFromDisk();
            }
        }
        else
        {
            maxGasPerPlayer.Value = GameRoot.Instance.configData.MaxGasPerPlayer;
            isScoreShared.Value = GameRoot.Instance.configData.IsScoreShared;

            int[] totalScoreVisible = new int[GameRoot.Instance.configData.TotalScoreVisiblePerPlayer.Length];
            int[] revenueVisible = new int[GameRoot.Instance.configData.RevenueVisiblePerPlayer.Length];
            int[] deductionsVisible = new int[GameRoot.Instance.configData.DeductionsVisiblePerPlayer.Length];

            for (int i = 0; i < GameRoot.Instance.configData.TotalScoreVisiblePerPlayer.Length; i++)
            {
                totalScoreVisible[i] = GameRoot.Instance.configData.TotalScoreVisiblePerPlayer[i] ? 1 : 0;
                revenueVisible[i] = GameRoot.Instance.configData.RevenueVisiblePerPlayer[i] ? 1 : 0;
                deductionsVisible[i] = GameRoot.Instance.configData.DeductionsVisiblePerPlayer[i] ? 1 : 0;
            }

            RegisterScoreVisibility(totalScoreVisible, revenueVisible, deductionsVisible);
        }
    }
    
    public ulong GetNetworkIdOfWarehouse(int warehouseNum)
    {
        return warehouseNetworkObjectIds.Value.arr[warehouseNum];
    }

    public ulong GetNetworkIdOfDestination(int destinationNum)
    {
        return destinationNetworkObjectIds.Value.arr[destinationNum];
    }

    public ulong GetNetworkIdOfPlayer(int playerNum)
    {
        if (playerNum < 0 || playerNum > playerNetworkObjectIds.Value.arr.Length)
        {
            return 0;
        }
        
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

            destinationNetworkObjectIds.Value = new NetworkSerializableUlongArray();
            destinationNetworkObjectIds.Value.arr = new ulong[0];
            
            playerNetworkObjectIds.Value = new NetworkSerializableUlongArray();
            playerNetworkObjectIds.Value.arr = new ulong[0];
            
            totalScoreVisiblePerPlayer.Value = new NetworkSerializableIntArray( new int[0]);
            revenueVisiblePerPlayer.Value = new NetworkSerializableIntArray(new int[0]);
            deductionsVisiblePerPlayer.Value = new NetworkSerializableIntArray(new int[0]);

            RoadblockSystem.OnRoadblockActivate -= OnRoadblockActivate;
            RoadblockSystem.OnRoadblockActivate += OnRoadblockActivate;
            
            RoadblockSystem.OnRoadblockDeactivate -= OnRoadblockDeactivate;
            RoadblockSystem.OnRoadblockDeactivate += OnRoadblockDeactivate;
        }
        else
        {
        }
    }

    private void OnDisable()
    {
        RoadblockSystem.OnRoadblockActivate -= OnRoadblockActivate;
        RoadblockSystem.OnRoadblockDeactivate -= OnRoadblockDeactivate;
    }

    private void OnEnable()
    {
        if (this.IsServer)
        {
            RoadblockSystem.OnRoadblockActivate -= OnRoadblockActivate;
            RoadblockSystem.OnRoadblockActivate += OnRoadblockActivate;

            RoadblockSystem.OnRoadblockDeactivate -= OnRoadblockDeactivate;
            RoadblockSystem.OnRoadblockDeactivate += OnRoadblockDeactivate;
        }
    }

    //should only be called from server side
    private void OnRoadblockActivate(int roadblockNum)
    {
        if (IsServer)
        {
            //go in and activate all the tiles
            List<(int, int)> affectedTiles = RoadblockSystem.Instance.GetRoadblockAffectedTiles(roadblockNum);
            foreach ((int, int) affectedTile in affectedTiles)
            {
                GameObject tileObject = MapGenerator.Instance.map[affectedTile.Item1][affectedTile.Item2];

                MapNetworkBehaviour mapNetworkBehaviour = tileObject.GetComponentInChildren<MapNetworkBehaviour>();

                int duration = RoadblockSystem.Instance.GetRoadblockDuration(roadblockNum);
                mapNetworkBehaviour.DisableTileServerSide(duration);
                mapNetworkBehaviour.DisableTile_ClientRpc(RoadblockSystem.Instance.GetRoadblockInformedPlayer(roadblockNum), duration);
            }
        }
    }
    
    //should only be called from server side
    private void OnRoadblockDeactivate(int roadblockNum)
    {
        if (IsServer)
        {
            //go in and activate all the tiles
            List<(int, int)> affectedTiles = RoadblockSystem.Instance.GetRoadblockAffectedTiles(roadblockNum);
            foreach ((int, int) affectedTile in affectedTiles)
            {
                GameObject tileObject = MapGenerator.Instance.map[affectedTile.Item1][affectedTile.Item2];

                MapNetworkBehaviour mapNetworkBehaviour = tileObject.GetComponentInChildren<MapNetworkBehaviour>();
                
                mapNetworkBehaviour.EnableTileServerSide();
                mapNetworkBehaviour.EnableTile_ClientRpc(RoadblockSystem.Instance.GetRoadblockInformedPlayer(roadblockNum));
            }
        }
    }

    private void OverrideDestinationIconsFromDisk()
    {
        string path = Application.dataPath;
    
        if (Application.platform == RuntimePlatform.OSXPlayer) {
            path += "/../../";
        }
        else if (Application.platform == RuntimePlatform.WindowsPlayer) {
            path += "/../";
        }

        if (!Directory.Exists(path + DESTINATION_IMAGES_DIRECTORY_PATH))
        {
            return;
        }
        
        string[] filenames = Directory.GetFiles(path + DESTINATION_IMAGES_DIRECTORY_PATH);

        Array.Sort(filenames);

        List<Sprite> destinationSprites = new List<Sprite>();

        foreach (string file in filenames)
        {
            Sprite destinationSprite;
            if (ImageLoadingUtils.LoadImageAsSprite(file, out destinationSprite))
            {
                destinationSprites.Add(destinationSprite);
            }
        }

        int upperBound = Mathf.Min(destinationSprites.Count, destinationNetworkObjectIds.Value.arr.Length);
        for (int i = 0; i < upperBound; i++)
        {
            GameObject destinationObject =
                NetworkManager.Singleton.SpawnManager.SpawnedObjects[destinationNetworkObjectIds.Value.arr[i]].gameObject;
            
            destinationObject.GetComponentInChildren<SpriteRenderer>().sprite = destinationSprites[i];
        }
    }
    
    private void OverrideWarehouseIconsFromDisk()
    {
        string path = Application.dataPath;
    
        if (Application.platform == RuntimePlatform.OSXPlayer) {
            path += "/../../";
        }
        else if (Application.platform == RuntimePlatform.WindowsPlayer) {
            path += "/../";
        }
        
        if (!Directory.Exists(path + WAREHOUSE_IMAGES_DIRECTORY_PATH))
        {
            return;
        }

        string[] filenames = Directory.GetFiles(path + WAREHOUSE_IMAGES_DIRECTORY_PATH);

        Array.Sort(filenames);

        List<Sprite> warehouseSprites = new List<Sprite>();

        foreach (string file in filenames)
        {
            Sprite warehouseSprite;
            if (ImageLoadingUtils.LoadImageAsSprite(file, out warehouseSprite))
            {
                warehouseSprites.Add(warehouseSprite);
            }
        }

        int upperBound = Mathf.Min(warehouseSprites.Count, warehouseNetworkObjectIds.Value.arr.Length);
        for (int i = 0; i < upperBound; i++)
        {
            GameObject warehouseObject =
                NetworkManager.Singleton.SpawnManager.SpawnedObjects[warehouseNetworkObjectIds.Value.arr[i]].gameObject;
            
            warehouseObject.GetComponentInChildren<SpriteRenderer>().sprite = warehouseSprites[i];
        }
    }
    
    private void OverrideTruckIconsFromDisk()
    {
        string path = Application.dataPath;
    
        if (Application.platform == RuntimePlatform.OSXPlayer) {
            path += "/../../";
        }
        else if (Application.platform == RuntimePlatform.WindowsPlayer) {
            path += "/../";
        }
        
        if (!Directory.Exists(path + TRUCK_IMAGES_DIRECTORY_PATH))
        {
            return;
        }

        string[] filenames = Directory.GetFiles(path + TRUCK_IMAGES_DIRECTORY_PATH);

        Array.Sort(filenames);

        List<Sprite> truckSprites = new List<Sprite>();

        foreach (string file in filenames)
        {
            Sprite truckSprite;
            if (ImageLoadingUtils.LoadImageAsSprite(file, out truckSprite))
            {
                truckSprites.Add(truckSprite);
            }
        }

        int upperBound = Mathf.Min(truckSprites.Count, playerNetworkObjectIds.Value.arr.Length);
        
        for (int i = 0; i < upperBound; i++)
        {
            GameObject playerObject =
                NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerNetworkObjectIds.Value.arr[i]].gameObject;
            
            playerObject.GetComponentInChildren<SpriteRenderer>().sprite = truckSprites[i];
        }
    }

    public void RegisterWareHouseNetworkObjectIds(List<GameObject> warehouses)
    {
        warehouseNetworkObjectIds.Value.arr = new ulong[warehouses.Count];

        for (int i = 0; i < warehouses.Count; i++)
        {
            warehouseNetworkObjectIds.Value.arr[i] = warehouses[i].GetComponent<NetworkObject>().NetworkObjectId;
        }

        if (!Application.isEditor)
        {
            OverrideWarehouseIconsFromDisk();
        }
    }

    public bool IsTotalScoreVisiblePerPlayer(int playerNum)
    {
        return totalScoreVisiblePerPlayer.Value.arr[playerNum] != 0;
    }
    
    public bool IsRevenueVisiblePerPlayer(int playerNum)
    {
        return revenueVisiblePerPlayer.Value.arr[playerNum] != 0;
    }
    
    public bool IsDeductionsVisiblePerPlayer(int playerNum)
    {
        return deductionsVisiblePerPlayer.Value.arr[playerNum] != 0;
    }
    
  

    public void RegisterScoreVisibility(int[] totalScoreVisibility, int[] revenueVisibility,
        int[] deductionsVisibility)
    {
        totalScoreVisiblePerPlayer.Value = new NetworkSerializableIntArray(new int[deductionsVisibility.Length]);
        revenueVisiblePerPlayer.Value = new NetworkSerializableIntArray(new int[deductionsVisibility.Length]);
        deductionsVisiblePerPlayer.Value = new NetworkSerializableIntArray(new int[deductionsVisibility.Length]);

        for (int i = 0; i < totalScoreVisibility.Length; i++)
        {
            totalScoreVisiblePerPlayer.Value.arr[i] = totalScoreVisibility[i];
            revenueVisiblePerPlayer.Value.arr[i] = revenueVisibility[i];
            deductionsVisiblePerPlayer.Value.arr[i] = deductionsVisibility[i];
        }
    }

    public void RegisterDestinationNetworkObjectIds(List<GameObject> destinations)
    {
        destinationNetworkObjectIds.Value.arr = new ulong[destinations.Count];

        for (int i = 0; i < destinations.Count; i++)
        {
            destinationNetworkObjectIds.Value.arr[i] = destinations[i].GetComponent<NetworkObject>().NetworkObjectId;
        }
        
        if (!Application.isEditor)
        {
            OverrideDestinationIconsFromDisk();
        }
    }

    public void RegisterPlayerNetworkObjectIds(List<GameObject> players)
    {
        playerNetworkObjectIds.Value.arr = new ulong[players.Count];

        for (int i = 0; i < players.Count; i++)
        {
            playerNetworkObjectIds.Value.arr[i] = players[i].GetComponent<NetworkObject>().NetworkObjectId;
        }
        
        if (!Application.isEditor) {
            OverrideTruckIconsFromDisk();
        }
    }

    public void RegisterMapDimentions(int width, int height)
    {
        mapWidth.Value = width;
        mapHeight.Value = height;
    }
}