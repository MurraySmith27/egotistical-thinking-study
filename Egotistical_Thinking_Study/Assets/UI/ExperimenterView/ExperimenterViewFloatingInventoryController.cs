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

    private List<InventorySlot> _inventorySlots;
    
    private InputAction clickAction;
    private InputAction mousePosition;


    void Start()
    {
        rootVisualElement = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("root");

        inventorySlotContainer = rootVisualElement.Q<VisualElement>("floating-inventory-popup").Q<VisualElement>("slot-container");

        clickAction = experimenterInput["mouseClick"];
        mousePosition = experimenterInput["mousePosition"];

        clickAction.performed += OnClick;
    }

    void OnDestroy()
    {
        clickAction.performed -= OnClick;
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        Vector2 mousePos = mousePosition.ReadValue<Vector2>();
        
        Vector2 topLeftCorner = new Vector2(0f, 0f);
        Vector2 bottomRightCorner = new Vector2(Screen.width / 2f,Screen.height);
        
        float width = bottomRightCorner.x - topLeftCorner.x;
        float height = bottomRightCorner.y - topLeftCorner.y;
        
        //raycast from camera center, see if it intersects with the map.
        RaycastHit hit;
        Camera playerCameraComponent = playerCamera.GetComponent<Camera>();
        Ray ray = playerCameraComponent.ViewportPointToRay(new Vector3((mousePos.x - topLeftCorner.x) / width, ((mousePos.y - topLeftCorner.y) / height), 0));
        
        if (Physics.Raycast(ray.origin, ray.direction, out hit, 100, ~LayerMask.NameToLayer("MapTile")))
        {
            Vector3 hitPos = hit.transform.position;

            InventoryNetworkBehaviour inventoryNetworkBehaviour =
                hit.transform.gameObject.GetComponent<InventoryNetworkBehaviour>(); 
            if (inventoryNetworkBehaviour != null)
            {
                mouseClickSFX.Play();
                Vector2Int destinationPos = new((int)(hitPos.x / MapGenerator.Instance.tileWidth),
                        (int)(hitPos.y / MapGenerator.Instance.tileHeight));
                
                if (hit.transform.gameObject.name.Contains("Destination"))
                {
                    int inventoryNum;
                    
                    for (int i = 0 ;)


                }
            }
        }
    }


    private void PopulateInventory(InventoryType inventoryType, int inventoryNum)
    {
        _inventorySlots.Clear();
        
        inventorySlotContainer.Clear();
        
        List<(int, int)> inventory = InventorySystem.Instance.GetInventory(inventoryNum, inventoryType);

        int numInventoryRows = Mathf.CeilToInt(inventory.Count / (float)numItemsPerRow);

        for (int row = 0; row < numInventoryRows; row++)
        {

            for (int col = 0; col < numItemsPerRow; col++)
            {
                int itemNum = inventory[row * numItemsPerRow + col].Item1;
                int itemCount = inventory[row * numItemsPerRow + col].Item2;
                
                InventorySlot newSlot = new InventorySlot(false);

                ItemDetails itemDetails = InventorySystem.Instance.m_items[itemNum];
                
                newSlot.HoldItem(itemDetails, itemCount);

                newSlot.style.width = inventorySlotSizePixels;
                newSlot.style.height = inventorySlotSizePixels;

                _inventorySlots.Add(newSlot);
                inventorySlotContainer.Add(newSlot);
            }
        }

    }

}