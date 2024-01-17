using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class RemoveComponentFromPrefabsWindow : EditorWindow
{
    GameObject temp;

    TextField folderInput;
    InspectorElement inspector;

    Button runScriptButton;
    Toggle recursiveToggle;
    bool recursiveToggleValue;

    InspectorInput inspectorObj;
    

    [MenuItem("MurraysTools/BatchComponentRemoval")]
    public static void OpenBatchComponentRemoveWindow() {
        EditorWindow wnd = GetWindow<RemoveComponentFromPrefabsWindow>();
        wnd.titleContent = new GUIContent("Batch Remove Component to Prefabs");
    }

    public void CreateGUI()
    {
        temp = new GameObject();
        
        inspectorObj = temp.AddComponent<InspectorInput>();
        
        folderInput = new TextField("");
        
        inspector = new InspectorElement(inspectorObj);

        runScriptButton = new Button();
        runScriptButton.RegisterCallback<ClickEvent>(OnRunScriptClicked);
        runScriptButton.text = "Remove On All Prefabs";

        recursiveToggle = new Toggle();
        recursiveToggle.RegisterCallback<ChangeEvent<bool>>((evt) => 
        {
            recursiveToggleValue = evt.newValue;
        });

        rootVisualElement.Add(new Label("Prefab Folder:"));
        rootVisualElement.Add(folderInput);
        rootVisualElement.Add(new Label("Recursive:"));
        rootVisualElement.Add(recursiveToggle);        
        rootVisualElement.Add(new Label("Component to copy:"));
        rootVisualElement.Add(inspector);
        rootVisualElement.Add(runScriptButton);

        inspector.style.flexGrow = new StyleFloat(0.1f);
        runScriptButton.style.flexGrow = new StyleFloat(0.1f);
    }

    public void OnDestroy() {
        if (temp != null) {
            DestroyImmediate(temp, true);
        }
    }

    private void OnRunScriptClicked(ClickEvent evt) {
        string folder = folderInput.value;
        Component component = inspectorObj.component;

        RemoveComponentFromAllPrefabsInFolder(folder, component, recursiveToggleValue);
    }


    private void RemoveComponentFromAllPrefabsInFolder(string folderPath, Component component, bool recursive, bool isRoot = true) {
        
        List<GameObject> allMapItems;

        string[] fileEntries = Directory.GetFiles(Application.dataPath+"/"+folderPath);
        allMapItems = new List<GameObject>();
        foreach (string entry in fileEntries) {
            if (entry.EndsWith(".prefab")) {
                
                if (folderPath.Contains("Resources")) {
                    string filePath = string.Join("/", folderPath.Split("/")[1..^0]) + "/" + entry.Split("/").Last().Split("\\").Last().Split(".")[0];
                    allMapItems.Add((GameObject)Resources.Load(filePath, typeof(GameObject)));
                }
                else {
                    string filePath = folderPath + "/" + entry.Split("/").Last().Split("\\").Last();
                    if (!filePath.StartsWith("Assets")) {
                        filePath = "Assets/" + filePath;
                    }
                    allMapItems.Add((GameObject)AssetDatabase.LoadAssetAtPath<GameObject>(filePath));

                }
            }
        }

        Type componentType = component.GetType();

        foreach (GameObject mapItem in allMapItems) {
            GameObject instance = PrefabUtility.InstantiatePrefab(mapItem as GameObject) as GameObject;
            

            var componentOnDisk = mapItem.GetComponent(componentType);
            if (componentOnDisk != null) {

                var componentInstance = instance.GetComponent(componentType);

                DestroyImmediate(componentInstance, true);
                PrefabUtility.ApplyRemovedComponent(instance, componentOnDisk, InteractionMode.AutomatedAction);
            }
            DestroyImmediate(instance, true);
        }


        if (recursive) {
            string[] subDirectories = Directory.GetDirectories(Application.dataPath+"/"+folderPath);
            
            foreach (string directory in subDirectories) {
                RemoveComponentFromAllPrefabsInFolder(folderPath + "/" + directory.Split("/").Last().Split("\\").Last(), component, recursive, false);
            }
        }
        
    }
}
