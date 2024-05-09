using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Unity.Netcode;

public delegate void RecieveClientSideSessionInfoEvent();

public class ClientConnectionHandler : NetworkBehaviour
{
    private static ClientConnectionHandler _instance;
    public static ClientConnectionHandler Instance
    {
        get { return _instance; }
    }

    public RecieveClientSideSessionInfoEvent m_onRecieveClientSideSessionInfo;

    public int m_numConnectedClients = 0;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }
    
    public struct PlayerSessionInfo
    {
        public int playerNum;
        public ulong clientId;
    }
    
    public Dictionary<Guid, PlayerSessionInfo> serverSideClientList;

    public PlayerSessionInfo clientSideSessionInfo;
    
    public bool m_clienSideSessionInfoReceived = false;
    
    public override void OnNetworkSpawn()
    {
        if (this.IsServer)
        {
            serverSideClientList = new Dictionary<Guid, PlayerSessionInfo>();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        foreach (Guid guid in serverSideClientList.Keys)
        {
            if (serverSideClientList[guid].clientId == clientId)
            {
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { clientId }
                    }
                };
                RecievePlayerSessionInfo_ClientRpc(serverSideClientList[guid].playerNum, clientRpcParams);
                break;
            }
        }
    }
    
    [ClientRpc]
    private void RecievePlayerSessionInfo_ClientRpc(int playerNum, ClientRpcParams clientRpcParams = default)
    {

        m_clienSideSessionInfoReceived = true;
        clientSideSessionInfo = new PlayerSessionInfo();
        clientSideSessionInfo.playerNum = playerNum;
        clientSideSessionInfo.clientId = NetworkManager.Singleton.LocalClientId;

        if (m_onRecieveClientSideSessionInfo != null && m_onRecieveClientSideSessionInfo.GetInvocationList().Length > 0) {
            m_onRecieveClientSideSessionInfo();
        }
    }
    
    public void Server_ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;
        
        //payload should only contain the GUID generated by the connecting player.
        Guid playerSessionGuid = new Guid(request.Payload);
        PlayerSessionInfo sessionInfo;
        if (serverSideClientList.ContainsKey(playerSessionGuid))
        {
            sessionInfo = serverSideClientList[playerSessionGuid];
            sessionInfo.clientId = clientId;
        }
        else
        {
            sessionInfo.playerNum = serverSideClientList.Keys.Count;
            sessionInfo.clientId = clientId;
            serverSideClientList.Add(playerSessionGuid, sessionInfo);
            m_numConnectedClients++;
        }
        
        response.Approved = true;
        response.CreatePlayerObject = false;
    }
    
}
