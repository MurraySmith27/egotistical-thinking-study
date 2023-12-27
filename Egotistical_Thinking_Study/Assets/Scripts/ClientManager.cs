using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
public class ClientManager : MonoBehaviour
{

    [SerializeField] UIDocument mainMenu;
    public void StartClient() {
        NetworkManager.Singleton.StartClient();

        //TODO: Activate client game view

        

    }
}
