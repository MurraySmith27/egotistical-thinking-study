using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerNetworkBehaviour : NetworkBehaviour
{
    public InputActionAsset clientInput;
    private InputAction clickAction;
    private InputAction mousePosition;
    private NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();

    
    void Awake() {
        clickAction = clientInput["mouseClick"];

        mousePosition = clientInput["mousePosition"];
    }

    public override void OnNetworkSpawn() {
        if (this.IsServer) {
            position.Value = gameObject.transform.position;
            NetworkManager.Singleton.OnClientConnectedCallback += Server_StartMovingGameObject;
        }
        else if (this.IsClient) {
            Debug.Log("Client setup");
            clickAction.performed += OnClick; 
        }

        position.OnValueChanged += UpdatePosition;
    }

    public void OnClick(InputAction.CallbackContext context) {
        Vector2 mousePos = mousePosition.ReadValue<Vector2>();
        Debug.Log($"on click called! mousePos: {mousePos}");

        mousePos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

        //raycast from camera center, see if it intersects with the map.
        RaycastHit hit;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
        Debug.Log($"ray origin: {ray.origin} ray direction: {ray.direction}");
        Debug.DrawRay(ray.origin, ray.origin + ray.direction * 100, color:Color.red, duration: 5f, false);
        if (Physics.Raycast(ray, out hit, 0, LayerMask.NameToLayer("MapTile"))) {
            Debug.Log($"Hit point: {hit.point}");
        }
    }

    private void UpdatePosition(Vector3 prev, Vector3 current) {
        gameObject.transform.position = position.Value;
    }

    private void Server_StartMovingGameObject(ulong obj) {
        StartCoroutine(MoveGameObject());
    }

    private IEnumerator MoveGameObject() {
        var count = 0;
        var updateFrequency = new WaitForSeconds(0.5f);
        while (count < 4)
        {
            position.Value += new Vector3(1f, 0, 0);
            yield return updateFrequency;
        }
        NetworkManager.Singleton.OnClientConnectedCallback -= Server_StartMovingGameObject;
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
