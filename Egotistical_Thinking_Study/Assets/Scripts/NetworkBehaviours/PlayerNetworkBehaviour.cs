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
    public Color m_playerColor;
    
    [SerializeField] private InputActionAsset clientInput;
    [SerializeField] private float secondsPerGridMove;
    [SerializeField] private float m_gasRefillRadius = 8f;
    
    private InputAction clickAction;
    private InputAction mousePosition;
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    private Coroutine moveCoroutine;

    private GameObject playerCamera;

    private GameObject gameViewQuad; 

    public NetworkVariable<int> m_playerNum;

    public NetworkVariable<int> m_numGasRemaining;

    private List<GameObject> m_children;
    
    void Awake() {
        clickAction = clientInput["mouseClick"];

        mousePosition = clientInput["mousePosition"];

        m_playerNum = new NetworkVariable<int>();

        m_numGasRemaining = new NetworkVariable<int>();

        m_children = new();
    }

    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            m_children.Add(transform.GetChild(i).gameObject);
        }
    }

    void OnPositionChange(Vector3 newPosition)
    {
        if (this.IsServer)
        {
            foreach (GameObject gasStation in MapGenerator.Instance.gasStations)
            {
                if (Vector3.Distance(gasStation.transform.position,newPosition) < m_gasRefillRadius)
                {
                    m_numGasRemaining.Value = GameRoot.Instance.configData.MaxGasPerPlayer;
                    break;
                }
            }
        }
        else
        {
            LineRenderer lineRenderer = GetComponent<LineRenderer>();
            Vector3[] lineRendererPoints = new Vector3[lineRenderer.positionCount];
            lineRenderer.GetPositions(lineRendererPoints);

            var newLineRendererPoints = lineRendererPoints.Skip(1);
            
            lineRenderer.SetPositions(newLineRendererPoints.ToArray());
        }
    }
    
    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
            m_playerNum.Value = 0;

            m_numGasRemaining.Value = GameRoot.Instance.configData.MaxGasPerPlayer;
        }
        else if (this.IsClient) {
            clickAction.performed += OnClick;

            playerCamera = GameObject.FindGameObjectWithTag("PlayerCamera");
        }

        // gameViewQuad = GameObject.FindGameObjectWithTag("GameViewQuad");
        // gameViewQuad.GetComponent<MeshRenderer>().enabled = true;
        position.OnValueChanged += UpdatePosition;
    }
    
    public void OnClick(InputAction.CallbackContext context) {
        Vector2 mousePos = mousePosition.ReadValue<Vector2>();

        // mousePos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);
        
        // Vector2 topLeftCorner = Camera.main.WorldToScreenPoint(gameViewQuad.transform.GetChild(0).position);
        // Vector2 bottomRightCorner = Camera.main.WorldToScreenPoint(gameViewQuad.transform.GetChild(1).position);

        Vector2 topLeftCorner = new Vector2(0f, 0f);
        Vector2 bottomRightCorner = new Vector2(Screen.width / 2f,Screen.height);
        
        float width = bottomRightCorner.x - topLeftCorner.x;
        float height = bottomRightCorner.y - topLeftCorner.y;
        
        
        //raycast from camera center, see if it intersects with the map.
        RaycastHit hit;
        Camera playerCameraComponent = playerCamera.GetComponent<Camera>();
        Ray ray = playerCameraComponent.ViewportPointToRay(new Vector3((mousePos.x - topLeftCorner.x) / width, ((mousePos.y - topLeftCorner.y) / height), 0));
        
        Debug.DrawRay(ray.origin, ray.origin + ray.direction * 100, color:Color.red, duration: 5f, false);
        if (Physics.Raycast(ray.origin, ray.direction, out hit, 100, ~LayerMask.NameToLayer("MapTile")))
        {
            Vector3 hitPos = hit.transform.position;

            if (hit.transform.gameObject.name.Contains("Road"))
            {
                int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
                Vector2Int destinationPos = new((int)(hitPos.x / MapGenerator.Instance.tileWidth), (int)(hitPos.y / MapGenerator.Instance.tileHeight));
                MovePlayerTo_ServerRpc(destinationPos, playerNum);
            }
            else
            {
                Vector3 closestHitPos = Vector3.zero;
                float closestHitDistance = float.PositiveInfinity;
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        Vector3 newHitPos = ray.origin + new Vector3(i * MapGenerator.Instance.tileWidth,
                            j * MapGenerator.Instance.tileHeight, 0);
                        
                        Debug.DrawRay(newHitPos, ray.origin + ray.direction * 100, color:Color.red, duration: 5f, false);
                        RaycastHit hit2 = new();
                        if (Physics.Raycast(newHitPos, ray.direction, out hit2, 100,
                                ~LayerMask.NameToLayer("MapTile")))
                        {
                            if (hit2.transform.gameObject.name.Contains("Road"))
                            {
                                float distance = Vector3.Distance(hitPos, hit2.transform.position);
                                if (distance < closestHitDistance)
                                {
                                    closestHitPos = hit2.transform.position;
                                    closestHitDistance = distance;
                                }
                            }
                        }
                    }
                }

                if (closestHitDistance != float.PositiveInfinity)
                {
                    int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
                    Vector2Int destinationPos = new((int)(closestHitPos.x / MapGenerator.Instance.tileWidth), (int)(closestHitPos.y / MapGenerator.Instance.tileHeight));
                    MovePlayerTo_ServerRpc(destinationPos, playerNum);
                }
            }
        }
    }
    

    [ServerRpc(RequireOwnership=false)]
    void MovePlayerTo_ServerRpc(Vector2Int destinationPos,  int playerNum, ServerRpcParams serverRpcParams = default)
    {
        if (m_playerNum.Value != playerNum)
        {
            return;
        }
        
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
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

            if (m_numGasRemaining.Value == 0)
            {
                yield break;
            }
            
            
            lastPosition = nextPosition;
            nextPosition = new Vector3(destination.x * MapGenerator.Instance.tileWidth,
                destination.y * MapGenerator.Instance.tileHeight, playerZ);
            m_numGasRemaining.Value--;

            foreach (GameObject child in m_children)
            {
                child.SetActive(false);
            }

            if (nextPosition.x > lastPosition.x)
            {
                //moving right
                m_children[0].SetActive(true);
            }
            else if (nextPosition.x < lastPosition.x)
            {
                m_children[1].SetActive(true);
            }
            else if (nextPosition.y < lastPosition.y)
            {
                m_children[2].SetActive(true);
            }
            else if (nextPosition.y > lastPosition.y)
            {
                m_children[3].SetActive(true);
            }
            
            for (float t = 0; t < secondsPerGridMove; t += Time.deltaTime)
            {
                position.Value = Vector3.Lerp(lastPosition, nextPosition, t / secondsPerGridMove);
                
                yield return null;
            }

            OnPositionChange(nextPosition);
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
