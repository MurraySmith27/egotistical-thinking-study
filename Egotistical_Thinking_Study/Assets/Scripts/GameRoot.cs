using System.Collections;
using System.Collections.Generic;
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

public class ConfigData
{
    public Warehouse[] Warehouses { get; set; }
    public Order[] Orders;
    public int MaxGasPerPlayer;
    public int[] CameraRotationPerPlayer;
    public int NumPlayers;
    public bool IsScoreShared;
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
    }
}
