using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ClientManager : MonoBehaviour
{

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject clientMenu;

    public void StartClient(string address, int port) {

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
