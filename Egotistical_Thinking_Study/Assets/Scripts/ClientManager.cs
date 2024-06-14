using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine.SceneManagement;


public class ClientManager : MonoBehaviour
{

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject clientMenu;
    [SerializeField] private GameObject clientConnectionIntermediateMenu;

    public bool m_testing = true;

    public static string m_ipAddress;
    public static int m_port;

    public void StartClient(string address, int port)
    {
        Guid playerGUID;
        if (m_testing)
        {
            playerGUID = Guid.NewGuid();
        }
        else
        {
            //generate client-side guid in player prefs to identify machines
            if (PlayerPrefs.HasKey("SessionGUID"))
            {
                playerGUID = new Guid(PlayerPrefs.GetString("SessionGUID"));
            }
            else
            {
                playerGUID = Guid.NewGuid();
                PlayerPrefs.SetString("SessionGUID", playerGUID.ToString());
            }
        }

        byte[] playerGUIDbyteArr = playerGUID.ToByteArray();

        NetworkManager.Singleton.NetworkConfig.ConnectionData = playerGUIDbyteArr;
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(address, (ushort)port);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
        

        bool success = NetworkManager.Singleton.StartClient();

        if (!success) {
            Debug.LogError("Could not create client!");
            OnDisconnected(0);
            return;
        }

        mainMenu.SetActive(false);

        clientConnectionIntermediateMenu.SetActive(true);

        NetworkManager.Singleton.OnClientConnectedCallback += obj => { clientMenu.SetActive(true); clientConnectionIntermediateMenu.SetActive(false); MapDataNetworkBehaviour.Instance.OnGameStart();};
    
    }

    public void OnDisconnected(ulong clientId)
    {
        
        foreach (GameObject obj in FindObjectsOfType(typeof(GameObject)))
        {
            if (obj != null && obj.name != this.gameObject.name)
            {
                DestroyImmediate(obj);
            }
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        DestroyImmediate(NetworkManager.Singleton.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
