using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CustomPropertyDrawer(typeof(VFXKeyAttribute))]
public class VFXKeyDrawer : PropertyDrawer
{
    private static VFXDatabase database;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Load VFXDatabase
        if (database == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:VFXDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                database = AssetDatabase.LoadAssetAtPath<VFXDatabase>(path);
            }
        }

        if (database == null || database.entries == null || database.entries.Count == 0)
        {
            // If database not found or has no entries, draw as standard text field
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Gather all keys from the database
        List<string> keys = database.entries
            .Select(e => e.key)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();

        // Always ensure "None" is at the top since it's the default/no-effect value
        if (!keys.Contains("None"))
        {
            keys.Insert(0, "None");
        }
        else
        {
            keys.Remove("None");
            keys.Insert(0, "None");
        }

        // Ensure the current value is in the list so we don't lose custom strings
        string currentValue = property.stringValue;
        if (!string.IsNullOrEmpty(currentValue) && !keys.Contains(currentValue))
        {
            keys.Add(currentValue);
        }

        int currentIndex = keys.IndexOf(currentValue);
        if (currentIndex < 0) currentIndex = 0; // default to first item

        // Draw dropdown popup
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, keys.ToArray());
        if (newIndex != currentIndex)
        {
            property.stringValue = keys[newIndex];
        }
    }
}
