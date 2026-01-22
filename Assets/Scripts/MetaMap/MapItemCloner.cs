using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 通用地图项克隆器 - 使用反射实现完全自动化的深拷贝
/// 优点：完全自动，无需在子类中实现任何方法
/// 缺点：性能稍低，需要小心处理特殊类型
/// </summary>
public static class MapItemCloner
{
    /// <summary>
    /// 通用克隆方法 - 使用反射自动复制所有字段
    /// </summary>
    public static T CloneWithReflection<T>(T source, GameObject prefab) where T : MapItem
    {
        if (source == null || prefab == null)
            return null;

        // 创建新实例
        GameObject newGO = GameObject.Instantiate(prefab);
        T target = newGO.GetComponent<T>();

        if (target == null)
            return null;

        // 使用反射复制所有字段
        CopyAllFields(source, target);

        return target;
    }

    /// <summary>
    /// 复制所有字段（包括私有字段）
    /// </summary>
    private static void CopyAllFields(object source, object target)
    {
        if (source == null || target == null)
            return;

        Type type = source.GetType();

        // 获取所有字段（包括私有、公有、继承的）
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                             BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        
        FieldInfo[] fields = type.GetFields(flags);

        foreach (FieldInfo field in fields)
        {
            // 跳过不应该复制的字段
            if (ShouldSkipField(field))
                continue;

            object value = field.GetValue(source);

            // 深拷贝集合类型
            if (value != null)
            {
                value = DeepCopyValue(value);
            }

            field.SetValue(target, value);
        }
    }

    /// <summary>
    /// 判断是否应该跳过该字段
    /// </summary>
    private static bool ShouldSkipField(FieldInfo field)
    {
        // 跳过 GameObject 相关字段（Unity自己管理）
        if (typeof(GameObject).IsAssignableFrom(field.FieldType))
            return true;
        
        if (typeof(Transform).IsAssignableFrom(field.FieldType))
            return true;
        
        if (typeof(Component).IsAssignableFrom(field.FieldType))
            return true;

        // 跳过带有 NonSerialized 特性的字段
        if (field.IsDefined(typeof(NonSerializedAttribute), false))
            return true;

        return false;
    }

    /// <summary>
    /// 深拷贝值（处理集合类型）
    /// </summary>
    private static object DeepCopyValue(object value)
    {
        if (value == null)
            return null;

        Type type = value.GetType();

        // 值类型和字符串直接返回
        if (type.IsValueType || type == typeof(string))
            return value;

        // List<Vector2> - 最常见的情况
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type itemType = type.GetGenericArguments()[0];
            
            // List<Vector2>
            if (itemType == typeof(Vector2))
            {
                return new List<Vector2>((List<Vector2>)value);
            }
            // List<Vector3>
            else if (itemType == typeof(Vector3))
            {
                return new List<Vector3>((List<Vector3>)value);
            }
            // 其他 List 类型
            else
            {
                IList sourceList = (IList)value;
                IList targetList = (IList)Activator.CreateInstance(type);
                foreach (var item in sourceList)
                {
                    targetList.Add(DeepCopyValue(item));
                }
                return targetList;
            }
        }

        // Dictionary
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            IDictionary sourceDict = (IDictionary)value;
            IDictionary targetDict = (IDictionary)Activator.CreateInstance(type);
            foreach (DictionaryEntry entry in sourceDict)
            {
                targetDict.Add(entry.Key, DeepCopyValue(entry.Value));
            }
            return targetDict;
        }

        // 数组
        if (type.IsArray)
        {
            Array sourceArray = (Array)value;
            Array targetArray = Array.CreateInstance(type.GetElementType(), sourceArray.Length);
            for (int i = 0; i < sourceArray.Length; i++)
            {
                targetArray.SetValue(DeepCopyValue(sourceArray.GetValue(i)), i);
            }
            return targetArray;
        }

        // 默认：直接返回引用（可能需要根据实际情况调整）
        return value;
    }

    /// <summary>
    /// 自动检测位置字段并应用偏移
    /// </summary>
    public static void ApplyPositionOffsetAuto(MapItem item, Vector2 offset)
    {
        if (item == null)
            return;

        Type type = item.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                             BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        // 查找名为 "position" 的 Vector2 字段
        FieldInfo posField = type.GetField("position", flags);
        if (posField != null && posField.FieldType == typeof(Vector2))
        {
            Vector2 currentPos = (Vector2)posField.GetValue(item);
            posField.SetValue(item, currentPos + offset);
            return;
        }

        // 查找 List<Vector2> 类型的字段（如 positionLine, positinLineL, positinLineR）
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.FieldType == typeof(List<Vector2>))
            {
                List<Vector2> points = (List<Vector2>)field.GetValue(item);
                if (points != null)
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        points[i] += offset;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 自动检测并获取中心位置
    /// </summary>
    public static Vector2 GetCenterPositionAuto(MapItem item)
    {
        if (item == null)
            return Vector2.zero;

        Type type = item.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                             BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        // 1. 优先查找 "position" 字段
        FieldInfo posField = type.GetField("position", flags);
        if (posField != null && posField.FieldType == typeof(Vector2))
        {
            return (Vector2)posField.GetValue(item);
        }

        // 2. 查找第一个 List<Vector2> 类型的字段，计算其中心
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.FieldType == typeof(List<Vector2>))
            {
                List<Vector2> points = (List<Vector2>)field.GetValue(item);
                if (points != null && points.Count > 0)
                {
                    Vector2 center = Vector2.zero;
                    foreach (Vector2 point in points)
                    {
                        center += point;
                    }
                    return center / points.Count;
                }
            }
        }

        return Vector2.zero;
    }

    /// <summary>
    /// 打印对象的所有字段（调试用）
    /// </summary>
    public static void DebugPrintFields(MapItem item)
    {
        if (item == null)
            return;

        Type type = item.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                             BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        Debug.Log($"=== {type.Name} 的所有字段 ===");
        foreach (FieldInfo field in type.GetFields(flags))
        {
            object value = field.GetValue(item);
            Debug.Log($"{field.Name} ({field.FieldType.Name}): {value}");
        }
    }
}
