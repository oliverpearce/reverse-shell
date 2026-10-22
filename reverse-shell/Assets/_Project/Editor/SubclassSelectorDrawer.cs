using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Combat.EditorTools
{
    /// <summary>
    /// Inspector for [SerializeReference, SubclassSelector] fields: a dropdown to pick the concrete
    /// type (e.g. DamageEffect, ApplyStatusEffect) plus that type's fields underneath.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            var header = new Rect(position.x, position.y, position.width, line);
            string typeName = CurrentTypeName(property);

            var foldRect = new Rect(header.x, header.y, EditorGUIUtility.labelWidth, line);
            var buttonRect = new Rect(header.x + EditorGUIUtility.labelWidth, header.y, header.width - EditorGUIUtility.labelWidth, line);
            bool hasChildren = HasVisibleChildren(property);
            var foldLabel = new GUIContent(typeName != null ? ObjectNames.NicifyVariableName(typeName) : label.text);
            if (hasChildren) property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, foldLabel, true);
            else EditorGUI.LabelField(foldRect, foldLabel);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(typeName != null ? "Change type" : "Choose type"), FocusType.Keyboard))
                ShowTypeMenu(property);
            EditorGUI.indentLevel = indent;

            if (hasChildren && property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = header.yMax + Spacing;
                foreach (var child in VisibleChildren(property))
                {
                    float h = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                    y += h + Spacing;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, true);

            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
                foreach (var child in VisibleChildren(property))
                    height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
            return height;
        }

        static IEnumerable<SerializedProperty> VisibleChildren(SerializedProperty property)
        {
            var it = property.Copy();
            var end = property.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                yield return it.Copy();
            }
        }

        static bool HasVisibleChildren(SerializedProperty property)
        {
            return VisibleChildren(property).Any();
        }

        /// <summary>"Combat.Runtime Combat.DamageEffect" -> "DamageEffect"; null when unassigned.</summary>
        static string CurrentTypeName(SerializedProperty property)
        {
            string full = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return null;
            int space = full.IndexOf(' ');
            string typeName = space >= 0 ? full.Substring(space + 1) : full;
            int dot = typeName.LastIndexOf('.');
            return dot >= 0 ? typeName.Substring(dot + 1) : typeName;
        }

        void ShowTypeMenu(SerializedProperty property)
        {
            var baseType = ElementType(fieldInfo.FieldType);
            var serializedObject = property.serializedObject;
            string path = property.propertyPath;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), false, () => Assign(serializedObject, path, null));
            menu.AddSeparator("");

            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsGenericType && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name);
            foreach (var type in types)
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => Assign(serializedObject, path, type));
            menu.ShowAsContext();
        }

        static void Assign(SerializedObject serializedObject, string path, Type type)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(path);
            if (property == null) return;
            property.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            property.isExpanded = true;
            serializedObject.ApplyModifiedProperties();
        }

        static Type ElementType(Type fieldType)
        {
            if (fieldType.IsArray) return fieldType.GetElementType();
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                return fieldType.GetGenericArguments()[0];
            return fieldType;
        }
    }
}
