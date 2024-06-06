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

    public static string m_ipAddress;
    public static int m_port;
    public static string m_mapFilePath;
    public static string m_configFilePath;

    public static bool m_reset = false;

    public void StartServer(string address, int port)
    {
        m_ipAddress = address;
        m_port = port;
        
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(address, (ushort)port);

        NetworkManager.Singleton.ConnectionApprovalCallback = ClientConnectionHandler.Instance.Server_ApproveConnection;
        
        
        bool success = NetworkManager.Singleton.StartServer();
        
        if (!success) {
            Debug.LogError("Could not start server!");
            return;
        }

        mainMenu.SetActive(false);
        serverControlMenu.SetActive(true);

        
        VisualElement rootServerMenuElement = serverControlMenu.GetComponent<UIDocument>().rootVisualElement;
        if (rootServerMenuElement != null)
        {
            rootServerMenuElement.Q<Label>("ip-text").text = $"IP: {address}";
            rootServerMenuElement.Q<Label>("port-text").text = $"Port: {port}";
        }
    }
}
