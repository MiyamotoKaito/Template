#if UNITY_2019_3_OR_NEWER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    /// <summary>
    /// 型情報をキャッシュするための静的な辞書。
    /// Key: 基底クラスの型
    /// Value: (派生型の配列, ポップアップ表示用の型名の配列, 完全修飾名の配列)
    /// </summary>
    private static readonly Dictionary<Type, (Type[] types, string[] names, string[] fullNames)> s_TypeCache = new();

    /// <summary>
    /// プロパティのGUIを描画する。
    /// </summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        // --- Popup の Rect（1行分）---
        Rect popupRect = new Rect(position)
        {
            height = EditorGUIUtility.singleLineHeight
        };

        // --- 本体フィールドの Rect（残り全部）---
        Rect fieldRect = new Rect(position)
        {
            y = position.y + EditorGUIUtility.singleLineHeight,
            height = position.height - EditorGUIUtility.singleLineHeight
        };

        // ---- 必要なキャッシュ処理・型処理 ----
        var baseType = GetType(property);
        if (baseType == null)
        {
            EditorGUI.PropertyField(fieldRect, property, label, true);
            return;
        }

        if (!s_TypeCache.TryGetValue(baseType, out var cachedData))
        {
            cachedData = CreateInheritedTypesCache(baseType, ((SubclassSelectorAttribute)attribute).IsIncludeMono());
            s_TypeCache.Add(baseType, cachedData);
        }
        var (inheritedTypes, typePopupNameArray, typeFullNameArray) = cachedData;

        // 現在の型インデックス
        int currentTypeIndex = Array.IndexOf(typeFullNameArray, property.managedReferenceFullTypename);
        if (currentTypeIndex < 0)
            currentTypeIndex = 0;

        // ---- Popup を描画 ----
        int selectedTypeIndex = EditorGUI.Popup(popupRect, currentTypeIndex, typePopupNameArray);

        // ---- 型変更処理 ----
        if (currentTypeIndex != selectedTypeIndex)
        {
            Type selectedType = inheritedTypes[selectedTypeIndex];
            property.managedReferenceValue =
                selectedType == null ? null : Activator.CreateInstance(selectedType);
        }

        // ---- フィールド本体の描画 ----
        EditorGUI.PropertyField(fieldRect, property, label, true);
    }

    /// <summary>
    /// プロパティの高さを取得する。
    /// </summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float baseHeight = EditorGUI.GetPropertyHeight(property, true);
        return baseHeight + EditorGUIUtility.singleLineHeight;
    }

    /// <summary>
    /// 指定された基底クラスを継承する全ての型情報を収集し、キャッシュデータを作成する。
    /// </summary>
    /// <param name="baseType">基底クラスの型。</param>
    /// <param name="includeMono">MonoBehaviourを継承する型を含めるかどうか。</param>
    /// <returns>派生型情報のキャッシュデータ。</returns>
    private static (Type[], string[], string[]) CreateInheritedTypesCache(Type baseType, bool includeMono)
    {
        Type monoType = typeof(MonoBehaviour);

        // 1. abstractクラスも含む、すべての派生型リストを作成する (カテゴリ判定用)
        var allInheritedTypesWithAbstract = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => baseType.IsAssignableFrom(p) && p.IsClass && (!monoType.IsAssignableFrom(p) || includeMono))
            .ToArray();

        // 2. ユーザーが選択可能な、abstractでない派生型リストを作成する
        var selectableTypesArray = allInheritedTypesWithAbstract
            .Where(t => !t.IsAbstract)
            .ToArray();

        // <null>オプションを先頭に追加する。
        var finalSelectableTypes = selectableTypesArray.Prepend(null).ToArray();

        // 3. ポップアップ表示用の型名配列を作成する
        var typePopupNameArray = finalSelectableTypes.Select(type =>
        {
            if (type == null) return "<null>";

            string displayName;
            var parent = type.BaseType;

            // 親クラスが「中間クラス」(abstract含む)である場合、カテゴリ分けする。
            // 判定には allInheritedTypesWithAbstract を使用する。
            if (parent != null && parent != baseType && allInheritedTypesWithAbstract.Contains(parent))
            {
                displayName = $"{parent.Name}/{type.Name}";
            }
            // 自身が他クラスの「中間クラス」(abstract含む)である場合、自身をカテゴリのルートとして表示する。
            // このロジックは、abstractクラス自身は選択肢にないので、具象クラスが中間クラスになる場合のみ適用される。
            else if (allInheritedTypesWithAbstract.Any(t => t.BaseType == type))
            {
                displayName = $"{type.Name}/{type.Name}";
            }
            // 上記以外の場合は、カテゴリ分けせずに表示する。
            else
            {
                displayName = type.Name;
            }

            // ネストクラスの '+' を '/' に置換する。
            if (displayName.Contains('+'))
            {
                displayName = displayName.Replace('+', '/');
            }

            return displayName;
        }).ToArray();

        // 完全修飾名の配列を作成する。
        var typeFullNameArray = finalSelectableTypes.Select(type => type == null ? "" : $"{type.Assembly.GetName().Name} {type.FullName}").ToArray();

        // 最終的に返却するのは、選択可能な型と、それに対応する表示名。
        return (finalSelectableTypes, typePopupNameArray, typeFullNameArray);
    }

    /// <summary>
    /// ポップアップGUIの描画範囲を取得する。
    /// </summary>
    /// <param name="currentPosition">現在のプロパティの描画範囲。</param>
    /// <returns>ポップアップの描画範囲。</returns>
    private Rect GetPopupPosition(Rect currentPosition)
    {
        Rect popupPosition = new Rect(currentPosition);
        popupPosition.width -= EditorGUIUtility.labelWidth;
        popupPosition.x += EditorGUIUtility.labelWidth;
        popupPosition.height = EditorGUIUtility.singleLineHeight;
        return popupPosition;
    }

    // 15. 内部メソッド、抽出メソッド
    /// <summary>
    /// SerializedPropertyから、そのプロパティの基底となる型を取得する。
    /// </summary>
    /// <param name="property">対象のプロパティ。</param>
    /// <returns>プロパティの基底型。</returns>
    public static Type GetType(SerializedProperty property)
    {
        // ManagedReference の場合は Unity の型情報を使う
        if (property.propertyType == SerializedPropertyType.ManagedReference)
        {
            if (string.IsNullOrEmpty(property.managedReferenceFieldTypename))
                return null;

            // "AssemblyName TypeFullName" 形式になっている
            string[] parts = property.managedReferenceFieldTypename.Split(' ');
            if (parts.Length == 2)
            {
                return Type.GetType($"{parts[1]}, {parts[0]}");
            }
        }

        // それ以外は従来通り FieldInfo から取る
        FieldInfo fieldInfo = GetFieldInfo(property);
        if (fieldInfo == null)
            return null;

        Type fieldType = fieldInfo.FieldType;

        if (fieldType.IsArray)
            return fieldType.GetElementType();

        if (fieldType.IsGenericType &&
            fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            return fieldType.GetGenericArguments()[0];
        }

        return fieldType;
    }

    /// <summary>
    /// SerializedPropertyのパスを解析し、対応するFieldInfoを取得する。
    /// </summary>
    /// <param name="property">対象のプロパティ。</param>
    /// <returns>対応するFieldInfo。</returns>
    public static FieldInfo GetFieldInfo(SerializedProperty property)
    {
        string propertyPath = property.propertyPath;
        string[] elements = propertyPath.Replace(".Array.data[", "[").Split('.');

        Type currentType = property.serializedObject.targetObject.GetType();
        FieldInfo field = null;

        foreach (string element in elements)
        {
            if (element.Contains("["))
            {
                string fieldName = element.Substring(0, element.IndexOf("["));

                // フィールドを親クラスまで遡って取得
                field = GetFieldInfoFromType(currentType, fieldName);
                if (field == null)
                    return null;

                Type fieldType = field.FieldType;

                // 配列
                if (fieldType.IsArray)
                {
                    currentType = fieldType.GetElementType();
                }
                // List<T>
                else if (fieldType.IsGenericType &&
                         fieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    currentType = fieldType.GetGenericArguments()[0];
                }
                else
                {
                    // ★重要★
                    // ここで currentType = fieldType にしてよい
                    // 配列でもリストでもない場合は普通のフィールド
                    currentType = fieldType;
                }
            }
            else
            {
                // 通常フィールド名
                field = GetFieldInfoFromType(currentType, element);
                if (field == null)
                    return null;

                currentType = field.FieldType;
            }
        }

        return field;
    }

    private static FieldInfo GetFieldInfoFromType(Type type, string fieldName)
    {
        FieldInfo field = null;

        // 継承階層をさかのぼって検索
        for (Type t = type; t != null; t = t.BaseType)
        {
            field = t.GetField(fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            if (field != null) break;
        }

        return field;
    }
}
#endif