using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{

    [SerializeField] private List<char> tileNames = new List<char>();
    [SerializeField] private List<GameObject> tilePrefabs = new List<GameObject>();
    private Dictionary<char, GameObject> nameToPrefabMap;

    void Start() {
        nameToPrefabMap = new Dictionary<char, GameObject>();

        for (int i = 0; i < tileNames.Count; i++) {
            nameToPrefabMap.Add(tileNames[i], tilePrefabs[i]);
        }
    }

    public void GenerateMap(char[][] textMap) {
        for (int col = 0; col < textMap.Length; col++) {
            for (int row = 0; row < textMap[0].Length; row++) {
                if (nameToPrefabMap.ContainsKey(textMap[col][row])) {
                    Instantiate(nameToPrefabMap[textMap[col][row]], new Vector3(col, 0, row), Quaternion.identity);
                }
            }
        }
    }
}
