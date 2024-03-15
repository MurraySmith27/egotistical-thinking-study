using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CameraNetworkBehaviour : NetworkBehaviour
{

    private GameObject m_trackingObject;
    public override void OnNetworkSpawn()
    {
        if (this.IsServer)
        {
            
            
        }
        else
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

        foreach (PlayerNetworkBehaviour playerNetworkBehaviour in FindObjectsOfType<PlayerNetworkBehaviour>())
        {
            if (playerNetworkBehaviour.m_playerNum.Value == playerNum)
            {
                m_trackingObject = playerNetworkBehaviour.gameObject;
                break;
            }
        }
    }

    void Update()
    {
        if (m_trackingObject != null)
        {
            transform.position = new Vector3(m_trackingObject.transform.position.x + MapGenerator.Instance.tileWidth / 2f,
                m_trackingObject.transform.position.y - MapGenerator.Instance.tileHeight / 2f, transform.position.z);
        }
    }
}
