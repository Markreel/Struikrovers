using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Struikrovers.Game;

[CustomEditor(typeof(GameController))]
public class GameControllerEditor : Editor
{
    private SerializedProperty mapFileNameProperty;

    private void OnEnable()
    {
        mapFileNameProperty = serializedObject.FindProperty("mapFileName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "mapFileName");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Board Map", EditorStyles.boldLabel);

        string mapsFolder = Path.Combine(Application.dataPath, "StreamingAssets", "Maps");
        string[] mapFiles = Directory.Exists(mapsFolder)
            ? Directory.GetFiles(mapsFolder, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();

        if (!Directory.Exists(mapsFolder))
        {
            EditorGUILayout.HelpBox(
                "Create this folder in your project: Assets/StreamingAssets/Maps. Put only board-map JSON files in it.",
                MessageType.Warning);
        }
        else if (mapFiles.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No .json files found. Add your board maps to Assets/StreamingAssets/Maps.",
                MessageType.Warning);
        }
        else
        {
            string current = Path.GetFileName(mapFileNameProperty.stringValue ?? "");
            int selectedIndex = Array.IndexOf(mapFiles, current);
            if (selectedIndex < 0)
                selectedIndex = 0;

            int newIndex = EditorGUILayout.Popup("Map File", selectedIndex, mapFiles);
            if (newIndex >= 0 && newIndex < mapFiles.Length)
                mapFileNameProperty.stringValue = mapFiles[newIndex];
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Reload Board (Play Mode)"))
                ((GameController)target).Reload();
        }
    }
}