using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class ExperimenterViewFloatingInventoryController : MonoBehaviour
{

    [SerializeField] private int inventorySlotSizePixels = 64;

    [SerializeField] private int numItemsPerRow = 8;
    
    [SerializeField] private InputActionAsset experimenterInput;

    [SerializeField] private Transform playerCamera;

    [SerializeField] private AudioSource mouseClickSFX;

    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);

    private VisualElement rootVisualElement;

    private VisualElement inventorySlotContainer;

    private Label inventoryHeader;

    private VisualElement inventoryRootElement;

    private List<InventorySlot> _inventorySlots;
    
    private InputAction clickAction;
    private InputAction mousePosition;

    [SerializeField] private int inventoryRootAdditionalHeight = 50;

    private InventoryNetworkBehaviour currentInventory;

    private InventoryType inventoryType;
    private int inventoryNum;
    
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
        inventoryHeader = inventoryRootElement.Q<Label>("header");
        
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
        
        Debug.Log($"ON CLICK, WIDTH: {Screen.width}, height: {Screen.height}");
        
        Vector2 topLeftCorner = new Vector2(Screen.width * 0.3f, 0f);
        Vector2 bottomRightCorner = new Vector2(Screen.width,Screen.height * 0.75f);
        
        float width = bottomRightCorner.x - topLeftCorner.x;
        float height = bottomRightCorner.y - topLeftCorner.y;
        
        Vector2 viewportPoint = new Vector2((mousePos.x - topLeftCorner.x) / width, (mousePos.y - topLeftCorner.y) / height);
        
        //raycast from camera center, see if it intersects with the map.
        RaycastHit hit;
        Camera playerCameraComponent = playerCamera.GetComponent<Camera>();
        Ray ray = playerCameraComponent.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0));
        
        if (Physics.Raycast(ray.origin, ray.direction, out hit, 100, LayerMask.GetMask(new string[]{"MapTile", "Player"})))
        {
            InventoryNetworkBehaviour inventoryNetworkBehaviour =
                hit.transform.gameObject.GetComponent<InventoryNetworkBehaviour>(); 
            if (inventoryNetworkBehaviour != null)
            {
                mouseClickSFX.Play();
                
                if (hit.transform.gameObject.name.Contains("Destination"))
                {
                    inventoryType = InventoryType.Destination;
                    for (int i = 0; i < MapGenerator.Instance.destinations.Count; i++)
                    {
                        if (MapGenerator.Instance.destinations[i].name == hit.transform.name)
                        {
                            inventoryNum = i;
                            InventorySystem.Instance.RegisterDestinationInventoryChangedCallback(inventoryNum, OnInventoryUpdate);
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
                            InventorySystem.Instance.RegisterWarehouseInventoryChangedCallback(inventoryNum, OnInventoryUpdate);
                            break;
                        }
                    }
                }
                else if (hit.transform.gameObject.name.Contains("Player"))
                {
                    inventoryType = InventoryType.Player;
                    inventoryNum = hit.transform.gameObject.GetComponent<PlayerNetworkBehaviour>().m_playerNum.Value;
                    InventorySystem.Instance.RegisterPlayerInventoryChangedCallback(inventoryNum, OnInventoryUpdate);
                }
                
                
                PopulateInventory(inventoryNetworkBehaviour, hit.transform.gameObject.name, viewportPoint * new Vector2(Screen.width, Screen.height));
            }
            else
            {
                HideInventoryPopup();
            }
        }
        else
        {
            HideInventoryPopup();
        }
    }

    private void OnInventoryUpdate()
    {
        Debug.Log("invnetory updated!");
        if (currentInventory != null)
        {
            PopulateInventory(currentInventory, "", new Vector2(-1, -1));
        }
    }

    private void PopulateInventory(InventoryNetworkBehaviour inventoryNetworkBehaviour, string inventoryName, Vector2 screenPos)
    {
        _inventorySlots.Clear();
        
        inventorySlotContainer.Clear();

        if (inventoryName != "")
        {
            inventoryHeader.text = inventoryName;
        }
        
        List<(int, int)> inventory = inventoryNetworkBehaviour.GetInventory();

        int numInventoryRows = Mathf.CeilToInt(inventory.Count / (float)numItemsPerRow);
        
        if (screenPos.x > 0)
        {
            int width = numItemsPerRow * (inventorySlotSizePixels + 4) + 4;
            int height = Mathf.Max(numInventoryRows * (inventorySlotSizePixels + 4) + 4 + inventoryRootAdditionalHeight,
                80);

            inventoryRootElement.style.width = width;
            inventoryRootElement.style.height = height;

            int minLeftOffset = 0;
            int maxLeftOffset = Mathf.FloorToInt(referenceResolution.x - width*2f);

            int minTopOffset = 0;
            int maxTopOffset = Mathf.FloorToInt(referenceResolution.y - height*4f);
            
            inventoryRootElement.style.left = Mathf.Clamp(referenceResolution.x * (screenPos.x - width / 2f) / Screen.width, minLeftOffset, maxLeftOffset);
            inventoryRootElement.style.top = Mathf.Clamp(referenceResolution.y * (Screen.height - screenPos.y - height / 2f) / Screen.height, minTopOffset, maxTopOffset);

            inventoryRootElement.style.visibility = Visibility.Visible;
        }

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

                ItemDetails itemDetails = InventorySystem.Instance.m_items[itemNum];
                
                newSlot.HoldItem(itemDetails, itemCount);

                newSlot.style.width = inventorySlotSizePixels;
                newSlot.style.height = inventorySlotSizePixels;
                newSlot.style.maxWidth = inventorySlotSizePixels;
                newSlot.style.maxHeight = inventorySlotSizePixels;

                _inventorySlots.Add(newSlot);
                inventorySlotContainer.Add(newSlot);
                itemIdx++;
            }
        }

        currentInventory = inventoryNetworkBehaviour;
    }

    private void HideInventoryPopup()
    {
        inventoryRootElement.style.visibility = Visibility.Hidden;
        
        inventoryHeader.text = "";
        
        _inventorySlots.Clear();
        
        inventorySlotContainer.Clear();

        if (currentInventory != null)
        {
            if (inventoryType == InventoryType.Destination)
            {
                InventorySystem.Instance.DeregisterDestinationInventoryChangedCallback(inventoryNum, OnInventoryUpdate);
            }
            else if (inventoryType == InventoryType.Warehouse)
            {
                InventorySystem.Instance.DeregisterWarehouseInventoryChangedCallback(inventoryNum, OnInventoryUpdate);
            }
            else if (inventoryType == InventoryType.Player)
            {
                InventorySystem.Instance.DeregisterPlayerInventoryChangedCallback(inventoryNum, OnInventoryUpdate);
            }

            inventoryNum = -1;
            currentInventory = null;
        }
    }

}