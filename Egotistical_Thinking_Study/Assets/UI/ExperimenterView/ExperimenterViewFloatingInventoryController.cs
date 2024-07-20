using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ExperimenterViewFloatingInventoryController : MonoBehaviour
{

    [SerializeField] private int inventorySlotSizePixels = 64;

    [SerializeField] private int numItemsPerRow = 8;
    
    [SerializeField] private InputActionAsset experimenterInput;

    [SerializeField] private Transform playerCamera;

    [SerializeField] private AudioSource mouseClickSFX;

    private VisualElement rootVisualElement;

    private VisualElement inventorySlotContainer;

    private VisualElement inventoryRootElement;

    private List<InventorySlot> _inventorySlots;
    
    private InputAction clickAction;
    private InputAction mousePosition;


    void Awake()
    {
        _inventorySlots = new List<InventorySlot>();
        
        clickAction = experimenterInput["mouseClick"];
        mousePosition = experimenterInput["mousePosition"];
    }
    
    void Start()
    {
        rootVisualElement = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("root");

        inventoryRootElement = rootVisualElement.Q<VisualElement>("floating-inventory-popup");
        inventorySlotContainer = inventoryRootElement.Q<VisualElement>("slot-container");

        clickAction.performed += OnClick;
    }
    
    private void OnEnable() {
        clickAction.Enable();
        mousePosition.Enable();
    }

    private void OnDisable() {
        clickAction.Disable();
        mousePosition.Disable();
    }

    void OnDestroy()
    {
        clickAction.performed -= OnClick;
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        Vector2 mousePos = mousePosition.ReadValue<Vector2>();
        
        Vector2 topLeftCorner = new Vector2(Screen.width * 0.3f - 96, 0f);
        Vector2 bottomRightCorner = new Vector2(Screen.width,Screen.height * 0.75f);
        
        float width = bottomRightCorner.x - topLeftCorner.x;
        float height = bottomRightCorner.y - topLeftCorner.y;
        
        Vector2 viewportPoint = new Vector2((mousePos.x - topLeftCorner.x) / width, (mousePos.y - topLeftCorner.y) / height);
        
        //raycast from camera center, see if it intersects with the map.
        RaycastHit hit;
        Camera playerCameraComponent = playerCamera.GetComponent<Camera>();
        Ray ray = playerCameraComponent.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0));
        
        Debug.Log($"drawing ray from: {ray.origin} to {ray.origin + ray.direction * 100}");
        Debug.DrawRay(ray.origin, ray.origin + ray.direction * 100, color:Color.red, duration: 5f, false);
        if (Physics.Raycast(ray.origin, ray.direction, out hit, 100, LayerMask.GetMask(new string[]{"MapTile", "Player"})))
        {
            Debug.Log($"HIT! name: {hit.transform.name}");

            InventoryNetworkBehaviour inventoryNetworkBehaviour =
                hit.transform.gameObject.GetComponent<InventoryNetworkBehaviour>(); 
            if (inventoryNetworkBehaviour != null)
            {
                mouseClickSFX.Play();
                
                int inventoryNum = -1;
                InventoryType inventoryType = InventoryType.Destination;
                if (hit.transform.gameObject.name.Contains("Destination"))
                {
                    inventoryType = InventoryType.Destination;
                    for (int i = 0; i < MapGenerator.Instance.destinations.Count; i++)
                    {
                        if (MapGenerator.Instance.destinations[i].name == hit.transform.name)
                        {
                            inventoryNum = i;
                            break;
                        }
                    }
                }
                else if (hit.transform.gameObject.name.Contains("Warehouse"))
                {
                    inventoryType = InventoryType.Warehouse;
                    for (int i = 0; i < MapGenerator.Instance.warehouses.Count; i++)
                    {
                        if (MapGenerator.Instance.warehouses[i].name == hit.transform.name)
                        {
                            inventoryNum = i;
                            break;
                        }
                    }
                }
                else if (hit.transform.gameObject.name.Contains("Player"))
                {
                    inventoryType = InventoryType.Player;
                    inventoryNum = hit.transform.gameObject.GetComponent<PlayerNetworkBehaviour>().m_playerNum.Value;
                }
                
                Debug.Log($"populating popup for inventory number: {inventoryNum}");
                
                PopulateInventory(inventoryType, inventoryNum, viewportPoint);
            }
        }
    }


    private void PopulateInventory(InventoryType inventoryType, int inventoryNum, Vector2 screenPos)
    {
        _inventorySlots.Clear();
        
        inventorySlotContainer.Clear();
        
        List<(int, int)> inventory = InventorySystem.Instance.GetInventory(inventoryNum, inventoryType);
        
        int numInventoryRows = Mathf.CeilToInt(inventory.Count / (float)numItemsPerRow);

        int width = numItemsPerRow * (inventorySlotSizePixels + 4) + 4;
        int height = numInventoryRows * (inventorySlotSizePixels + 4) + 4;


        inventoryRootElement.style.width = width;
        inventoryRootElement.style.height = height;

        inventoryRootElement.style.left = screenPos.x - width / 2f;
        inventoryRootElement.style.top = screenPos.y - height / 2f;

        int itemIdx = 0;
        
        for (int row = 0; row < numInventoryRows; row++)
        {
            for (int col = 0; col < numItemsPerRow; col++)
            {
                int itemNum = inventory[itemIdx].Item1;

                if (itemNum == -1)
                {
                    continue;
                }
                
                int itemCount = inventory[itemIdx].Item2;
                
                InventorySlot newSlot = new InventorySlot(false);

                Debug.Log($"getting item number: {itemNum}");
                ItemDetails itemDetails = InventorySystem.Instance.m_items[itemNum];
                
                newSlot.HoldItem(itemDetails, itemCount);

                newSlot.style.width = inventorySlotSizePixels;
                newSlot.style.height = inventorySlotSizePixels;

                _inventorySlots.Add(newSlot);
                inventorySlotContainer.Add(newSlot);
                itemIdx++;
            }
        }

    }

}