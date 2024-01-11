using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerNetworkBehaviour : NetworkBehaviour
{
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
            NetworkManager.Singleton.OnClientConnectedCallback += Server_StartMovingGameObject;
        }
    }

    private void Server_StartMovingGameObject(ulong obj) {
        StartCoroutine(MoveGameObject());
    }

    private IEnumerator MoveGameObject() {
        var count = 0;
        var updateFrequency = new WaitForSeconds(0.5f);
        while (count < 4)
        {
            position.Value += new Vector3(0.1f, 0, 0);
            yield return updateFrequency;
        }
        NetworkManager.Singleton.OnClientConnectedCallback -= Server_StartMovingGameObject;
    }


}
