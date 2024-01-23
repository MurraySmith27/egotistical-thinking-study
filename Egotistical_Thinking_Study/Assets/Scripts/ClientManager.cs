using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;


public class ClientManager : MonoBehaviour
{

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject clientMenu;

    public void StartClient(string address, int port) {
        
        //generate client-side guid in player prefs to identify machines
        Guid playerGUID;
        if (PlayerPrefs.HasKey("SessionGUID"))
        {
            playerGUID = new Guid(PlayerPrefs.GetString("SessionGUID"));
        }
        else
        {
            playerGUID = Guid.NewGuid();
            PlayerPrefs.SetString("SessionGUID", playerGUID.ToString());
        }

        byte[] playerGUIDbyteArr = playerGUID.ToByteArray();

        NetworkManager.Singleton.NetworkConfig.ConnectionData = playerGUIDbyteArr;
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(address, (ushort)port);
        

        bool success = NetworkManager.Singleton.StartClient();

        if (!success) {
            Debug.LogError("Could not create client!");
            return;
        }

        mainMenu.SetActive(false);

        clientMenu.SetActive(true);

    }
}
