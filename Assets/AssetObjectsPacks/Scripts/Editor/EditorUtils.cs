﻿

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AssetObjectsPacks {
    public static class EditorUtils {
        public static string MakeDirectoryIfNone (string directory) {
            if(!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }
        public static string[] GetFilePathsInDirectory (string dir, bool include_dir, string file_extenstion, string valid_file_check, bool should_contain, SearchOption search = SearchOption.AllDirectories) {
            string data_path = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            //int sub_index = data_path.Length + (include_dir ? -6 : (1 + dir.Length));//Assets ...;  
            int sub_index = data_path.Length + (include_dir ? 1 : (1 + dir.Length));//Assets ...;  
            return Directory.GetFiles(data_path+"/"+dir, "*" + file_extenstion, search)
                .Where(s => s.Contains(valid_file_check) == should_contain)
                .Select(s => s.Substring(sub_index))
                .ToArray();
        }
        public static T GetAssetAtPath<T> (string path) where T : Object {
            Object[] data = AssetDatabase.LoadAllAssetsAtPath(path);
            System.Type t = typeof(T);
            int l = data.Length;
            for (int i = 0; i < l; ++i) {
                Object d = data[i];
                if (d.GetType() == t) {
                    return (T)d;
                }
                
            }
            return null;
        }
        public static T[] GetAssetsAtPath<T> (string path) where T : Object {
            System.Type t = typeof(T);
            List<T> ret = new List<T>();
            Object[] data = AssetDatabase.LoadAllAssetsAtPath(path);
            int l = data.Length;
            for (int i = 0; i < l; ++i) {
                Object d = data[i];
                if (d == null) {
                    Debug.Log("null at: " + path);

                }
                else {

                    if (d.GetType() == t) {
                        ret.Add((T)d);
                    }
                }
            }
            return ret.ToArray();
                
        }
        const string back_slash = "/";
        const char back_slash_c = '/';
         
        public static string[] DirectoryNameSplit(string full_path) {
            if (!full_path.Contains(back_slash)) {
                Debug.LogError(full_path + " isnt a valid path");
                return null;
            }
            string[] sp = full_path.Split(back_slash_c);
            string name = sp.Last();
            string dir = string.Join(back_slash, sp.Slice(0, sp.Length - 1)) + back_slash;
            return new string[] {dir, name};
        }



        #region SERIALIZED_PROPERTY_EXTENSIONS

        public static bool Contains (this SerializedProperty p, string e, out int at_index) {
            at_index = -1;
            for (int i = 0; i < p.arraySize; i++) {
                if (p.GetArrayElementAtIndex(i).stringValue == e) {
                    at_index = i;
                    return true;
                }
            }
            return false;
        }
        public static bool Contains (this SerializedProperty p, int e, out int at_index) {
            at_index = -1;
            for (int i = 0; i < p.arraySize; i++) {
                if (p.GetArrayElementAtIndex(i).intValue == e) {
                    at_index = i;
                    return true;
                }
            }
            return false;
        }
        
        public static void Add (this SerializedProperty p, string e) {
            int c = p.arraySize;
            p.InsertArrayElementAtIndex(c);
            p.GetArrayElementAtIndex(c).stringValue = e;
        }
        public static void Add (this SerializedProperty p, int e) {
            int c = p.arraySize;
            p.InsertArrayElementAtIndex(c);
            p.GetArrayElementAtIndex(c).intValue = e;
        }

        public static void Remove (this SerializedProperty p, string e) {
            int i;
            if (p.Contains(e, out i)) {
                p.DeleteArrayElementAtIndex(i);
            }
        }
        public static void Remove (this SerializedProperty p, int e) {
            int i;
            if (p.Contains(e, out i)) {
                p.DeleteArrayElementAtIndex(i);
            }
        }
        public static bool Contains (this SerializedProperty p, string e) {
            int at_index;
            return p.Contains(e, out at_index);
        }
        public static bool Contains (this SerializedProperty p, int e) {
            int at_index;
            return p.Contains(e, out at_index);
        }

        
        public static void CopyProperty(this SerializedProperty p, SerializedProperty c) {
            if (p.propertyType != c.propertyType) {
                Debug.LogError("Incompatible types (" + p.propertyType + ", " + c.propertyType + ")");
                return;
            }
            switch (p.propertyType){
                case SerializedPropertyType.Integer	:p.intValue = c.intValue;break;
                case SerializedPropertyType.Boolean	:p.boolValue = c.boolValue;break;
                case SerializedPropertyType.Float	:p.floatValue = c.floatValue;break;
                case SerializedPropertyType.String	:p.stringValue = c.stringValue;break;
                case SerializedPropertyType.Color	:p.colorValue = c.colorValue;break;
                case SerializedPropertyType.ObjectReference	:p.objectReferenceValue = c.objectReferenceValue;break;
                case SerializedPropertyType.Enum	:p.enumValueIndex = c.enumValueIndex;break;
                case SerializedPropertyType.Vector2	:p.vector2Value = c.vector2Value;break;
                case SerializedPropertyType.Vector3	:p.vector3Value = c.vector3Value;break;
                case SerializedPropertyType.Vector4	:p.vector4Value = c.vector4Value;break;
                case SerializedPropertyType.Rect	:p.rectValue = c.rectValue;break;
                case SerializedPropertyType.AnimationCurve	:p.animationCurveValue = c.animationCurveValue;break;
                case SerializedPropertyType.Bounds	:p.boundsValue = c.boundsValue;break;
                case SerializedPropertyType.Quaternion	:p.quaternionValue = c.quaternionValue;break;
                case SerializedPropertyType.ExposedReference:	p.exposedReferenceValue = c.exposedReferenceValue;break;
                case SerializedPropertyType.Vector2Int:	p.vector2IntValue = c.vector2IntValue;break;
                case SerializedPropertyType.Vector3Int:	p.vector3IntValue = c.vector3IntValue;break;
                case SerializedPropertyType.RectInt	:p.rectIntValue = c.rectIntValue;break;
                case SerializedPropertyType.BoundsInt	:p.boundsIntValue = c.boundsIntValue;break;
                default:Debug.LogError("Not implemented: " + p.propertyType);break;
            }
        }

        #endregion


       
        
        
        



        
        
        
        
        
    }

}






