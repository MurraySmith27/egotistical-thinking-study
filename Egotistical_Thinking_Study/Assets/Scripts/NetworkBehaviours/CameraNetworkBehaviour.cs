using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CameraNetworkBehaviour : NetworkBehaviour
{
    private static CameraNetworkBehaviour _instance;

    public static CameraNetworkBehaviour Instance
    {
        get
        {
            return _instance;
        }
    }


    public NetworkVariable<NetworkSerializableIntArray> cameraRotationPerPlayer = new NetworkVariable<NetworkSerializableIntArray>();

    private void Awake()
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


    public override void OnNetworkSpawn()
    {
        if (!this.IsServer)
        {
            ClientConnectionHandler.Instance.m_onRecieveClientSideSessionInfo += SetCameraRotation;

        }
        SetCameraXPos(0, MapDataNetworkBehaviour.Instance.mapWidth.Value);
        SetCameraYPos(0, MapDataNetworkBehaviour.Instance.mapHeight.Value);
        
        MapDataNetworkBehaviour.Instance.mapWidth.OnValueChanged += SetCameraXPos;
        MapDataNetworkBehaviour.Instance.mapHeight.OnValueChanged += SetCameraYPos;
    }

    private void SetCameraRotation()
    {
        int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

        int rotation = cameraRotationPerPlayer.Value.arr[playerNum];

        float yawRotation = rotation * 90f;
        transform.rotation = Quaternion.Euler(0, 0, yawRotation);
    }

    private void SetCameraXPos(int oldWidth, int width)
    {
        transform.position = new Vector3((width / 2f) * MapGenerator.Instance.tileWidth, transform.position.y, transform.position.z);
    }
    
    private void SetCameraYPos(int oldHeight, int height)
    {
        transform.position = new Vector3(transform.position.x, -(height / 2f) * MapGenerator.Instance.tileHeight, transform.position.z);
    }

    public void OnGameStart()
    {
        cameraRotationPerPlayer.Value = new NetworkSerializableIntArray();
        cameraRotationPerPlayer.Value.arr = GameRoot.Instance.configData.CameraRotationPerPlayer;
    }
}
