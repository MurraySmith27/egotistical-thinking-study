using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class MapGenerator : MonoBehaviour
{
    private static MapGenerator _instance;
    public static MapGenerator Instance { get {return _instance;} }
    
    [SerializeField] public float tileWidth = 7f;
    [SerializeField] public float tileHeight = 7f;

    [SerializeField] private char roadLetter = 'R';
    [SerializeField] private char playerLetter = 'P';
    [SerializeField] private List<GameObject> playerPrefabs;
    [SerializeField] private List<char> tileNames = new List<char>();
    [SerializeField] private List<GameObject> tilePrefabs = new List<GameObject>();

    [SerializeField] private List<char> roadConnectionLettersNorth;

    [SerializeField] private List<char> roadConnectionLettersEast;

    [SerializeField] private List<char> roadConnectionLettersSouth;

    [SerializeField] private List<char> roadConnectionLettersWest;

    [SerializeField] private List<char> warehouseLetters;

    private Dictionary<char, GameObject> nameToPrefabMap;

    public List<List<GameObject>> map;

    public List<List<bool>> roadMap;

    public Vector2Int gridOrigin;

    public List<GameObject> playerObjects;

    public int numWarehouses { 
        get {
            return warehouses.Count;
        }
    }

    public List<GameObject> warehouses;
    
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

    void Start() {
        nameToPrefabMap = new Dictionary<char, GameObject>();

        for (int i = 0; i < tileNames.Count; i++)
        {
            nameToPrefabMap.Add(tileNames[i], tilePrefabs[i]);
        }
    }

    public void GenerateMap(string[] textMap)
    {
        map = new List<List<GameObject>>(textMap[0].Length);
        roadMap = new List<List<bool>>(textMap[0].Length);
        playerObjects = new List<GameObject>();
        for (int col = 0; col < textMap[0].Length; col++)
        {
            map.Add(new List<GameObject>(textMap.Length));
            roadMap.Add(new List<bool>(textMap.Length));
            for (int row = 0; row < textMap.Length; row++)
            {
                bool isRoad = false;
                if (nameToPrefabMap.ContainsKey(textMap[row][col])) {
                    GameObject go = Instantiate(nameToPrefabMap[textMap[row][col]], new Vector3(col * tileWidth, -row * tileHeight, 0), Quaternion.identity);
                    
                    go.GetComponent<NetworkObject>().Spawn();
                    map[col].Add(go);

                    if (warehouseLetters.Contains(textMap[row][col]))
                    {
                        warehouses.Add(go);
                    }
                }
                else if (textMap[row][col] == roadLetter || textMap[row][col] == playerLetter) {
                    string roadConnectionsSuffix = "";
                    if (row != 0 && roadConnectionLettersNorth.Contains(textMap[row - 1][col])) {
                        roadConnectionsSuffix += "N";
                    }
                    if (col != textMap[row].Length - 1 && roadConnectionLettersEast.Contains(textMap[row][col + 1])) {
                        roadConnectionsSuffix += "E";
                    }
                    if (row != textMap.Length - 1 && roadConnectionLettersSouth.Contains(textMap[row + 1][col])) {
                        roadConnectionsSuffix += "S";
                    }
                    if (col != 0 && roadConnectionLettersWest.Contains(textMap[row][col - 1])) {
                        roadConnectionsSuffix += "W";
                    }

                    Object roadPrefab = Resources.Load("Prefabs/MapItems/Roads/Road" + roadConnectionsSuffix);

                    if (roadPrefab == null) {
                        Debug.LogError("Road Failed to load!");
                    }
                    else {
                        GameObject go = Instantiate((GameObject)roadPrefab, new Vector3(col * tileWidth, -row * tileHeight, 0), Quaternion.identity);

                        go.GetComponent<NetworkObject>().Spawn();
                        map[col].Add(go);
                    }

                    if (textMap[row][col] == playerLetter) {
                        GameObject go = Instantiate(playerPrefabs[playerObjects.Count], new Vector3(col * tileWidth, -row * tileHeight, -1), Quaternion.identity);
                        
                        go.GetComponent<NetworkObject>().Spawn();
                        
                        go.GetComponent<PlayerNetworkBehaviour>().m_playerNum.Value = playerObjects.Count;
                        playerObjects.Add(go);
                    }
                    
                    isRoad = true;
                }
                else
                {
                    map[col].Add(null);
                }
                roadMap[col].Add(isRoad);
            }
        }
        
        MapDataNetworkBehaviour.Instance.RegisterWareHouseNetworkObjectIds(warehouses);
        MapDataNetworkBehaviour.Instance.RegisterPlayerNetworkObjectIds(playerObjects);
    }

    private Dictionary<Vector2Int, List<Vector2Int>> ConstructGraphFromMap()
    {
        Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();

        for (int x = 0; x < map.Count; x++)
        {
            for (int y = 0; y < map[x].Count; y++)
            {
                if (roadMap[x][y])
                {
                    Vector2Int location = new(x, y);
                    graph.Add(location, new List<Vector2Int>());
                    if (x != 0 && roadMap[x-1][y])
                    {
                        graph[location].Add(new(x-1, y));
                    }
                    if (x != map.Count - 1 && roadMap[x+1][y])
                    {
                        graph[location].Add(new(x+1, y));
                    }
                    if (y != 0 && roadMap[x][y-1])
                    {
                        graph[location].Add(new(x, y-1));
                    }
                    if (y != map[x].Count - 1 && roadMap[x][y+1])
                    {
                        graph[location].Add(new(x, y+1));
                    }
                }
            }
        }

        return graph;
    }
    
    private Vector2Int MinDistance(Dictionary<Vector2Int, int> dist, Dictionary<Vector2Int, bool> sptSet)
    {
        // Initialize min value
        int min = int.MaxValue;
        Vector2Int minIndex = new(-1, -1);


        foreach (Vector2Int location in dist.Keys)
        {
            if (sptSet[location] == false && dist[location] <= min)
            {
                min = dist[location];
                minIndex = location;
            }
        }

        return minIndex;
    }

    public List<Vector2Int> NavigateRoads(Vector2Int startingPos, Vector2Int endingPos)
    {
        //the grid instantiates with -y increasing. need to correct for this.
        startingPos = new Vector2Int(startingPos.x, -startingPos.y);
        endingPos = new Vector2Int(endingPos.x, -endingPos.y);
        
        Dictionary<Vector2Int, List<Vector2Int>> graph = this.ConstructGraphFromMap();
        
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Dictionary<Vector2Int, bool> shortestPathSet = new Dictionary<Vector2Int, bool>();

        Dictionary<Vector2Int, List<Vector2Int>> shortestPaths = new Dictionary<Vector2Int, List<Vector2Int>>();

        for (int x = 0; x < map.Count; x++)
        {
            for (int y = 0; y < map[x].Count; y++)
            {
                if (roadMap[x][y])
                {
                    distances.Add(new(x,y), int.MaxValue);
                    shortestPathSet.Add(new(x, y), false);
                }
            }
        }

        distances[startingPos] = 0;
        shortestPaths[startingPos] = new List<Vector2Int>(new Vector2Int[]{startingPos});

        foreach (Vector2Int pos in graph.Keys)
        {
            Vector2Int minDistLocation = MinDistance(distances, shortestPathSet);
            
            shortestPathSet[minDistLocation] = true;

            foreach (Vector2Int location in graph.Keys)
            {
                if (!shortestPathSet[location] &&
                    graph[minDistLocation].Contains(location) &&
                    distances[minDistLocation] != int.MaxValue &&
                    distances[minDistLocation] + 1 < distances[location])
                {
                    distances[location] = distances[minDistLocation] + 1;
                    List<Vector2Int> newPath = new List<Vector2Int>(shortestPaths[minDistLocation]);
                    newPath.Add(location);
                    shortestPaths[location] = newPath;
                }
            }
        }

        List<Vector2Int> shortestPath = shortestPaths[endingPos];
        //now we have shortest paths to each node from starting node.
        //adjust again for -y
        for (int i = 0; i < shortestPath.Count; i++)
        {
            shortestPath[i] = new Vector2Int(shortestPath[i].x, -shortestPath[i].y);
        } 
        return shortestPath;
    }
}
