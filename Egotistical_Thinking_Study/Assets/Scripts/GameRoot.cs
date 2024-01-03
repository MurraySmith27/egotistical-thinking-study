using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoot : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    private string[] map;


    public void SetMap(string[] map_) {
        this.map = map_;
    }
    
    public void OnStart() {
        this.mapGenerator.GenerateMap(this.map);
    }
}
