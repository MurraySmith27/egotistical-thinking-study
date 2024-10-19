using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class MapNetworkBehaviour : NetworkBehaviour
{
    [SerializeField] private GameObject tileObject;
    [SerializeField] private GameObject tileDisabledObject;

    [SerializeField] private TMP_Text countdownTimerText;
    
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    private int currentCountdownValue = -1;
    private Coroutine countdownCoroutine;

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
        }
        else
        {
            ClientConnectionHandler.Instance.m_onRecieveClientSideSessionInfo += SetRotation;
        }
    }

    public void EnableTileServerSide()
    {
        if (tileObject != null)
        {
            tileObject.SetActive(true);
        }

        if (tileDisabledObject != null)
        {
            tileDisabledObject.SetActive(false);
        }
    }

    public void DisableTileServerSide()
    {
        if (tileObject != null)
        {
            tileObject.SetActive(false);
        }

        if (tileDisabledObject != null)
        {
            tileDisabledObject.SetActive(true);
        }
    }

    [ClientRpc]
    public void EnableTile_ClientRpc(int affectedPlayer)
    {
        if (affectedPlayer == ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum)
        {
            if (tileObject != null)
            {
                tileObject.SetActive(true);
            }

            if (tileDisabledObject != null)
            {
                tileDisabledObject.SetActive(false);
            }
        }
    }

    [ClientRpc]
    public void DisableTile_ClientRpc(int affectedPlayer, int duration)
    {
        if (affectedPlayer == ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum)
        {
            if (tileObject != null)
            {
                tileObject.SetActive(false);
            }

            if (tileDisabledObject != null)
            {
                tileDisabledObject.SetActive(true);
            }

            int prevCountdownValue = currentCountdownValue; 
            currentCountdownValue = Math.Max(currentCountdownValue, duration);
            
            if (currentCountdownValue != prevCountdownValue)
            {
                StopCoroutine(countdownCoroutine);
            }

            countdownCoroutine = StartCoroutine(RoadblockTimerCountdown());
        }
    }

    private IEnumerator RoadblockTimerCountdown()
    {
        if (countdownTimerText != null)
        {
            countdownTimerText.gameObject.SetActive(true);

            while (currentCountdownValue >= 0)
            {
                countdownTimerText.text = $"{currentCountdownValue}";
                currentCountdownValue--;
                yield return new WaitForSeconds(1);
            }

            countdownTimerText.gameObject.SetActive(false);
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
