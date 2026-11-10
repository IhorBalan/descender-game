using System;
using Descender.Fx;
using Descender.Triggers;
using UnityEditor;
using UnityEngine;

namespace Descender.EditorTools.Triggers
{
    /// <summary>
    /// The inspector of a TriggerZone: its fields, then its actions as a list where
    /// "Add action" offers every TriggerAction class there is, so a new action class
    /// shows up here with no editor code. And GameObject &gt; Descender &gt; Trigger zone,
    /// which makes a placeholder (a shape with a zone) where the scene view looks.
    /// </summary>
    [CustomEditor(typeof(TriggerZone))]
    public sealed class TriggerZoneEditor : Editor
    {
        private const string ActionsField = "actions";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", ActionsField);
            var actions = serializedObject.FindProperty(ActionsField);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions, run in order", EditorStyles.boldLabel);
            for (var i = 0; i < actions.arraySize; i++)
            {
                if (DrawAction(actions, i))
                {
                    break;
                }
            }
            if (GUILayout.Button("Add action"))
            {
                ShowMenu();
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>One action with its fields and a Remove button. True when it was removed.</summary>
        private static bool DrawAction(SerializedProperty actions, int index)
        {
            var element = actions.GetArrayElementAtIndex(index);
            var value = element.managedReferenceValue;
            var title = value != null ? ObjectNames.NicifyVariableName(value.GetType().Name) : "(empty)";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{index + 1}. {title}", EditorStyles.miniBoldLabel);
            var removed = GUILayout.Button("Remove", GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            if (removed)
            {
                actions.DeleteArrayElementAtIndex(index);
                return true;
            }
            EditorGUI.indentLevel++;
            var child = element.Copy();
            var end = element.GetEndProperty();
            var inside = child.NextVisible(true);
            while (inside && !SerializedProperty.EqualContents(child, end))
            {
                EditorGUILayout.PropertyField(child, true);
                inside = child.NextVisible(false);
            }
            EditorGUI.indentLevel--;
            return false;
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<TriggerAction>())
            {
                if (type.IsAbstract)
                {
                    continue;
                }
                var chosen = type;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => Add(chosen));
            }
            menu.ShowAsContext();
        }

        private void Add(Type type)
        {
            serializedObject.Update();
            var actions = serializedObject.FindProperty(ActionsField);
            actions.arraySize++;
            var added = actions.GetArrayElementAtIndex(actions.arraySize - 1);
            added.managedReferenceValue = Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/Descender/Trigger zone", false, 10)]
        public static void CreateZone()
        {
            var view = SceneView.lastActiveSceneView;
            Vector2 at = view != null ? (Vector2)view.pivot : Vector2.zero;
            var shape = Shape.Make(null, "Trigger zone", ShapeKind.Diamond, at, Vector2.one, Color.yellow, "item");
            var go = shape.gameObject;
            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            go.AddComponent<TriggerZone>();
            Undo.RegisterCreatedObjectUndo(go, "Create trigger zone");
            Selection.activeGameObject = go;
        }
    }
}
