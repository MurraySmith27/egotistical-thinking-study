using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MapNetworkBehaviour : NetworkBehaviour
{
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
        }
        else
        {
            ClientConnectionHandler.Instance.m_onRecieveClientSideSessionInfo += SetRotation;
        }
    }

    private void SetRotation()
    {
        if (!gameObject.name.Contains("Road"))
        {
            int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

            int rotation = CameraNetworkBehaviour.Instance.cameraRotationPerPlayer.Value.arr[playerNum];

            float yawRotation = rotation * 90f;
            transform.rotation = Quaternion.Euler(0, 0, yawRotation);
        }
    }
}
