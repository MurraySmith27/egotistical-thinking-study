using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Networking.Transport;
using UnityEditor;

public class PlayerNetworkBehaviour : NetworkBehaviour
{
    
    
    [SerializeField] private InputActionAsset clientInput;
    [SerializeField] private float secondsPerGridMove;
    
    private InputAction clickAction;
    private InputAction mousePosition;
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    private Coroutine moveCoroutine;

    public int m_playerNum;
    
    void Awake() {
        clickAction = clientInput["mouseClick"];

        mousePosition = clientInput["mousePosition"];
    }

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
        }
        else if (this.IsClient) {
            clickAction.performed += OnClick; 
        }

        position.OnValueChanged += UpdatePosition;
    }
    
    public void OnClick(InputAction.CallbackContext context) {
        Vector2 mousePos = mousePosition.ReadValue<Vector2>();

        mousePos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

        //raycast from camera center, see if it intersects with the map.
        RaycastHit hit;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
        
        Debug.DrawRay(ray.origin, ray.origin + ray.direction * 100, color:Color.red, duration: 5f, false);
        if (Physics.Raycast(ray.origin, ray.direction, out hit, 100, ~LayerMask.NameToLayer("MapTile")))
        {
            Vector3 hitPos = hit.transform.position;

            if (hit.transform.gameObject.name.Contains("Road"))
            {
                int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
                MovePlayerTo_ServerRpc(hitPos, playerNum);
            }
        }
    }
    

    [ServerRpc(RequireOwnership=false)]
    void MovePlayerTo_ServerRpc(Vector3 worldPos,  int playerNum, ServerRpcParams serverRpcParams = default)
    {
        if (m_playerNum != playerNum)
        {
            return;
        }
        
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
        
        Vector2Int destinationPos = new((int)(worldPos.x / MapGenerator.Instance.tileWidth), (int)(worldPos.y / MapGenerator.Instance.tileHeight));
        Vector3 playerPos = transform.position;
        Vector2 playerPosVec2 = new((playerPos.x / MapGenerator.Instance.tileWidth),
            (playerPos.y / MapGenerator.Instance.tileHeight));
        Vector2Int playerGridPos = new((int)Math.Round(playerPosVec2.x),(int)Math.Round(playerPosVec2.y));

        List<Vector2Int> path = MapGenerator.Instance.NavigateRoads(playerGridPos, destinationPos);

        List<Vector2> vec2Path = new List<Vector2>(path.Select(obj => new Vector2(obj.x, obj.y)).ToArray());

        vec2Path[0] = playerPosVec2;
        

        moveCoroutine = StartCoroutine(MovePlayerAlongPath(vec2Path, playerNum));
    }

    private IEnumerator MovePlayerAlongPath(List<Vector2> path, int playerNum)
    {
        float playerZ = transform.position.z;

        Vector3 lastPosition = Vector3.zero;
        Vector3 nextPosition = new Vector3(path[0].x * MapGenerator.Instance.tileWidth,
            path[0].y * MapGenerator.Instance.tileHeight, playerZ);
        
        foreach (Vector2 destination in path.Skip(1))
        {

            lastPosition = nextPosition;
            nextPosition = new Vector3(destination.x * MapGenerator.Instance.tileWidth,
                destination.y * MapGenerator.Instance.tileHeight, playerZ);
            
            for (float t = 0; t < secondsPerGridMove; t += Time.deltaTime)
            {
                position.Value = Vector3.Lerp(lastPosition, nextPosition, t / secondsPerGridMove);
                yield return null;
            }
        }
    }

    private void UpdatePosition(Vector3 prev, Vector3 current) {
        gameObject.transform.position = position.Value;
    }
    

    private void OnEnable() {
        clickAction.Enable();
        mousePosition.Enable();
    }

    private void OnDisable() {
        clickAction.Disable();
        mousePosition.Disable();
    }


}
