using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Networking.Transport;
using UnityEditor;

public class PlayerNetworkBehaviour : NetworkBehaviour
{
    public InputActionAsset clientInput;
    private InputAction clickAction;
    private InputAction mousePosition;
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    private List<GUID> serverSideClientList;

    
    void Awake() {
        clickAction = clientInput["mouseClick"];

        mousePosition = clientInput["mousePosition"];
    }

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
            serverSideClientList = new List<GUID>();
            NetworkManager.Singleton.OnClientConnectedCallback += Server_RegisterClient;
            NetworkManager.Singleton.OnClientDisconnectCallback += Server_DeregisterClient;
        }
        else if (this.IsClient) {
            clickAction.performed += OnClick; 
        }

        position.OnValueChanged += UpdatePosition;
    }

    private void Server_RegisterClient(ulong clientId)
    {
        
    }

    private void Server_DeregisterClient(ulong clientId)
    {
        
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
            
            
        }
    }
    

    [ServerRpc]
    void MovePlayerTo()
    {
        
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
