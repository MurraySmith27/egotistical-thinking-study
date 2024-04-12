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

    [SerializeField] private float m_loadingDistanceFromWarehouse = 1f;

    [SerializeField] private VisualTreeAsset m_orderElementAsset;

    [SerializeField] private VisualTreeAsset m_playerInventoryElementAsset;
    
    private VisualElement m_playerInventoryRoot;
    
    private VisualElement m_playerInventoryElement;

    private VisualElement m_otherPlayersInventoryRoot;
    
    private VisualElement m_ownedWarehouseInventoryElement;

    private int m_ownedWarehouseNum;
    
    private VisualElement m_root;

    private List<InventorySlot> m_playerInventoryItems = new List<InventorySlot>();
    
    private List<InventorySlot> m_warehouseInventoryItems = new List<InventorySlot>();

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

    private Dictionary<int, VisualElement> m_otherPlayersTrucksInventoryElements;

    private Dictionary<int, List<InventorySlot>> m_otherPlayersTrucksInventorySlots;

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

        m_playerInventoryRoot = m_root.Q<VisualElement>("owned-truck-inventory-root");
        
        m_playerInventoryElement = m_root.Q<VisualElement>("player-inventory-element");

        m_otherPlayersInventoryRoot = m_root.Q<VisualElement>("other-players-inventory-root");
        
        m_playerInventoryElement.Q<Label>("header").text = $"Inventory";

        m_loadAllButtons = new List<VisualElement>();

        m_ghostIcon = m_root.Q<VisualElement>("ghost-icon");

        m_ghostIcon.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        m_ghostIcon.RegisterCallback<PointerUpEvent>(OnPointerUp);

        m_ownedWarehouseInventoryElement = m_root.Q<VisualElement>("warehouse-inventory");
        
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

        ProgressBar gasBar = m_root.Q<ProgressBar>("truck-gas-bar");

        
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

        OrderSystem.Instance.acceptedOrders.OnValueChanged += OnAcceptedOrdersChanged;

        OrderSystem.Instance.currentScorePerPlayer.OnValueChanged += OnScoreChanged;

        m_thisPlayerGameObject.GetComponent<PlayerNetworkBehaviour>().m_numGasRemaining.OnValueChanged +=
            OnGasValueChanged;


        m_otherPlayersTrucksInventorySlots = new();
        m_otherPlayersTrucksInventoryElements = new();

    }

    public void StartDrag(Vector2 position, InventorySlot originalInventorySlot)
    {
        
        m_draggingOriginalInventorySlot = originalInventorySlot;
        if (originalInventorySlot.worldBound.Overlaps(m_playerInventoryElement.worldBound))
        {
            if (m_currentLoadingWarehouseNum != -1 &&
                InventorySystem.Instance.GetOwnerOfWarehouse(m_currentLoadingWarehouseNum) == -1)
            {
                ItemDetails details = null;
                for (int i = 0; i < InventorySystem.Instance.m_items.Count; i++)
                {
                    ItemDetails item = InventorySystem.Instance.m_items[i];
                    if (item.GUID == m_draggingOriginalInventorySlot.ItemGuid)
                    {
                        details = item;
                        break;
                    }
                }

                InventorySystem.Instance.AddItemToInventory(m_currentLoadingWarehouseNum, false, details.GUID, 1);
                InventorySystem.Instance.RemoveItemFromInventory(ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum, true, details.GUID, 1);
                
                UpdatePlayerInventory();

                return;
            }
            else
            {
                m_draggingFromPlayerInventory = true;
                m_draggingFromInventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
            }
            
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

            int warehouseOwner = InventorySystem.Instance.GetOwnerOfWarehouse(nearestWarehouseNum);
            
            bool belongsToThisPlayer = nearestWarehouseNum != -1 && warehouseOwner == playerNum;

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
                else if (warehouseOwner == -1)
                {
                    m_currentLoadingWarehouse = nearestWarehouseNetworkObject.gameObject;
                    m_currentLoadingWarehouseNum = nearestWarehouseNum;
                    
                    UpdatePlayerInventory();
                    
                }
                
                UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value, OrderSystem.Instance.incompleteOrders.Value, OrderSystem.Instance.acceptedOrders.Value);
                
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
                    m_currentLoadingWarehouse = null;
                    m_currentLoadingWarehouseNum = -1;   
                    
                    UpdatePlayerInventory();
                }
                
                UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value , OrderSystem.Instance.incompleteOrders.Value, OrderSystem.Instance.acceptedOrders.Value);
                
            }
        }
        
        
        //also need to go through the trucks to see which are nearby this player's owned warehouse.

        ulong ownedWarehouseNetworkObjectId =
            MapDataNetworkBehaviour.Instance.GetNetworkIdOfWarehouse(m_ownedWarehouseNum);

        GameObject ownedWarehouseGameObject = null;
        foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.NetworkObjectId == ownedWarehouseNetworkObjectId)
            {
                ownedWarehouseGameObject = networkObject.gameObject;
                break;
            }
        }
        
        List<GameObject> closeEnoughTrucks = new List<GameObject>();
        List<int> closeEnoughTrucksPlayerNumbers = new List<int>();
        
        for (int truckNum = 0; truckNum < MapDataNetworkBehaviour.Instance.playerNetworkObjectIds.Value.arr.Length; truckNum++)
        {
            ulong playerNetworkId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(truckNum);
            if (playerNetworkId == 0 || playerNetworkId == ClientConnectionHandler.Instance.clientSideSessionInfo.clientId)
            {
                continue;
            }
            
            foreach (NetworkObject networkObject in FindObjectsOfType<NetworkObject>())
            {
                if (networkObject.NetworkObjectId == playerNetworkId)
                {
                    closeEnoughTrucksPlayerNumbers.Add(truckNum);
                    
                    float playerToWarehouseDistance =
                        (ownedWarehouseGameObject.transform.position - networkObject.transform.position).magnitude;
                    if (playerToWarehouseDistance < m_loadingDistanceFromWarehouse && playerToWarehouseDistance < m_loadingDistanceFromWarehouse)
                    {
                        closeEnoughTrucks.Add(networkObject.gameObject);
                    }
                }
            }
        }

        m_otherPlayersTrucksInventoryElements.Clear();
        m_otherPlayersTrucksInventorySlots.Clear();

        for (int i = 0; i < closeEnoughTrucks.Count; i++)
        {
            VisualElement otherPlayersTruckInventoryRoot = m_playerInventoryElementAsset.Instantiate();
            m_otherPlayersTrucksInventoryElements.Add(closeEnoughTrucksPlayerNumbers[i], otherPlayersTruckInventoryRoot);
            m_otherPlayersTrucksInventorySlots.Add(closeEnoughTrucksPlayerNumbers[i], new List<InventorySlot>());
        }

        UpdatePlayerInventory();
    }

    
    void OnActiveOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current) {
        UpdateOrdersList(current, OrderSystem.Instance.completeOrders.Value, OrderSystem.Instance.incompleteOrders.Value, OrderSystem.Instance.acceptedOrders.Value);
    }
    
    void OnCompleteOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current) {
        UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, current, OrderSystem.Instance.incompleteOrders.Value, OrderSystem.Instance.acceptedOrders.Value);
    }

    void OnIncompleteOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current)
    {
        UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value, current, OrderSystem.Instance.acceptedOrders.Value);
    }
    
    void OnAcceptedOrdersChanged(NetworkSerializableIntArray old, NetworkSerializableIntArray current)
    {
        UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value, OrderSystem.Instance.incompleteOrders.Value, current);
    }


    void UpdateOrdersList(NetworkSerializableIntArray activeOrders, NetworkSerializableIntArray completeOrders, NetworkSerializableIntArray incompleteOrders, NetworkSerializableIntArray acceptedOrders)
    {
        m_orderRoot.Clear();
        m_activeOrderElements.Clear();

        for (int i = 0; i < activeOrders.arr.Length; i++)
        {
            m_loadAllButtons.Add(null);
            
            if (activeOrders.arr[i] != 0 && acceptedOrders.arr[i] != 1)
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

                    Button acceptOrderButton = orderElement.Q<Button>("accept-button");
                    acceptOrderButton.style.visibility = Visibility.Hidden;
                    
                    Button rejectOrderButton = orderElement.Q<Button>("reject-button");
                    rejectOrderButton.style.visibility = Visibility.Hidden;
                    
                    if (acceptedOrders.arr[i] == 2)
                    {
                        if (m_currentLoadingWarehouseNum == order.destinationWarehouse && completeOrders.arr[i] != 0 &&
                            incompleteOrders.arr[i] != 0)
                        {
                            loadAllButton.style.visibility = Visibility.Visible;
                            int temp = i;
                            loadAllButton.clicked += () => { LoadAllFromOrderCallback(temp); };

                            loadAllButton.text = "Deposit Items";
                        }
                    }
                    else if (acceptedOrders.arr[i] == 0)
                    {
                        acceptOrderButton.style.visibility = Visibility.Visible;
                        rejectOrderButton.style.visibility = Visibility.Visible;

                        int temp = i;
                        acceptOrderButton.clicked += () => { OrderSystem.Instance.AcceptOrder(temp); };
                        
                        rejectOrderButton.clicked += () => { OrderSystem.Instance.RejectOrder(temp); };

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
        ProgressBar gasBar = m_root.Q<ProgressBar>("truck-gas-bar");
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
        }

        
        

        List<InventorySlot> inventoryItems = m_playerInventoryItems;
        if (!isPlayer)
        {
            inventoryItems = m_warehouseInventoryItems;
        }

        Color itemTintColor = Color.white;

        if (m_currentLoadingWarehouseNum != -1 &&
            InventorySystem.Instance.GetOwnerOfWarehouse(m_currentLoadingWarehouseNum) == -1)
        {
            itemTintColor = Color.green;
        }

        //populate inventory
        PopulateInventory(isPlayer, inventoryNum, inventoryItems, inventoryContainer, itemTintColor);

        if (isPlayer)
        {
            foreach (int playerNum in m_otherPlayersTrucksInventoryElements.Keys)
            {
                VisualElement otherPlayerInventoryElement = m_otherPlayersTrucksInventoryElements[playerNum];
                
                m_otherPlayersInventoryRoot.Add(otherPlayerInventoryElement);
                
                // PopulateInventory(true, playerNum, m_otherPlayersTrucksInventorySlots[playerNum], otherPlayerInventoryElement.Q<VisualElement>("slot-container"), Color.white);
            }
        }
        
    }


    private void PopulateInventory(bool isPlayer, int inventoryNum, List<InventorySlot> inventoryItems, VisualElement inventoryContainer, Color itemTintColor)
    {

        inventoryContainer.Clear();
        inventoryItems.Clear();
        
        Debug.Log($"inventory num: {inventoryNum}. is player: {isPlayer}");
        List<(int, int)> inventory = InventorySystem.Instance.GetInventory(inventoryNum, isPlayer);
        
        int numInventorySlots = InventorySystem.Instance.m_numInventorySlotsPerPlayer;
        if (!isPlayer)
        {
            numInventorySlots = InventorySystem.Instance.m_numInventorySlotsPerWarehouse;
        }
        for (int i = 0; i < numInventorySlots; i++)
        {
            InventorySlot itemSlot = new InventorySlot();
            inventoryContainer.Add(itemSlot);
            inventoryItems.Add(itemSlot);
            
            
            if (inventory[i].Item1 != -1)
            {
                //item exists in this slot.
                itemSlot.HoldItem(InventorySystem.Instance.m_items[inventory[i].Item1], inventory[i].Item2);

                itemSlot.SetItemTint(itemTintColor);
            }
        }
    }
}
