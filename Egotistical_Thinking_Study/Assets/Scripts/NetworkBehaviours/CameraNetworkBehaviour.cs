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
            SetCameraRotation();
        }
    }

    private void SetCameraRotation()
    {
        int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

        Debug.Log($"rotating. value: {playerNum}");
        int rotation = cameraRotationPerPlayer.Value.arr[playerNum];

        float yawRotation = rotation * 90f;

        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType(typeof(GameObject)))
        {
            if (go != this.gameObject && go.activeInHierarchy)
            {
                go.transform.rotation = Quaternion.Euler(0, 0, yawRotation);
            }
        }

    }

    public void OnGameStart()
    {
        cameraRotationPerPlayer.Value = new NetworkSerializableIntArray();
        cameraRotationPerPlayer.Value.arr = GameRoot.Instance.configData.CameraRotationPerPlayer;
        Debug.Log("settingaaaAAA");
    }
}
