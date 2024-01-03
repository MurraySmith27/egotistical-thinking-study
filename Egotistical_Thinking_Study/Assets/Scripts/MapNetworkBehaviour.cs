using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MapNetworkBehaviour : NetworkBehaviour
{
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            Debug.Log("Setting network variable!");
            position.Value = gameObject.transform.position;
        }
    }
}
