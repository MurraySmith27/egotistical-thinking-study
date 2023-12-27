using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SFB;

public class ServerControlMenuController : MonoBehaviour
{

    private Label mapFileLabel;
    private Button loadMapFileButton;
    // Start is called before the first frame update
    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        mapFileLabel = root.Q<Label>("map-file-label");

        loadMapFileButton = root.Q<Button>("load-map-file-button");
        

        loadMapFileButton.clicked += LoadMapFile;

    }

    void LoadMapFile() {
        Debug.Log("called load map file!");
        var mapFilePath = StandaloneFileBrowser.OpenFilePanel("Open File", "", "", false);

        Debug.Log(mapFilePath);
    }
}
