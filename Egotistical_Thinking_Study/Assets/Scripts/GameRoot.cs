using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class ConfigData
{
    public int[][] WarehouseContents { get; set; }
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
    
    
    public void SetMap(string[] map_) {
        this.map = map_;
    }

    public void SetConfigData(ConfigData configData_)
    {
        this.configData = configData_;
        
        var warehouseContentsArray = configData.WarehouseContents;
        for (int i = 0; i < warehouseContentsArray.Length; i++)
        {
            for (int j = 0; j < warehouseContentsArray[i].Length; j++)
            {
                Debug.Log(warehouseContentsArray[i][j]);
            }
        }
    }
    
    public void OnStart() {
        this.mapGenerator.GenerateMap(this.map);
    }
}
