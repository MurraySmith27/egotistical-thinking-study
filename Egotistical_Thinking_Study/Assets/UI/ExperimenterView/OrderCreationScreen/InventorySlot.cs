using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventorySlot : VisualElement
{
    public VisualElement Icon;
    public string ItemGuid = "";
    public int count = 0;

    private Label countLabel;

    public InventorySlot(bool interactable = true)
    {
        Icon = new VisualElement();
        Icon.AddToClassList("slot-icon");
        this.Add(Icon);

        countLabel = new Label();
        countLabel.AddToClassList("item-count");
        this.Add(countLabel);
        
        this.AddToClassList("slot");

        if (interactable)
        {
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }
    }

    public void SetItemTint(Color color)
    {
        Icon.style.unityBackgroundImageTintColor = color;
    }
    
    public void HoldItem(ItemDetails itemDetails, int itemCount)
    {
        Icon.style.backgroundImage = itemDetails.Icon.texture;
        ItemGuid = itemDetails.GUID;
        count = itemCount;

        countLabel.text = $"{itemCount}";
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || ItemGuid.Equals(""))
        {
            return;
        }

        ClientMenuController.Instance.StartDrag(evt.position, this);
    }

    public void DropItem()
    {
        ItemGuid = "";
        Icon.style.backgroundImage = null;
    }
}
