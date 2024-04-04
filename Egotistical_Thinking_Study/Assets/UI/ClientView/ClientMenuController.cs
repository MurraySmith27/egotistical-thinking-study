using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
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

    private List<VisualElement> m_loadAllButtons;

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

    private List<VisualElement> m_activeOrderElements;

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

        m_activeOrderElements = new List<VisualElement>();
        
        VisualElement playerInventoryRoot = m_root.Q<VisualElement>("player-inventory-root");
        
        m_playerInventoryElement = m_inventoryElementAsset.Instantiate();
        playerInventoryRoot.Add(m_playerInventoryElement);
        
        
        m_playerInventoryElement.Q<Label>("header").text = $"Inventory";

        m_warehouseInventoryRoot = m_root.Q<VisualElement>("warehouse-inventory-root");

        m_loadAllButtons = new List<VisualElement>();

        m_destinationWarehouseInventoryRoot = m_root.Q<VisualElement>("destination-inventory-root");

        m_ghostIcon = m_root.Q<VisualElement>("ghost-icon");

        m_ghostIcon.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        m_ghostIcon.RegisterCallback<PointerUpEvent>(OnPointerUp);

        m_ownedWarehouseInventoryElement = m_inventoryElementAsset.Instantiate();
        m_warehouseInventoryRoot.Add(m_ownedWarehouseInventoryElement);
        
        VisualElement inventoryElement = m_ownedWarehouseInventoryElement.Q<VisualElement>("inventory");
        
        inventoryElement.style.borderBottomColor = Color.red;
        inventoryElement.style.borderTopColor = Color.red;
        inventoryElement.style.borderLeftColor = Color.red;
        inventoryElement.style.borderRightColor = Color.red;

        inventoryElement.style.borderBottomWidth = 3f;
        inventoryElement.style.borderTopWidth = 3f;
        inventoryElement.style.borderLeftWidth = 3f;
        inventoryElement.style.borderRightWidth = 3f;
        
        m_ownedWarehouseInventoryElement.Q<Label>("header").text = $"Warehouse";

        m_ownedWarehouseInventoryElement.style.opacity = 0.5f;
        
        m_root.Q<Label>("score-label").text = "0G";

        ProgressBar gasBar = m_root.Q<ProgressBar>("gas-bar");

        
        int maxGas = MapDataNetworkBehaviour.Instance.maxGasPerPlayer.Value;
        gasBar.title = $"{maxGas}/{maxGas}";
        gasBar.value = 100;

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

        OrderSystem.Instance.activeOrders.OnValueChanged += OnActiveOrdersChanged;

        OrderSystem.Instance.completeOrders.OnValueChanged += OnCompleteOrdersChanged;

        OrderSystem.Instance.incompleteOrders.OnValueChanged += OnIncompleteOrdersChanged;

        OrderSystem.Instance.currentScorePerPlayer.OnValueChanged += OnScoreChanged;

        m_thisPlayerGameObject.GetComponent<PlayerNetworkBehaviour>().m_numGasRemaining.OnValueChanged +=
            OnGasValueChanged;
        
        
    }

    public void StartDrag(Vector2 position, InventorySlot originalInventorySlot)
    {
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
            else {
                return;
            }
        }
        else
        {
            Debug.LogError("need a better way to determine which inventory we're dragging from");
        }
        

        m_isDragging = true;
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
        
        int destinationInventoryNum = m_currentLoadingWarehouseNum;
        if (m_inRangeOfOwnedInventory)
        {
            destinationInventoryNum = m_ownedWarehouseNum;
        }
        
        if (playerInventorySlots.Count() != 0)
        {
            (int, InventorySlot) closestSlot = playerInventorySlots.OrderBy(x =>
                Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();

            //need to update inventory
            destinationInventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
            InventorySystem.Instance.AddItemToInventory(destinationInventoryNum, true, details.GUID, itemCount, closestSlot.Item1);

            InventorySystem.Instance.RemoveItemFromInventory(m_draggingFromInventoryNum, m_draggingFromPlayerInventory, details.GUID, itemCount);
        }
        else if (warehouseInventorySlots.Count() != 0 && destinationInventoryNum != -1)
        {
            (int, InventorySlot) closestSlot = warehouseInventorySlots.OrderBy(x =>
                Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();
            
            InventorySystem.Instance.AddItemToInventory(destinationInventoryNum, false, details.GUID, itemCount, closestSlot.Item1);
            
            InventorySystem.Instance.RemoveItemFromInventory(m_draggingFromInventoryNum, m_draggingFromPlayerInventory, details.GUID, itemCount);
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
                    
                    VisualElement inventoryElement =
                        m_ownedWarehouseInventoryElement.Q<VisualElement>("inventory");

                    inventoryElement.style.borderBottomColor = Color.green;
                    inventoryElement.style.borderTopColor = Color.green;
                    inventoryElement.style.borderLeftColor = Color.green;
                    inventoryElement.style.borderRightColor = Color.green;
                    
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
                UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value, OrderSystem.Instance.incompleteOrders.Value);
                
            }
            else if (!foundNearestWarehouse && m_currentLoadingWarehouseNum != -1)
            {

                if (InventorySystem.Instance.GetOwnerOfWarehouse(m_currentLoadingWarehouseNum) == playerNum)
                {
                    m_ownedWarehouseInventoryElement.style.opacity = 0.5f;

                    VisualElement inventoryElement =
                        m_ownedWarehouseInventoryElement.Q<VisualElement>("inventory");
                    
                    inventoryElement.style.borderBottomColor = Color.red;
                    inventoryElement.style.borderTopColor = Color.red;
                    inventoryElement.style.borderLeftColor = Color.red;
                    inventoryElement.style.borderRightColor = Color.red;
                    
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
                
                UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value , OrderSystem.Instance.incompleteOrders.Value);
                
            }
        }
    }

    
    void OnActiveOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current) {
        UpdateOrdersList(current, OrderSystem.Instance.completeOrders.Value, OrderSystem.Instance.incompleteOrders.Value);
    }
    
    void OnCompleteOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current) {
        UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, current, OrderSystem.Instance.incompleteOrders.Value);
    }

    void OnIncompleteOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current)
    {
        UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value, current);
    }


    void UpdateOrdersList(NetworkSerializableIntArray activeOrders, NetworkSerializableIntArray completeOrders, NetworkSerializableIntArray incompleteOrders)
    {
        m_orderRoot.Clear();
        m_activeOrderElements.Clear();

        for (int i = 0; i < activeOrders.arr.Length; i++)
        {
            m_loadAllButtons.Add(null);
            
            if (activeOrders.arr[i] != 0)
            {
                m_orderRoot.style.display = DisplayStyle.Flex;
                
                NetworkSerializableOrder order = OrderSystem.Instance.GetOrder(i);

                if (order.receivingPlayer == ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum)
                {
                    VisualElement orderElement = m_orderElementAsset.Instantiate();
                    orderElement.AddToClassList("order");
                    ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
                    if (order.orderTimeLimit != -1)
                    {
                        orderTimer.style.visibility = Visibility.Visible;
                        orderTimer.lowValue =  order.orderTimeRemaining / (float)order.orderTimeLimit;
                        orderTimer.title = $"{order.orderTimeRemaining}s";
                    }

                    orderElement.Q<Label>("order-number-label").text = $"Order {i + 1}:";
                    orderElement.Q<Label>("order-description").text = order.textDescription.ToString();
                    orderElement.Q<Label>("send-to-player-label").text = $"Receiving Player: {order.receivingPlayer}";
                    orderElement.Q<Label>("score-reward-label").text = $"Reward: {order.scoreReward}G";
                    VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
                    foreach (string key in order.requiredItems.Keys)
                    {
                        int itemNum = Int32.Parse(key);
                        InventorySlot slot = new InventorySlot(false);
                        slot.HoldItem(InventorySystem.Instance.m_items[itemNum], order.requiredItems[key]);
                        itemsContainer.Add(slot);
                    }

                    string mapDestinationText = $"Destination Warehouse: ";
                    orderElement.Q<Label>("map-destination-label").text = mapDestinationText;
                    
                    ulong warehouseNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(order.destinationWarehouse);
                    
                    Sprite destinationSprite = null;
                    foreach (NetworkBehaviour networkBehaviour in FindObjectsOfType<NetworkBehaviour>())
                    {
                        if (networkBehaviour.NetworkObjectId == warehouseNetworkObjectId)
                        {
                            destinationSprite = networkBehaviour.GetComponentInChildren<SpriteRenderer>().sprite;
                            break;
                        }
                    }

                    orderElement.Q<VisualElement>("map-destination-image").style.backgroundImage = destinationSprite.texture;

                    if (completeOrders.arr[i] != 0)
                    {
                        orderElement.Q<VisualElement>("checkmark-overlay").style.visibility = Visibility.Visible;
                    }
                    else if (incompleteOrders.arr[i] != 0)
                    {
                        orderElement.Q <VisualElement>("x-overlay").style.visibility = Visibility.Visible;
                    }

                    Button loadAllButton = orderElement.Q<Button>("send-order-button");

                    m_loadAllButtons[i] = loadAllButton;

                    loadAllButton.style.visibility = Visibility.Hidden;
                    if (m_currentLoadingWarehouseNum == order.destinationWarehouse && completeOrders.arr[i] != 0 && incompleteOrders.arr[i] != 0)
                    {
                        loadAllButton.style.visibility = Visibility.Visible;
                        int temp = i;
                        loadAllButton.clicked += () =>
                        {
                            LoadAllFromOrderCallback(temp);
                        };

                        loadAllButton.text = "Deposit Items";
                    }
                    

                    m_orderRoot.Add(orderElement);
                    m_activeOrderElements.Add(orderElement);
                }
            }
        }
    }

    private void OnScoreChanged(NetworkSerializableIntArray previous, NetworkSerializableIntArray current)
    {
        int score = 0;
        if (MapDataNetworkBehaviour.Instance.isScoreShared.Value)
        {
            score = current.arr.Sum();
        }
        else
        {
            int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
            score = current.arr[playerNum];
        }
        
        m_root.Q<Label>("score-label").text = $"{score}G";
    }
    
    void OnGasValueChanged(int previous, int current)
    {
        ProgressBar gasBar = m_root.Q<ProgressBar>("gas-bar");
        int maxGas = MapDataNetworkBehaviour.Instance.maxGasPerPlayer.Value;
        gasBar.value = (100f * current) / maxGas;
        gasBar.title = $"{current}/{maxGas}";
    }

    private void LoadAllFromOrderCallback(int orderIndex)
    {
        if (m_currentLoadingWarehouseNum == OrderSystem.Instance.orders.Value.orders[orderIndex].destinationWarehouse)
        {
            List<(int, int)> playerInventory = InventorySystem.Instance.GetInventory(ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum, true);
            List<(int, int)> warehouseInventory = InventorySystem.Instance.GetInventory(m_currentLoadingWarehouseNum, false);
            foreach (string key in OrderSystem.Instance.orders.Value.orders[orderIndex].requiredItems.Keys)
            {
                int itemNum = Int32.Parse(key);
                
                int numLeftRequired = OrderSystem.Instance.orders.Value.orders[orderIndex].requiredItems[key];

                int warehouseItemSlot = -1;
                int warehouseItemQuantity = 0;
                for (int i = 0; i < warehouseInventory.Count; i++)
                {
                    if (warehouseInventory[i].Item1 == itemNum)
                    {
                        warehouseItemSlot = i;
                        warehouseItemQuantity = warehouseInventory[i].Item2;
                    }
                }

                numLeftRequired -= warehouseItemQuantity;
                

                int itemSlot = -1;
                int itemQuantity = 0;
                for (int i = 0; i < playerInventory.Count; i++)
                {
                    if (playerInventory[i].Item1 == itemNum)
                    {
                        itemSlot = i;
                        itemQuantity = playerInventory[i].Item2;
                    }
                }

                string itemGuid = InventorySystem.Instance.m_items[itemNum].GUID;
                
                if (itemSlot != -1)
                {
                    int quantityToMove = Math.Min(itemQuantity, numLeftRequired);
                    
                    InventorySystem.Instance.AddItemToInventory(m_currentLoadingWarehouseNum, false, itemGuid, quantityToMove);
                    
                    InventorySystem.Instance.RemoveItemFromInventory(ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum, true, itemGuid, quantityToMove);

                }
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
