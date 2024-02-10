using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientMenuController : MonoBehaviour
{

    [SerializeField] private GameObject m_inventoryMenuGameObject;
    private VisualElement m_root;
    private Button m_viewInventoryButton;
    
    void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("root");

        m_viewInventoryButton = m_root.Q<Button>("view-inventory-button");
        m_viewInventoryButton.clicked += OpenInventoryMenu;
    }

    void OpenInventoryMenu()
    {
        m_inventoryMenuGameObject.SetActive(true);
        this.gameObject.SetActive(false);
    }

}
