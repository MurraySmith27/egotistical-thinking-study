using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
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

    [SerializeField] private AudioSource m_correctSFX;
    
    [SerializeField] private AudioSource m_incorrectSFX;

    [SerializeField] private Gradient m_gasFillColorGradient;
    
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
    
    private Button m_gasRefillButton;

    private List<VisualElement> m_activeOrderElements;

    private Dictionary<int, VisualElement> m_otherPlayersTrucksInventoryElements;

    private Dictionary<int, List<InventorySlot>> m_otherPlayersTrucksInventorySlots;

    private bool m_initialized = false;

    private bool m_nearGasStation = false;

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
        StartCoroutine(InitializeCoroutine());
    }

    private Color GetAverageColor(Sprite sprite)
    {
        Color[] colors = sprite.texture.GetPixels();
        float rTotal = 0;
        float gTotal = 0;
        float bTotal = 0;
        foreach (Color color in colors)
        {
            rTotal += color.r;
            gTotal += color.g;
            bTotal += color.b;
        }

        return new Color(rTotal / colors.Length, gTotal / colors.Length, bTotal / colors.Length);
    }
    
    private IEnumerator InitializeCoroutine()
    {
        yield return new WaitUntil(() => {return ClientConnectionHandler.Instance.m_clienSideSessionInfoReceived;});
        m_initialized = true;
        m_root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("root");

        m_orderRoot = m_root.Q<VisualElement>("order-root");

        m_activeOrderElements = new List<VisualElement>();

        m_playerInventoryRoot = m_root.Q<VisualElement>("owned-truck-inventory-root");
        
        m_playerInventoryElement = m_root.Q<VisualElement>("player-inventory-element");

        m_gasRefillButton = m_root.Q<Button>("gas-refill-button");
        
        m_gasRefillButton.clicked += OnGasRefillButtonClicked;

        m_otherPlayersInventoryRoot = m_root.Q<VisualElement>("other-players-inventory-root");

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

        if (MapDataNetworkBehaviour.Instance.maxGasPerPlayer.Value == -1)
        {
            m_root.Q<VisualElement>("truck-gas-bar-root").style.display = DisplayStyle.None;
        }
        
        int maxGas = MapDataNetworkBehaviour.Instance.maxGasPerPlayer.Value;
        gasBar.title = $"{maxGas}/{maxGas}";
        gasBar.value = 100;
        
        foreach (VisualElement child in gasBar.Q<VisualElement>("unity-progress-bar").Children())
        {
            child.style.backgroundColor = m_gasFillColorGradient.Evaluate(1f);
        }

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
                m_thisPlayerGameObject = networkObject.gameObject;
            }
        }

        Sprite playerImage = m_thisPlayerGameObject.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite;
        m_playerInventoryRoot.Q<VisualElement>("player-inventory-icon").style.backgroundImage =
            Background.FromSprite(playerImage);

        m_playerInventoryRoot.Q<VisualElement>("inventory").style.backgroundColor = GetAverageColor(playerImage);

        OrderSystem.Instance.activeOrders.OnValueChanged += OnActiveOrdersChanged;

        OrderSystem.Instance.completeOrders.OnValueChanged += OnCompleteOrdersChanged;

        OrderSystem.Instance.incompleteOrders.OnValueChanged += OnIncompleteOrdersChanged;

        OrderSystem.Instance.acceptedOrders.OnValueChanged += OnAcceptedOrdersChanged;

        OrderSystem.Instance.currentScorePerPlayer.OnValueChanged += OnScoreChanged;
        
        PlayerNetworkBehaviour playerNetworkBehaviour = m_thisPlayerGameObject.GetComponent<PlayerNetworkBehaviour>();

        playerNetworkBehaviour.m_numGasRemaining.OnValueChanged +=
            OnGasValueChanged;
        
        playerNetworkBehaviour.m_onPlayerEnterGasStationRadius += OnPlayerEnterGasStationRadius;
        playerNetworkBehaviour.m_onPlayerExitGasStationRadius += OnPlayerExitGasStationRadius;
        
        m_otherPlayersTrucksInventorySlots = new();
        m_otherPlayersTrucksInventoryElements = new();
    }

    private void OnGasRefillButtonClicked()
    {
        if (m_nearGasStation)
        {
            m_thisPlayerGameObject.GetComponent<PlayerNetworkBehaviour>().RefillGas();
        }
    }
    
    private void OnPlayerEnterGasStationRadius(int playerNum)
    {
        Debug.Log("etnering gas station radius");
        m_nearGasStation = true;
        m_gasRefillButton.style.visibility = Visibility.Visible;
    }

    private void OnPlayerExitGasStationRadius(int playerNum)
    {
        Debug.Log("exiting gas station radius");
        m_nearGasStation = false;
        m_gasRefillButton.style.visibility = Visibility.Hidden;
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

                if (details == null)
                {
                    Debug.LogError("couldn't find item in Start Drag from player inventory!");
                    return;
                }

                int itemNum = -1;

                for (int i = 0; i < InventorySystem.Instance.m_items.Count; i++)
                {
                    if (InventorySystem.Instance.m_items[i].GUID == details.GUID)
                    {
                        itemNum = i;
                        break;
                    }
                }

                if (itemNum == -1)
                {
                    Debug.LogError($"couldn't find item with guid: {details.GUID} in list of items!");
                    return;
                }

                List<(int, int)> destinationInventory =
                    InventorySystem.Instance.GetInventory(m_currentLoadingWarehouseNum, false);

                bool foundItemInActiveOrder = false;
                //need to check if item is in an active order for this destination, if not administer a penalty.
                for (int i = 0; i < OrderSystem.Instance.activeOrders.Value.arr.Length; i++)
                {
                    if (OrderSystem.Instance.activeOrders.Value.arr[i] != 0)
                    {
                        Dictionary<string, int> requiredItems =
                            OrderSystem.Instance.orders.Value.orders[i].requiredItems;

                        int quantityInDestinationInventory = 0; 
                        for (int j = 0; j < destinationInventory.Count; j++)
                        {
                            if (destinationInventory[j].Item1 == itemNum)
                            {
                                quantityInDestinationInventory = destinationInventory[j].Item2;
                                break;
                            }
                        }
                        
                        if (requiredItems.Keys.Contains(itemNum.ToString()) && requiredItems[itemNum.ToString()] - quantityInDestinationInventory > 0)
                        {
                            foundItemInActiveOrder = true;
                        }
                        
                    }
                }

                if (foundItemInActiveOrder)
                {
                    m_correctSFX.Play();
                    InventorySystem.Instance.TransferItem(
                        ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum, true,
                        m_currentLoadingWarehouseNum, false, details.GUID, 1);
                }
                else
                {
                    m_incorrectSFX.Play();
                    OrderSystem.Instance.AddScoreToPlayer(
                        ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum,
                        Mathf.Min(-OrderSystem.Instance.incorrectDepositScorePenalty.Value, OrderSystem.Instance.incorrectDepositScorePenalty.Value));
                }

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
            //start drag from owned warehouse
            if (m_inRangeOfOwnedInventory || m_otherPlayersTrucksInventorySlots.Keys.Count > 0)
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
            //check if we can start drag from other truck's inventory
            foreach (int key in m_otherPlayersTrucksInventorySlots.Keys)
            {

                VisualElement otherPlayerInventoryElement = m_otherPlayersTrucksInventoryElements[key];
                if (originalInventorySlot.worldBound.Overlaps(otherPlayerInventoryElement.worldBound))
                {
                    m_draggingFromPlayerInventory = true;
                    m_draggingFromInventoryNum = key;
                }
            }
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
        
        // int inventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
        // if (!m_draggingFromPlayerInventory)
        // {
        int inventoryNum = m_draggingFromInventoryNum;
        //}

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
        if (m_inRangeOfOwnedInventory || destinationInventoryNum == -1)
        {
            destinationInventoryNum = m_ownedWarehouseNum;
        }
        
        if (playerInventorySlots.Count != 0 && m_inRangeOfOwnedInventory)
        {
            (int, InventorySlot) closestSlot = playerInventorySlots.OrderBy(x =>
                Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();

            destinationInventoryNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
            //need to this is valid by checking player inventory capacity
            int inventoryCount = InventorySystem.Instance.GetNumItemsInInventory(destinationInventoryNum, true);

            if (inventoryCount < InventorySystem.Instance.m_inventoryCapacityPerPlayer)
            {
                //need to update inventory
                InventorySystem.Instance.TransferItem(m_draggingFromInventoryNum, m_draggingFromPlayerInventory, destinationInventoryNum, true, details.GUID, 1);
            }
        }
        else if (warehouseInventorySlots.Count != 0)
        {
            if (m_draggingFromPlayerInventory)
            {
                if ((m_draggingFromInventoryNum == ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum && m_currentLoadingWarehouseNum != -1) ||
                    m_otherPlayersTrucksInventoryElements.Keys.Contains(m_draggingFromInventoryNum))
                {

                    (int, InventorySlot) closestSlot = warehouseInventorySlots.OrderBy(x =>
                        Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();

                    InventorySystem.Instance.TransferItem(m_draggingFromInventoryNum, m_draggingFromPlayerInventory, destinationInventoryNum, false, details.GUID, 1);
                }
            }
        }
        else if (!m_draggingFromPlayerInventory)
            //check if it's over annother player's truck

        {
            foreach (int key in m_otherPlayersTrucksInventoryElements.Keys)
            {
                List<(int, InventorySlot)> otherPlayerInventorySlots = new();
                
                for (int j = 0; j < m_otherPlayersTrucksInventorySlots[key].Count; j++)
                {
                    if (m_otherPlayersTrucksInventorySlots[key][j].worldBound.Overlaps(m_ghostIcon.worldBound))
                    {
                        otherPlayerInventorySlots.Add((j, m_otherPlayersTrucksInventorySlots[key][j]));
                    }
                }

                if (otherPlayerInventorySlots.Count != 0)
                {
                    (int, InventorySlot) closestSlot = otherPlayerInventorySlots.OrderBy(x =>
                        Vector2.Distance(x.Item2.worldBound.position, m_ghostIcon.worldBound.position)).First();

                    destinationInventoryNum = key;
                    int inventoryCount = InventorySystem.Instance.GetNumItemsInInventory(destinationInventoryNum, true);

                    if (inventoryCount < InventorySystem.Instance.m_inventoryCapacityPerPlayer)
                    {
                        //need to update inventory
                        InventorySystem.Instance.TransferItem(m_draggingFromInventoryNum, m_draggingFromPlayerInventory, destinationInventoryNum, true, details.GUID, 1);
                    }
                }
            }
        }
        
        m_isDragging = false;
        m_draggingOriginalInventorySlot = null;
        m_ghostIcon.style.visibility = Visibility.Hidden;
    }

    void Update()
    {
        if (m_initialized) {
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
                    Debug.Log($"found nearest warehouse! belongs to this player: {belongsToThisPlayer}");
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
                        
                    }
                    
                    UpdateOrdersList(OrderSystem.Instance.activeOrders.Value, OrderSystem.Instance.completeOrders.Value, OrderSystem.Instance.incompleteOrders.Value, OrderSystem.Instance.acceptedOrders.Value);
                    
                }
                else if (!foundNearestWarehouse && m_currentLoadingWarehouseNum != -1)
                {
                    
                    Debug.Log($"did not find nearest warehouse. current warehouse num: {m_currentLoadingWarehouseNum}");

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
            
            ulong thisPlayerNetworkId =
                MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(ClientConnectionHandler.Instance
                    .clientSideSessionInfo.playerNum);
            for (int truckNum = 0; truckNum < MapDataNetworkBehaviour.Instance.playerNetworkObjectIds.Value.arr.Length; truckNum++)
            {
                ulong playerNetworkId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfPlayer(truckNum);
                if (playerNetworkId == 0 || playerNetworkId == thisPlayerNetworkId)
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

                otherPlayersTruckInventoryRoot.style.flexGrow = 1;
                
                otherPlayersTruckInventoryRoot.style.flexShrink = 1;
                //find active child in heirarchy
                for (int childNum = 0; childNum < closeEnoughTrucks[i].transform.childCount; childNum++)
                {
                    GameObject child = closeEnoughTrucks[i].transform.GetChild(childNum).gameObject;
                    if (child.activeInHierarchy)
                    {
                        Sprite playerImage = child.GetComponent<SpriteRenderer>().sprite;
                        otherPlayersTruckInventoryRoot.Q<VisualElement>("player-inventory-icon").style.backgroundImage = Background.FromSprite(playerImage);
                        otherPlayersTruckInventoryRoot.Q<VisualElement>("inventory").style.backgroundColor = GetAverageColor(playerImage);
                        break;
                    }
                }
                m_otherPlayersTrucksInventoryElements.Add(closeEnoughTrucksPlayerNumbers[i], otherPlayersTruckInventoryRoot);
                m_otherPlayersTrucksInventorySlots.Add(closeEnoughTrucksPlayerNumbers[i], new List<InventorySlot>());
            }
            
            //update warehouse visuals if nearby
            if (m_otherPlayersTrucksInventoryElements.Keys.Count > 0)
            {
                VisualElement inventoryElement =
                    m_ownedWarehouseInventoryElement.Q<VisualElement>("inventory");

                inventoryElement.style.borderBottomColor = Color.green;
                inventoryElement.style.borderTopColor = Color.green;
                inventoryElement.style.borderLeftColor = Color.green;
                inventoryElement.style.borderRightColor = Color.green;
            }
            else if (m_ownedWarehouseNum != m_currentLoadingWarehouseNum) {
                VisualElement inventoryElement =
                    m_ownedWarehouseInventoryElement.Q<VisualElement>("inventory");
                
                inventoryElement.style.borderBottomColor = Color.red;
                inventoryElement.style.borderTopColor = Color.red;
                inventoryElement.style.borderLeftColor = Color.red;
                inventoryElement.style.borderRightColor = Color.red;
            }

            UpdatePlayerInventory();
        }
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
                    orderElement.AddToClassList("order-client-view");
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

                    List<(int, int)> destinationInventory =
                        InventorySystem.Instance.GetInventory(order.destinationWarehouse, false);
                    
                    VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
                    foreach (string key in order.requiredItems.Keys)
                    {
                        
                        int itemNum = Int32.Parse(key);

                        int quantityInDestinationInventory = 0; 
                        for (int j = 0; j < destinationInventory.Count; j++)
                        {
                            if (destinationInventory[j].Item1 == itemNum)
                            {
                                quantityInDestinationInventory = destinationInventory[j].Item2;
                                break;
                            }
                        }
                        
                        InventorySlot slot = new InventorySlot(false);
                        int numRequiredLeft = order.requiredItems[key] - quantityInDestinationInventory;
                        slot.HoldItem(InventorySystem.Instance.m_items[itemNum], numRequiredLeft);
                        if (numRequiredLeft > 0)
                        {
                            itemsContainer.Add(slot);
                        }
                    }

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
        Debug.Log($"on score changed. new score for this player: {score}");
        
        m_root.Q<Label>("score-label").text = $"{score}G";
    }
    
    void OnGasValueChanged(int previous, int current)
    {
        ProgressBar gasBar = m_root.Q<ProgressBar>("truck-gas-bar");
        int maxGas = MapDataNetworkBehaviour.Instance.maxGasPerPlayer.Value;
        gasBar.value = (100f * current) / maxGas;
        gasBar.title = $"{current}/{maxGas}";

        foreach (VisualElement child in gasBar.Q<VisualElement>("unity-progress-bar").Children())
        {
            child.style.backgroundColor = m_gasFillColorGradient.Evaluate(current / (float)maxGas);
        }
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
                    
                    InventorySystem.Instance.TransferItem(ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum, true, m_currentLoadingWarehouseNum, false, itemGuid, quantityToMove);

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
        Label inventoryTitle = m_playerInventoryElement.Q<Label>("inventory-title");
        if (!isPlayer)
        {
            if (ownedWarehouse)
            {
                inventoryContainer = m_ownedWarehouseInventoryElement.Q<VisualElement>("slot-container");
                inventoryTitle = null;
            }
        }

        
        

        List<InventorySlot> inventoryItems = m_playerInventoryItems;
        if (!isPlayer)
        {
            inventoryItems = m_warehouseInventoryItems;
        }

        Color inventoryBorderColor = Color.white;
        int inventoryBorderWidth = 0;

        if (m_currentLoadingWarehouseNum != -1 &&
            InventorySystem.Instance.GetOwnerOfWarehouse(m_currentLoadingWarehouseNum) == -1)
        {
            inventoryBorderColor = Color.green;
            inventoryBorderWidth = 5;
        }

        inventoryContainer.style.borderTopColor = inventoryBorderColor;
        inventoryContainer.style.borderBottomColor = inventoryBorderColor;
        inventoryContainer.style.borderLeftColor = inventoryBorderColor;
        inventoryContainer.style.borderRightColor = inventoryBorderColor;
        inventoryContainer.style.borderTopWidth = inventoryBorderWidth;
        inventoryContainer.style.borderBottomWidth = inventoryBorderWidth;
        inventoryContainer.style.borderLeftWidth = inventoryBorderWidth;
        inventoryContainer.style.borderRightWidth = inventoryBorderWidth;
        
        //populate inventory
        PopulateInventory(isPlayer, inventoryNum, inventoryItems, inventoryContainer, inventoryTitle, Color.white);

        m_otherPlayersInventoryRoot.Clear();
        
        if (isPlayer)
        {
            foreach (int playerNum in m_otherPlayersTrucksInventoryElements.Keys)
            {
                VisualElement otherPlayerInventoryElement = m_otherPlayersTrucksInventoryElements[playerNum];
                
                m_otherPlayersInventoryRoot.Add(otherPlayerInventoryElement);
                
                PopulateInventory(true, playerNum, m_otherPlayersTrucksInventorySlots[playerNum], otherPlayerInventoryElement.Q<VisualElement>("slot-container"), 
                    otherPlayerInventoryElement.Q<Label>("inventory-title"), Color.white);
            }
        }
        
    }

    private void PopulateInventory(bool isPlayer, int inventoryNum, List<InventorySlot> inventoryItems,
        VisualElement inventoryContainer, Label inventoryCapacityLabel, Color itemTintColor)
    {
        inventoryContainer.Clear();
        inventoryItems.Clear();

        List<(int, int)> inventory = InventorySystem.Instance.GetInventory(inventoryNum, isPlayer);

        int numInventorySlots = InventorySystem.Instance.m_numInventorySlotsPerPlayer;
        if (!isPlayer)
        {
            numInventorySlots = InventorySystem.Instance.m_numInventorySlotsPerWarehouse;
        }

        int numTotalItems = 0;
        for (int i = 0; i < numInventorySlots; i++)
        {
            InventorySlot itemSlot = new InventorySlot();
            inventoryContainer.Add(itemSlot);
            inventoryItems.Add(itemSlot);
            if (inventory[i].Item1 != -1)
            {
                numTotalItems += inventory[i].Item2;
                //item exists in this slot.
                itemSlot.HoldItem(InventorySystem.Instance.m_items[inventory[i].Item1], inventory[i].Item2);

                itemSlot.SetItemTint(itemTintColor);
            }
        }

        if (inventoryCapacityLabel != null)
        {
            inventoryCapacityLabel.text = $"{numTotalItems}/{InventorySystem.Instance.m_inventoryCapacityPerPlayer}";
        }

        if (numTotalItems >= InventorySystem.Instance.m_inventoryCapacityPerPlayer)
        {
            //gray out inventorySlots
            for (int i = 0; i < numInventorySlots; i++)
            {
                inventoryItems[i].SetItemTint(Color.gray);
            }
        }
    }
}
