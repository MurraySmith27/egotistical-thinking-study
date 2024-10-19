using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Windows.Speech;

public struct Order
{
    public int RecievingPlayer;
    public int DestinationWarehouse;
    public Dictionary<string, int> RequiredItems;
    public string TextDescription;
    public int ScoreReward;
    public int IncompleteOrderPenalty;
    public int TimeLimitSeconds;
}


public struct Warehouse
{
    public Dictionary<string, int> Contents;
    public int PlayerOwner;
}

public struct Roadblock
{
    public int informedPlayer;
    public List<List<int>> blockedTiles;
    public int autoActivateOnOrder;
    public int autoDeactivateOnCompleteOrder;
    public int duration;
}

public class ConfigData
{
    public Warehouse[] Warehouses;
    public Warehouse[] Destinations;
    public Order[] Orders;
    public int MaxGasPerPlayer;
    public int[] CameraRotationPerPlayer;
    public int NumPlayers;
    public bool IsScoreShared;
    public int IncorrectItemPenalty;
    public int GasRefillCost;
    public int GameTimerSeconds;
    public int StartingMoneyPerPlayer;
    public int InventoryCapacityPerPlayer;
    public bool IsPlayerCollisionEnabled;
    public bool TrucksPathAroundEachOther;
    public List<Roadblock> Roadblocks;
}

public class GameRoot : MonoBehaviour
{
    private static GameRoot _instance;

    public static GameRoot Instance
    {
        get
        {
            return _instance;
        }
    }


    [SerializeField] private MapGenerator mapGenerator;
    private string[] map;

    public ConfigData configData { get; private set; }

    public bool isServer = false;


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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
    
    
    public void SetMap(string[] map_) {
        this.map = map_;
    }

    public void SetConfigData(ConfigData configData_)
    {
        this.configData = configData_;
    }
    
    public void OnStart() {
        this.mapGenerator.GenerateMap(this.map);

        InventorySystem.Instance.OnGameStart();

        OrderSystem.Instance.OnGameStart();

        MapDataNetworkBehaviour.Instance.OnGameStart();

        CameraNetworkBehaviour.Instance.OnGameStart();

        RoadblockSystem.Instance.OnGameStart();

        GameTimerSystem.Instance.OnGameStart();
    }

    public void ResetGame()
    {
        var objects = NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values;
        foreach (NetworkObject obj in objects.ToList())
        {
            obj.Despawn();
        }
    }
}
