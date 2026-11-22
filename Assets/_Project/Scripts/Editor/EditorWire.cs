using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Sets serialized (including private [SerializeField]) fields by name. Throws if a field does not
    /// exist so a typo is caught while running the setup tool instead of becoming a null reference at runtime.
    /// </summary>
    internal static class EditorWire
    {
        public static void Set(UnityEngine.Object target, string field, object value)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target), $"Wire target is null (field '{field}').");

            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");

            Assign(prop, value, $"{target.GetType().Name}.{field}");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty p, object value, string label)
        {
            if (value == null)
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference)
                    throw new InvalidOperationException($"{label}: null assigned to non-reference property.");
                p.objectReferenceValue = null;
                return;
            }

            switch (value)
            {
                case UnityEngine.Object o:
                    p.objectReferenceValue = o;
                    return;
                case string s:
                    p.stringValue = s;
                    return;
                case bool b:
                    p.boolValue = b;
                    return;
                case int i:
                    p.intValue = i;
                    return;
                case float f:
                    p.floatValue = f;
                    return;
                case Color c:
                    p.colorValue = c;
                    return;
                case Vector3 v3:
                    p.vector3Value = v3;
                    return;
                case Vector2 v2:
                    p.vector2Value = v2;
                    return;
                case IList list:
                    p.arraySize = list.Count;
                    for (int k = 0; k < list.Count; k++)
                        Assign(p.GetArrayElementAtIndex(k), list[k], $"{label}[{k}]");
                    return;
                default:
                    throw new InvalidOperationException($"{label}: unsupported value type {value.GetType().Name}.");
            }
        }
    }
}
