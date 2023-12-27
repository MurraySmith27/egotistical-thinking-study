using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class ServerManager : MonoBehaviour
{
    
    [SerializeField] private UIDocument mainMenu;
    [SerializeField] private UIDocument serverControlMenu;

    [SerializeField] private MapGenerator mapGenerator;

    public void StartServer() {
        NetworkManager.Singleton.StartServer(); 

        mainMenu.enabled = false;
        serverControlMenu.enabled = true;
    }
}
