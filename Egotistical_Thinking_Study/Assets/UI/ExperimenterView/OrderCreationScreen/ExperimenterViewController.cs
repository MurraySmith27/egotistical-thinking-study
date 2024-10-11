using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class ExperimenterViewController : MonoBehaviour
{
    [SerializeField] private AudioSource m_mouseClickSFX;
    [SerializeField] private VisualTreeAsset m_orderElement;

    [SerializeField] private VisualTreeAsset m_roadblockElement;

    [SerializeField] private Sprite m_roadblockActiveButtonSprite;
    [SerializeField] private Sprite m_roadblockInactiveButtonSprite;

    [SerializeField] private VisualTreeAsset m_gasRefillButtonElement;

    [SerializeField] private Gradient m_gasFillColorGradient;

    [SerializeField] private Sprite m_movementDisableButtonEnabledSprite;
    [SerializeField] private Sprite m_movementDisableButtonDisabledSprite;

    private VisualElement m_root;
    private VisualElement m_orderContainer;
    private VisualElement m_roadblockContainer;

    private List<VisualElement> m_orderElements;
    
    private List<VisualElement> m_roadblockElements;

    private Dictionary<int, VisualElement> m_gasRefillElementsPerPlayer = new Dictionary<int, VisualElement>();
    private Dictionary<int, ProgressBar> m_gasBarElementsPerPlayer = new Dictionary<int, ProgressBar>();

    private Dictionary<int, NetworkVariable<int>.OnValueChangedDelegate> m_gasBarCallback = new Dictionary<int, NetworkVariable<int>.OnValueChangedDelegate>();
    
    private string m_cachedConfigFilePath;
    private string m_cachedMapFilePath;

    private void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement;

        m_orderElements = new List<VisualElement>();

        m_roadblockElements = new List<VisualElement>();

        m_orderContainer = m_root.Q<VisualElement>("order-container");

        m_roadblockContainer = m_root.Q<VisualElement>("roadblocks-list");

        UpdateOrdersList();

        UpdateRoadblocksList();
        
        ClientConnectionHandler.Instance.m_onClientConnected += OnClientConnected;

        m_root.Q<Label>("ip-label").text = $"IP: {ServerManager.m_ipAddress}";
        
        m_root.Q<Label>("port-label").text = $"Port: {ServerManager.m_port}";
        
        m_root.Q<Label>("score-label").text = $"Total Score: {OrderSystem.Instance.currentScorePerPlayer.Value.arr.Sum()}G";

        TimeSpan t = TimeSpan.FromSeconds(GameTimerSystem.Instance.timerSecondsRemaining.Value);
        m_root.Q<Label>("timer-label").text = t.ToString(@"mm\:ss");

        m_root.Q<Button>("pause-resume-button").clicked += OnPauseResumeButtonClicked;
        
        m_root.Q<Button>("reset-button").clicked += OnResetButtonClicked;
        
        m_root.Q<Button>("main-menu-button").clicked += OnMainMenuButtonClicked;
        
        GameTimerSystem.Instance.timerSecondsRemaining.OnValueChanged += OnTimerValueChanged;
        
        OrderSystem.Instance.onScoreChanged += OnScoreChanged;

        OrderSystem.Instance.onOrderChanged += OnOrderValueChanged;
        OrderSystem.Instance.onOrderComplete += OnOrderComplete;
        OrderSystem.Instance.onOrderIncomplete += OnOrderIncomplete;
        OrderSystem.Instance.onOrderRejected += OnOrderRejected;

        RoadblockSystem.OnRoadblockActivate -= OnRoadblockActivate;
        RoadblockSystem.OnRoadblockActivate += OnRoadblockActivate;
        
        RoadblockSystem.OnRoadblockDeactivate -= OnRoadblockDeactivate;
        RoadblockSystem.OnRoadblockDeactivate += OnRoadblockDeactivate;
    }

    private void OnDestroy()
    {
        RoadblockSystem.OnRoadblockActivate -= OnRoadblockActivate;
        RoadblockSystem.OnRoadblockDeactivate -= OnRoadblockDeactivate;

        if (OrderSystem.Instance != null)
        {
            OrderSystem.Instance.onScoreChanged -= OnScoreChanged;
            OrderSystem.Instance.onOrderChanged -= OnOrderValueChanged;
            OrderSystem.Instance.onOrderComplete -= OnOrderComplete;
            OrderSystem.Instance.onOrderIncomplete -= OnOrderIncomplete;
            OrderSystem.Instance.onOrderRejected -= OnOrderRejected;
        }
    }

    private void UpdateOrdersList()
    {
        Order[] orders = GameRoot.Instance.configData.Orders;
        for (int i = 0; i < orders.Length; i++)
        {
            VisualElement orderElement = m_orderElement.Instantiate();
            orderElement.AddToClassList("order");
            ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
            if (orders[i].TimeLimitSeconds != -1)
            {
                orderTimer.style.visibility = Visibility.Visible;
                orderTimer.lowValue = 100;
                orderTimer.title = $"{orders[i].TimeLimitSeconds}s";
            }

            orderElement.Q<Label>("order-number-label").text = $"Order {i + 1}:";
            orderElement.Q<Label>("order-description").text = orders[i].TextDescription;
            orderElement.Q<Label>("send-to-player-label").text = $"Receiving Player: {orders[i].RecievingPlayer}";
            orderElement.Q<Label>("score-reward-label").text = $"Reward: {orders[i].ScoreReward}G";
            VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
            foreach (string key in orders[i].RequiredItems.Keys)
            {
                int itemNum = Int32.Parse(key);
                InventorySlot slot = new InventorySlot(false);
                slot.HoldItem(InventorySystem.Instance.m_items[itemNum], orders[i].RequiredItems[key]);
                itemsContainer.Add(slot);
            }

            string mapDestinationText = $"Destination Warehouse:";
            
            //get map sprite
            ulong destiationNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfDestination(orders[i].DestinationWarehouse);

            Sprite destinationSprite = null;
            foreach (NetworkBehaviour networkBehaviour in FindObjectsOfType<NetworkBehaviour>())
            {
                if (networkBehaviour.NetworkObjectId == destiationNetworkObjectId)
                {
                    destinationSprite = networkBehaviour.GetComponentInChildren<SpriteRenderer>().sprite;
                    break;
                }
            }

            orderElement.Q<VisualElement>("map-destination-image").style.backgroundImage = destinationSprite.texture;
            
            orderElement.Q<Label>("map-destination-label").text = mapDestinationText;

            int x = i;
            orderElement.Q<Button>("send-order-button").clicked += () =>
            {
                m_mouseClickSFX.Play();
                orderElement.Q<Button>("send-order-button").style.visibility = Visibility.Hidden;
                OnOrderSent(x);
            };

            orderElement.MarkDirtyRepaint();
            m_orderContainer.Add(orderElement);
            m_orderElements.Add(orderElement);
            m_orderContainer.MarkDirtyRepaint();
        }
    }

    private void UpdateRoadblocksList()
    {
        List<Roadblock> roadblocks = GameRoot.Instance.configData.Roadblocks;
        
        for (int i = 0 ; i < roadblocks.Count; i++)
        {
            Roadblock roadblock = roadblocks[i];
            VisualElement roadblockElement = m_roadblockElement.Instantiate();

            roadblockElement.Q<Label>("roadblock-title").text = $"Roadblock {i}";

            roadblockElement.Q<Label>("informed-player").text = $"Informed Player: {roadblock.informedPlayer}";

            StringBuilder affectedTilesString = new();

            for (int j = 0; j < roadblock.blockedTiles.Count; j++)
            {
                List<int> affectedTile = roadblock.blockedTiles[j];
                affectedTilesString.Append($"({affectedTile[0]}, {affectedTile[1]})");

                if (j != roadblock.blockedTiles.Count - 1)
                {
                    affectedTilesString.Append(", ");
                }
            }
            roadblockElement.Q<Label>("affected-tiles").text = $"Affected Tiles: {affectedTilesString}";

            Label enabledWithOrder = roadblockElement.Q<Label>("enabled-with-order");
            if (roadblock.autoActivateOnOrder == -1)
            {
                enabledWithOrder.style.display = DisplayStyle.None;
            }
            else
            {
                enabledWithOrder.style.display = DisplayStyle.Flex;
                enabledWithOrder.text = $"Enabled when order {roadblock.autoActivateOnOrder + 1} accepted";
            }
            
            Label disabledWithOrderComplete = roadblockElement.Q<Label>("disabled-when-order-complete");
            if (roadblock.autoActivateOnOrder == -1)
            {
                disabledWithOrderComplete.style.display = DisplayStyle.None;
            }
            else
            {
                disabledWithOrderComplete.style.display = DisplayStyle.Flex;
                disabledWithOrderComplete.text = $"Disabled when order {roadblock.autoDeactivateOnCompleteOrder + 1} complete";
            }
            
            //set up button callback
            Button toggleButton = roadblockElement.Q<Button>("toggle-button");

            int temp = i;
            toggleButton.clicked += () =>
            {
                m_mouseClickSFX.Play();
                if (RoadblockSystem.Instance.IsRoadblockActive(temp))
                {
                    RoadblockSystem.Instance.DeactivateRoadblock(temp);
                }
                else
                {
                    RoadblockSystem.Instance.ActivateRoadblock(temp);
                }
            };
            
            roadblockElement.MarkDirtyRepaint();
            m_roadblockContainer.Add(roadblockElement);
            m_roadblockElements.Add(roadblockElement);
            m_roadblockContainer.MarkDirtyRepaint();
        }
    }

    private void OnRoadblockActivate(int roadblockNum)
    {
        Button toggleButton = m_roadblockElements[roadblockNum].Q<Button>("toggle-button");
        toggleButton.style.backgroundImage = Background.FromSprite(m_roadblockActiveButtonSprite);
        toggleButton.text = "Disable";
    }

    private void OnRoadblockDeactivate(int roadblockNum)
    {
        Button toggleButton = m_roadblockElements[roadblockNum].Q<Button>("toggle-button");
        toggleButton.style.backgroundImage = Background.FromSprite(m_roadblockInactiveButtonSprite);
        toggleButton.text = "Enable";
    }

    private void OnTimerValueChanged(int oldTimerValueSeconds, int currentTimerValueSeconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(currentTimerValueSeconds);
        m_root.Q<Label>("timer-label").text = t.ToString(@"mm\:ss");

        if (currentTimerValueSeconds <= 0)
        {
            GameTimerSystem.Instance.isGamePaused.Value = true;

            Button pauseResumeButton = m_root.Q<Button>("pause-resume-button");
            pauseResumeButton.clicked -= OnPauseResumeButtonClicked;
            pauseResumeButton.style.opacity = 0.5f;
        }
        else if (currentTimerValueSeconds < 60)
        {
            m_root.Q<VisualElement>("timer-parent").style.backgroundColor = new Color(128, 0, 0);
        }
    }

    private void OnPauseResumeButtonClicked()
    {
        m_mouseClickSFX.Play();
        if (!GameTimerSystem.Instance.isGamePaused.Value)
        {
            m_root.Q<Button>("pause-resume-button").text = "Resume";
        }
        else
        {
            m_root.Q<Button>("pause-resume-button").text = "Pause";
        }

        GameTimerSystem.Instance.isGamePaused.Value = !GameTimerSystem.Instance.isGamePaused.Value;
    }
    
    private void OnResetButtonClicked()
    {
        m_mouseClickSFX.Play();
        ServerManager.m_reset = true;
        
        foreach (GameObject obj in FindObjectsOfType(typeof(GameObject)))
        {
            if (obj != null && obj.name != this.gameObject.name && obj.name != this.transform.parent.name && obj.name != NetworkManager.Singleton.gameObject.name)
            {
                DestroyImmediate(obj);
            }
        }
        
        NetworkManager.Singleton.Shutdown();
        DestroyImmediate(NetworkManager.Singleton.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnMainMenuButtonClicked()
    {
        m_mouseClickSFX.Play();
        ServerManager.m_reset = false;
        
        foreach (GameObject obj in FindObjectsOfType(typeof(GameObject)))
        {
            if (obj != null && obj.name != this.gameObject.name && obj.name != this.transform.parent.name && obj.name != NetworkManager.Singleton.gameObject.name)
            {
                DestroyImmediate(obj);
            }
        }
        
        NetworkManager.Singleton.Shutdown();
        DestroyImmediate(NetworkManager.Singleton.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void OnScoreChanged(int[] scorePerPlayer)
    {
        m_root.Q<Label>("score-label").text = $"Total Score: {scorePerPlayer.Sum()}G";
        
        for (int i = 0; i < scorePerPlayer.Length; i++)
        {
            if (m_gasRefillElementsPerPlayer.ContainsKey(i))
            {
                m_gasRefillElementsPerPlayer[i].Q<Label>("score-label").text = $"{scorePerPlayer[i]}G";
            }
        }
    }
    
    
    
    private void OnClientConnected(ulong clientId)
    {
        foreach (Guid guid in ClientConnectionHandler.Instance.serverSideClientList.Keys)
        {
            if (ClientConnectionHandler.Instance.serverSideClientList[guid].clientId == clientId)
            {
                int playerNum = ClientConnectionHandler.Instance.serverSideClientList[guid].playerNum;

                GameObject playerGameObject = GameObject.FindGameObjectWithTag($"Player{playerNum}");

                Sprite playerIcon = playerGameObject.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite;

                VisualElement gasRefillElement = m_gasRefillButtonElement.Instantiate();
                
                m_root.Q<VisualElement>("refill-buttons-parent").Add(gasRefillElement);

                Label scoreLabel = gasRefillElement.Q<Label>("score-label");
                
                if (!GameRoot.Instance.configData.IsScoreShared)
                {
                    // scoreLabel.parent.Remove(scoreLabel);
                    scoreLabel.text = "";
                }
                else
                {
                    scoreLabel.text = $"{OrderSystem.Instance.currentScorePerPlayer.Value.arr[playerNum]}G";
                }

                m_gasBarElementsPerPlayer.Add(playerNum, gasRefillElement.Q<ProgressBar>("gas-bar"));

                m_gasBarElementsPerPlayer[playerNum].lowValue = 0;
                m_gasBarElementsPerPlayer[playerNum].highValue = 100;
                m_gasBarElementsPerPlayer[playerNum].title = $"{GameRoot.Instance.configData.MaxGasPerPlayer}/{GameRoot.Instance.configData.MaxGasPerPlayer}";

                int temp = playerNum;
                m_gasBarCallback.Add(playerNum,
                    (int oldGasValue, int newGasValue) =>
                    {
                        int maxGas = GameRoot.Instance.configData.MaxGasPerPlayer;
                        m_gasBarElementsPerPlayer[playerNum].value = (100f * newGasValue) / maxGas;
                        m_gasBarElementsPerPlayer[playerNum].title = $"{newGasValue}/{maxGas}";
                        
                        foreach (VisualElement child in m_gasBarElementsPerPlayer[playerNum].Q<VisualElement>("unity-progress-bar").Children())
                        {
                            child.style.backgroundColor = m_gasFillColorGradient.Evaluate(newGasValue / (float)maxGas);
                        }
                    });
                
                
                int currentGas = playerGameObject.GetComponent<PlayerNetworkBehaviour>().m_numGasRemaining.Value;
                int maxGas = GameRoot.Instance.configData.MaxGasPerPlayer;
                m_gasBarElementsPerPlayer[playerNum].value = (100f * currentGas) / maxGas;
                m_gasBarElementsPerPlayer[playerNum].title = $"{currentGas}/{maxGas}";
                
                foreach (VisualElement child in m_gasBarElementsPerPlayer[playerNum].Q<VisualElement>("unity-progress-bar").Children())
                {
                    child.style.backgroundColor = m_gasFillColorGradient.Evaluate(currentGas / (float)maxGas);
                }

                playerGameObject.GetComponent<PlayerNetworkBehaviour>().m_numGasRemaining.OnValueChanged += m_gasBarCallback[playerNum];
                
                m_gasRefillElementsPerPlayer.Add(playerNum, gasRefillElement);

                gasRefillElement.Q<VisualElement>("icon").style.backgroundImage = Background.FromSprite(playerIcon);

                gasRefillElement.Q<Button>("refill-button").clicked += () => OnGasRefillButtonClicked(guid);

                gasRefillElement.Q<Button>("disable-button").clicked += () => OnEnableButtonToggled(guid);

                gasRefillElement.Q<Button>("reset-button").clicked += () => OnPlayerResetButtonClicked(guid);
            }
        }
    }

    private void OnGasRefillButtonClicked(Guid playerGuid)
    {
        m_mouseClickSFX.Play();
        int playerNum = ClientConnectionHandler.Instance.serverSideClientList[playerGuid].playerNum;
        GameObject playerGameObject = GameObject.FindGameObjectWithTag($"Player{playerNum}");

        playerGameObject.GetComponent<PlayerNetworkBehaviour>().RefillGas();
    }
    
    private void OnEnableButtonToggled(Guid playerGuid)
    {
        m_mouseClickSFX.Play();
        int playerNum = ClientConnectionHandler.Instance.serverSideClientList[playerGuid].playerNum;
        GameObject playerGameObject = GameObject.FindGameObjectWithTag($"Player{playerNum}");

        PlayerNetworkBehaviour playerNetworkBehaviour = playerGameObject.GetComponent<PlayerNetworkBehaviour>();
        playerNetworkBehaviour.ToggleWhetherEnabled();

        Sprite backgroundImage = playerNetworkBehaviour.MovementEnabled ? m_movementDisableButtonEnabledSprite : m_movementDisableButtonDisabledSprite;

        string enableToggleButtonText = playerNetworkBehaviour.MovementEnabled ? "Disable" : "Enable";

        Button enableToggleButton = m_gasRefillElementsPerPlayer[playerNum].Q<Button>("disable-button");
        
        enableToggleButton.style.backgroundImage =
            Background.FromSprite(backgroundImage);
        enableToggleButton.text = enableToggleButtonText;
    }

    private void OnPlayerResetButtonClicked(Guid playerGuid)
    {
        m_mouseClickSFX.Play();
        int playerNum = ClientConnectionHandler.Instance.serverSideClientList[playerGuid].playerNum;
        GameObject playerGameObject = GameObject.FindGameObjectWithTag($"Player{playerNum}");

        PlayerNetworkBehaviour playerNetworkBehaviour = playerGameObject.GetComponent<PlayerNetworkBehaviour>();
        playerNetworkBehaviour.ResetPositionToSpawn();
    }
    
    
    private void OnOrderValueChanged(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];
        ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
        NetworkSerializableOrder order = OrderSystem.Instance.orders.Value.orders[orderIndex];
        if (order.orderTimeLimit != -1)
        {
            orderTimer.lowValue = 100f * order.orderTimeRemaining /
                                  (float)order.orderTimeLimit;
            orderTimer.title = $"{order.orderTimeRemaining}s";
            orderTimer.MarkDirtyRepaint();
        }
        
        List<(int, int)> destinationInventory =
            InventorySystem.Instance.GetInventory(order.destinationWarehouse, InventoryType.Destination);
        
        VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
        itemsContainer.Clear();
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
            slot.HoldItem(InventorySystem.Instance.m_items[itemNum], -1);
            slot.SetCountLabelText($"{quantityInDestinationInventory}/{order.requiredItems[key]}");
            itemsContainer.Add(slot);
        }
    }

    private void OnOrderComplete(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];
        
        if (OrderSystem.Instance.completeOrders.Value.arr[orderIndex] != 0)
        {
            orderElement.Q<VisualElement>("checkmark-overlay").style.visibility = Visibility.Visible;
        }
    }

    private void OnOrderIncomplete(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];
        
        if (OrderSystem.Instance.incompleteOrders.Value.arr[orderIndex] != 0)
        {
            orderElement.Q <VisualElement>("x-overlay").style.display = DisplayStyle.Flex;
        }
    }

    private void OnOrderRejected(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];

        if (OrderSystem.Instance.acceptedOrders.Value.arr[orderIndex] == 1)
        {
            orderElement.Q<VisualElement>("x-overlay").style.display = DisplayStyle.Flex;
        }
    }

    private void OnOrderSent(int orderIndex)
    {
        OrderSystem.Instance.SendOrder(orderIndex);
    }
}
