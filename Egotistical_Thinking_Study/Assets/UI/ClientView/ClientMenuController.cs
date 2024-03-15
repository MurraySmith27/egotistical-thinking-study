using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientMenuController : MonoBehaviour
{

    private static ClientMenuController _instance;

    public static ClientMenuController Instance
    {
        get { return _instance; }
    }

    [SerializeField] private GameObject m_inventoryMenuGameObject;
    
    [SerializeField] private VisualTreeAsset m_inventoryElementAsset;

    [SerializeField] private float m_loadingDistanceFromWarehouse = 1f;

    [SerializeField] private VisualTreeAsset m_orderElementAsset;
    
    private VisualElement m_playerInventoryElement;

    private VisualElement m_warehouseInventoryElement;

    private VisualElement m_ownedWarehouseInventoryElement;

    private int m_ownedWarehouseNum;
    
    private VisualElement m_root;

    private List<InventorySlot> m_playerInventoryItems = new List<InventorySlot>();
    
    private List<InventorySlot> m_warehouseInventoryItems = new List<InventorySlot>();

    private VisualElement m_warehouseInventoryRoot;
    
    private VisualElement m_destinationWarehouseInventoryRoot;

    private GameObject m_thisPlayerGameObject;

    private VisualElement m_ghostIcon;

    private bool m_isDragging;
    
    private bool m_draggingFromPlayerInventory;

    private bool m_inRangeOfOwnedInventory;

    private int m_draggingFromInventoryNum;
    
    private InventorySlot m_draggingOriginalInventorySlot;
    
    private GameObject m_currentLoadingWarehouse = null;
    
    private int m_currentLoadingWarehouseNum = -1;

    private VisualElement m_orderRoot;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }
    
    void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("root");

        m_orderRoot = m_root.Q<VisualElement>("order-root");
        
        VisualElement playerInventoryRoot = m_root.Q<VisualElement>("player-inventory-root");
        
        m_playerInventoryElement = m_inventoryElementAsset.Instantiate();
        playerInventoryRoot.Add(m_playerInventoryElement);
        
        
        m_playerInventoryElement.Q<Label>("header").text = $"Inventory";

        m_warehouseInventoryRoot = m_root.Q<VisualElement>("warehouse-inventory-root");

        m_destinationWarehouseInventoryRoot = m_root.Q<VisualElement>("destination-inventory-root");

        m_ghostIcon = m_root.Q<VisualElement>("ghost-icon");

        m_ghostIcon.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        m_ghostIcon.RegisterCallback<PointerUpEvent>(OnPointerUp);

        m_ownedWarehouseInventoryElement = m_inventoryElementAsset.Instantiate();
        m_warehouseInventoryRoot.Add(m_ownedWarehouseInventoryElement);
        
        m_ownedWarehouseInventoryElement.Q<Label>("header").text = $"Warehouse";

        m_ownedWarehouseInventoryElement.style.opacity = 0.5f;

        m_inRangeOfOwnedInventory = false;
        
        int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

        m_ownedWarehouseNum = -1;
        for (int i = 0; i < MapDataNetworkBehaviour.Instance.warehouseNetworkObjectIds.Value.arr.Length; i++)
        {
            if (InventorySystem.Instance.GetOwnerOfWarehouse(i) == playerNum)
            {
                m_ownedWarehouseNum = i;
                break;
            }
        }

        if (m_ownedWarehouseNum == -1)
        {
            Debug.LogError($"Could not find owned warehouse for player number: {playerNum}! " +
                           $"Check your config file to make sure each player has a warehouse.");
        }
        
        InventorySystem.Instance.RegisterWarehouseInventoryChangedCallback(m_ownedWarehouseNum, UpdateOwnedWarehouseInventory);
        //one initial call
        UpdateOwnedWarehouseInventory();
        
        m_warehouseInventoryElement = m_inventoryElementAsset.Instantiate();
        
        m_destinationWarehouseInventoryRoot.Add(m_warehouseInventoryElement);
        
        m_warehouseInventoryElement.style.visibility = Visibility.Hidden;
        
        InventorySystem.Instance.RegisterPlayerInventoryChangedCallback(playerNum, UpdatePlayerInventory);

        ulong playerNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(playerNum);

        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (playerNetworkObjectId == networkObject.NetworkObjectId)
            {
                //found it.
                m_thisPlayerGameObject = networkObject.gameObject;
            }
        }

        OrderSystem.Instance.activeOrders.OnValueChanged += OnOrderSent;
    }

    public void StartDrag(Vector2 position, InventorySlot originalInventorySlot)
    {
        m_isDragging = true;

        m_draggingOriginalInventorySlot = originalInventorySlot;
        if (originalInventorySlot.worldBound.Overlaps(m_playerInventoryElement.worldBound))
        {
            m_draggingFromPlayerInventory = true;
            m_draggingFromInventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
        }
        else if (originalInventorySlot.worldBound.Overlaps(m_warehouseInventoryElement.worldBound))
        {
            if (m_currentLoadingWarehouse == null)
            {
                Debug.LogError("somehow a drag and drop event started over the warehouse inventory " +
                               "while a warehouse inventory wasn't even there");
            }
            m_draggingFromPlayerInventory = false;
            m_draggingFromInventoryNum = m_currentLoadingWarehouseNum;
        }
        else if (originalInventorySlot.worldBound.Overlaps(m_ownedWarehouseInventoryElement.worldBound))
        {
            if (m_inRangeOfOwnedInventory)
            {
                m_draggingFromPlayerInventory = false;
                m_draggingFromInventoryNum = m_ownedWarehouseNum;
            }
        }
        else
        {
            Debug.LogError("need a better way to determine which inventory we're dragging from");
        }
        

        m_ghostIcon.style.top = position.y - m_ghostIcon.layout.height / 2f;
        m_ghostIcon.style.left = position.x - m_ghostIcon.layout.width / 2f;

        Sprite backgroundImage = null;
        foreach (ItemDetails item in InventorySystem.Instance.m_items)
        {
            if (item.GUID == originalInventorySlot.ItemGuid)
            {
                backgroundImage = item.Icon;
            }
        }

        m_ghostIcon.style.backgroundImage = backgroundImage.texture;

        m_ghostIcon.style.visibility = Visibility.Visible;
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!m_isDragging)
        {
            return;
        }
        
        m_ghostIcon.style.top = evt.position.y - m_ghostIcon.layout.height / 2f;
        m_ghostIcon.style.left = evt.position.x - m_ghostIcon.layout.width / 2f;
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!m_isDragging)
        {
            return;
        }

        ItemDetails details = null;
        int itemNum = -1;
        for (int i = 0; i < InventorySystem.Instance.m_items.Count; i++)
        {
            ItemDetails item = InventorySystem.Instance.m_items[i];
            if (item.GUID == m_draggingOriginalInventorySlot.ItemGuid)
            {
                details = item;
                itemNum = i;
                break;
            }
        }

        int itemCount = -1, originItemSlot = -1;
        
        int inventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
        if (!m_draggingFromPlayerInventory)
        {
            inventoryNum = m_draggingFromInventoryNum;
        }

        List<(int, int)> originInventory = InventorySystem.Instance.GetInventory(inventoryNum, m_draggingFromPlayerInventory);

        for (int i = 0; i < originInventory.Count; i++)
        {
            if (originInventory[i].Item1 == itemNum)
            {
                itemCount = originInventory[i].Item2;
                originItemSlot = i;
                break;
            }
        }
        
        //check if dragging to player inventory
        List<(int, InventorySlot)> playerInventorySlots = new();
        
        for (int i = 0; i < m_playerInventoryItems.Count; i++)
        {
            if (m_playerInventoryItems[i].worldBound.Overlaps(m_ghostIcon.worldBound))
            {
                playerInventorySlots.Add((i, m_playerInventoryItems[i]));
            }
        }
        
        List<(int, InventorySlot)> warehouseInventorySlots = new();
        
        for (int i = 0; i < m_warehouseInventoryItems.Count; i++)
        {
            if (m_warehouseInventoryItems[i].worldBound.Overlaps(m_ghostIcon.worldBound))
            {
                warehouseInventorySlots.Add((i, m_warehouseInventoryItems[i]));
            }
        }
        
        if (playerInventorySlots.Count() != 0)
        {
            (int, InventorySlot) closestSlot = playerInventorySlots.OrderBy(x =>
                Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();

            //need to update inventory
            int destinationInventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
            InventorySystem.Instance.AddItemToInventory(destinationInventoryNum, true, details.GUID, itemCount, closestSlot.Item1);
            for (int i = 0; i < itemCount; i++)
            {
                InventorySystem.Instance.RemoveItemFromInventory(m_draggingFromInventoryNum, details.GUID,
                    m_draggingFromPlayerInventory);
            }
        }
        else if (warehouseInventorySlots.Count() != 0)
        {
            (int, InventorySlot) closestSlot = warehouseInventorySlots.OrderBy(x =>
                Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();

            int destinationInventoryNum = m_currentLoadingWarehouseNum;
            if (m_inRangeOfOwnedInventory)
            {
                destinationInventoryNum = m_ownedWarehouseNum;
            }
            
            InventorySystem.Instance.AddItemToInventory(destinationInventoryNum, false, details.GUID, itemCount, closestSlot.Item1);
            for (int i = 0; i < itemCount; i++)
            {
                InventorySystem.Instance.RemoveItemFromInventory(m_draggingFromInventoryNum, details.GUID,
                    m_draggingFromPlayerInventory);
            }
        }

        m_isDragging = false;
        m_draggingOriginalInventorySlot = null;
        m_ghostIcon.style.visibility = Visibility.Hidden;
    }

    void Update()
    {
        //set warehouse view active if close enough.
        if (m_thisPlayerGameObject == null)
        {
            Debug.LogError("failed to register player gameobject in start method of ClientMenuController!");
        }
        else
        {
            bool foundNearestWarehouse = false;
            NetworkObject nearestWarehouseNetworkObject = null;
            int nearestWarehouseNum = -1;
            float minWarehouseDistance = float.MaxValue;
            for (int warehouseNum = 0; warehouseNum < MapDataNetworkBehaviour.Instance.warehouseNetworkObjectIds.Value.arr.Length; warehouseNum++)
            {
                ulong warehouseNetworkId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(warehouseNum);
                foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
                {
                    if (networkObject.NetworkObjectId == warehouseNetworkId)
                    {
                        float playerToWarehouseDistance =
                            (m_thisPlayerGameObject.transform.position - networkObject.transform.position).magnitude;
                        if (playerToWarehouseDistance < m_loadingDistanceFromWarehouse && playerToWarehouseDistance < minWarehouseDistance)
                        {
                            foundNearestWarehouse = true;
                            nearestWarehouseNum = warehouseNum;
                            nearestWarehouseNetworkObject = networkObject;
                            minWarehouseDistance = playerToWarehouseDistance;
                        }
                    }
                }
            }
            
            int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

            bool belongsToThisPlayer = nearestWarehouseNum != -1 && InventorySystem.Instance.GetOwnerOfWarehouse(nearestWarehouseNum) == playerNum;

            if (foundNearestWarehouse && m_currentLoadingWarehouseNum == -1)
            {

                if (belongsToThisPlayer)
                {
                    m_currentLoadingWarehouseNum = m_ownedWarehouseNum;
                    m_ownedWarehouseInventoryElement.style.opacity = 1f;
                    m_inRangeOfOwnedInventory = true;
                }
                else
                {
                    m_currentLoadingWarehouse = nearestWarehouseNetworkObject.gameObject;
                    m_currentLoadingWarehouseNum = nearestWarehouseNum;
                    m_warehouseInventoryElement.style.visibility = Visibility.Visible;

                    m_warehouseInventoryElement.Q<Label>("header").text = $"Destination: {nearestWarehouseNum}";
                    InventorySystem.Instance.RegisterWarehouseInventoryChangedCallback(m_currentLoadingWarehouseNum,
                        UpdateWarehouseInventory);
                    //also just update the warehouse to initial state. 
                    UpdateWarehouseInventory();
                }
                
            }
            else if (!foundNearestWarehouse && m_currentLoadingWarehouseNum != -1)
            {

                if (InventorySystem.Instance.GetOwnerOfWarehouse(m_currentLoadingWarehouseNum) == playerNum)
                {
                    m_ownedWarehouseInventoryElement.style.opacity = 0.5f;
                    m_inRangeOfOwnedInventory = false;
                    m_currentLoadingWarehouseNum = -1;
                    m_currentLoadingWarehouse = null;
                }
                else
                {
                    m_warehouseInventoryElement.style.visibility = Visibility.Hidden;
                    InventorySystem.Instance.DeregisterWarehouseInventoryChangedCallback(m_currentLoadingWarehouseNum,
                        UpdateWarehouseInventory);
                    m_warehouseInventoryElement.Q<VisualElement>("slot-container").Clear();
                    m_currentLoadingWarehouse = null;
                    m_currentLoadingWarehouseNum = -1;
                }
                
            }
        }
    }

    void OnOrderSent(NetworkSerializableIntArray old, NetworkSerializableIntArray current)
    {
        m_orderRoot.Clear();

        for (int i = 0; i < current.arr.Length; i++)
        {
            if (current.arr[i] != 0)
            {
                m_orderRoot.style.display = DisplayStyle.Flex;
                
                NetworkSerializableOrder order = OrderSystem.Instance.GetOrder(i);
                
                VisualElement orderElement = m_orderElementAsset.Instantiate();
                orderElement.AddToClassList("order");
                orderElement.Q<Label>("order-number-label").text = $"Order {i + 1}:";
                orderElement.Q<Label>("order-description").text = order.textDescription;
                orderElement.Q<Label>("send-to-player-label").text = $"Receiving Player: {order.receivingPlayer}";
                VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
                foreach (string key in order.requiredItems.Keys)
                {
                    int itemNum = Int32.Parse(key);
                    InventorySlot slot = new InventorySlot(false);
                    slot.HoldItem(InventorySystem.Instance.m_items[itemNum], order.requiredItems[key]);
                    itemsContainer.Add(slot);
                }

                string mapDestinationText = $"Destination Coords: ({order.mapDestination[0]}, {order.mapDestination[1]})";
                orderElement.Q<Label>("map-destination-label").text = mapDestinationText;

                m_orderRoot.Add(orderElement);
            }
        }
    }

    void UpdatePlayerInventory()
    {
        UpdateInventory(true, false);
    }

    void UpdateWarehouseInventory()
    {
        UpdateInventory(false, false);
    }

    void UpdateOwnedWarehouseInventory()
    {
        UpdateInventory(false, true);
    }

    void UpdateInventory(bool isPlayer, bool ownedWarehouse)
    {
        int inventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;

        if (!isPlayer)
        {
            if (ownedWarehouse)
            {
                inventoryNum = m_ownedWarehouseNum;
            }
            else
            {
                inventoryNum = m_currentLoadingWarehouseNum;
            }
        }
        
        VisualElement inventoryContainer = m_playerInventoryElement.Q<VisualElement>("slot-container");
        if (!isPlayer)
        {
            if (ownedWarehouse)
            {
                inventoryContainer = m_ownedWarehouseInventoryElement.Q<VisualElement>("slot-container");
            }
            else
            {
                inventoryContainer = m_warehouseInventoryElement.Q<VisualElement>("slot-container");
            }
        }

        inventoryContainer.Clear();
        
        
        List<(int, int)> inventory = InventorySystem.Instance.GetInventory(inventoryNum, isPlayer);

        List<InventorySlot> inventoryItems = m_playerInventoryItems;
        if (!isPlayer)
        {
            inventoryItems = m_warehouseInventoryItems;
        }
        inventoryItems.Clear();
        
        //populate inventory
        int numInventorySlots = InventorySystem.Instance.m_numInventorySlotsPerPlayer;
        if (!isPlayer)
        {
            numInventorySlots = InventorySystem.Instance.m_numInventorySlotsPerWarehouse;
        }
        for (int i = 0; i < numInventorySlots; i++)
        {
            InventorySlot itemSlot = new InventorySlot();
            inventoryItems.Add(itemSlot);
            inventoryContainer.Add(itemSlot);
            
            if (inventory[i].Item1 != -1)
            {
                //item exists in this slot.
                itemSlot.HoldItem(InventorySystem.Instance.m_items[inventory[i].Item1], inventory[i].Item2);
            }
        }
    }
}
