using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ServerManager : MonoBehaviour
{
    
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject serverControlMenu;

    [SerializeField] private MapGenerator mapGenerator;

    public void StartServer(string address, int port) {
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(address, (ushort)port);

        NetworkManager.Singleton.ConnectionApprovalCallback = ClientConnectionHandler.Instance.Server_ApproveConnection;
        
        
        bool success = NetworkManager.Singleton.StartServer();
        
        if (!success) {
            Debug.LogError("Could not start server!");
            return;
        }

        mainMenu.SetActive(false);
        serverControlMenu.SetActive(true);
    }
}
