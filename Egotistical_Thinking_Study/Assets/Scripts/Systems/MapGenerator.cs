using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode; 

public class MapGenerator : MonoBehaviour
{   
    [SerializeField] private float tileWidth = 7f;
    [SerializeField] private float tileHeight = 7f;

    [SerializeField] private char roadLetter = 'R';
    [SerializeField] private char playerLetter = 'P';
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<char> tileNames = new List<char>();
    [SerializeField] private List<GameObject> tilePrefabs = new List<GameObject>();

    [SerializeField] private List<char> roadConnectionLettersNorth;

    [SerializeField] private List<char> roadConnectionLettersEast;

    [SerializeField] private List<char> roadConnectionLettersSouth;

    [SerializeField] private List<char> roadConnectionLettersWest;

    private Dictionary<char, GameObject> nameToPrefabMap;

    void Start() {
        nameToPrefabMap = new Dictionary<char, GameObject>();

        for (int i = 0; i < tileNames.Count; i++) {
            nameToPrefabMap.Add(tileNames[i], tilePrefabs[i]);
        }

    }

    public void GenerateMap(string[] textMap) {
        for (int col = 0; col < textMap.Length; col++) {
            for (int row = 0; row < textMap[0].Length; row++) {
                if (nameToPrefabMap.ContainsKey(textMap[row][col])) {
                    GameObject go = Instantiate(nameToPrefabMap[textMap[row][col]], new Vector3(col * tileWidth, -row * tileHeight, 0), Quaternion.identity);

                    go.GetComponent<NetworkObject>().Spawn();
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

                    Object roadPrefab = Resources.Load("Prefabs/Roads/Road" + roadConnectionsSuffix);

                    if (roadPrefab == null) {
                        Debug.LogError("Road Failed to load!");
                    }
                    else {
                        GameObject go = Instantiate((GameObject)roadPrefab, new Vector3(col * tileWidth, -row * tileHeight, 0), Quaternion.identity);

                        go.GetComponent<NetworkObject>().Spawn();
                    }

                    if (textMap[row][col] == playerLetter) {
                        GameObject go = Instantiate(playerPrefab, new Vector3(col * tileWidth, -row * tileHeight, -1), Quaternion.identity);

                        go.GetComponent<NetworkObject>().Spawn();
                    }

                }
            }
        }
    }
}
