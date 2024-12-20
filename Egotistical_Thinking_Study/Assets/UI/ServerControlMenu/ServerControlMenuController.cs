using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SimpleFileBrowser;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ServerControlMenuController : MonoBehaviour
{

    [SerializeField] private AudioSource m_mouseClickSFX;
    [SerializeField] private GameRoot gameRoot;

    [SerializeField] private GameObject experimenterView;

    private Label mapFileLabel;
    private Button loadMapFileButton;
    private Button loadConfigFileButton;
    private Button startGameButton;

    private bool configFileLoaded = false;
    private bool mapFileLoaded = false;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        mapFileLabel = root.Q<Label>("map-file-label");

        loadMapFileButton = root.Q<Button>("load-map-file-button");

        loadConfigFileButton = root.Q<Button>("load-config-file-button");
        
        startGameButton = root.Q<Button>("start-game-button");

        loadMapFileButton.clicked += LoadMapFile;

        loadConfigFileButton.clicked += LoadConfigFile;
        
        startGameButton.clicked += OnGameStart;
        
        if (ServerManager.m_reset)
        {
            ServerManager.m_reset = false;
            
            SetMapFile(new string[]{ServerManager.m_mapFilePath});
            SetConfigFile(new string[]{ServerManager.m_configFilePath});

            OnGameStart();
        }

    }

    void OnGameStart()
    {
        m_mouseClickSFX.Play();
        gameRoot.OnStart();
        experimenterView.SetActive(true);
        this.gameObject.SetActive(false);
    }

    void LoadMapFile() {
        m_mouseClickSFX.Play();
        mapFileLabel.text = "loading dialogue";
        FileBrowser.ShowLoadDialog(SetMapFile, () => {}, FileBrowser.PickMode.FilesAndFolders, false, null, null, "Load Files and Folders", "Load" );
    }

    void LoadConfigFile()
    {
        m_mouseClickSFX.Play();
        mapFileLabel.text = "loading dialogue";
        FileBrowser.ShowLoadDialog(SetConfigFile, () => {}, FileBrowser.PickMode.FilesAndFolders, false, null, null, "Load Files and Folders", "Load" );
    }
    
    void SetMapFile(string[] paths) {
        string path = paths[0];

        ServerManager.m_mapFilePath = path;

        //load in text file at that path and set the map.

        string[] lines = File.ReadAllLines(path);

        //make sure it's valid
        for (int i = 0; i < lines.Length; i++) {
            if (lines[i].Length != lines[0].Length) {
                mapFileLabel.text = "Error: Invalid map file (lines are inconsitent lengths)";
            }
        }

        int lastSlashLocation = path.LastIndexOf("/");

        mapFileLabel.style.backgroundColor = Color.clear;
        
        mapFileLabel.text = "Map File: " + path.Substring(lastSlashLocation + 1, path.Length - lastSlashLocation - 1);

        mapFileLoaded = true;

        if (configFileLoaded)
        {
            SetLevelReadyToGoText();
        }

        gameRoot.SetMap(lines);
    }

    void SetConfigFile(string[] paths)
    {
        string path = paths[0];

        ServerManager.m_configFilePath = path;
        
        StreamReader sr = new StreamReader(path);

        string json = sr.ReadToEnd();

        ConfigData configData = JsonConvert.DeserializeObject<ConfigData>(json);
        
        //
        // int[][] wc = warehouseContents.Value<int[][]>("WarehouseContents");
        //
        // configData.WarehouseContents = new List<List<int>>();
        //
        // for (int i = 0; i < wc.Length; i++)
        // {
        //     configData.WarehouseContents.Add(new List<int>());
        //     for (int j = 0; j < wc[i].Length; j++)
        //     {
        //         configData.WarehouseContents[i].Add(wc[i][j]);
        //     }
        // }
        
        int lastSlashLocation = path.LastIndexOf("/");
        
        mapFileLabel.style.backgroundColor = Color.clear;
        
        mapFileLabel.text = "Config File: " + path.Substring(lastSlashLocation + 1, path.Length - lastSlashLocation - 1);

        configFileLoaded = true;

        if (mapFileLoaded)
        {
            SetLevelReadyToGoText();
        }
        
        gameRoot.SetConfigData(configData);
    }

    private void SetLevelReadyToGoText()
    {
        mapFileLabel.style.backgroundColor = Color.green;
        mapFileLabel.text = "Game Ready";
    }
}
