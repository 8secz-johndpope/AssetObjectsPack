﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AssetObjectsPacks {
    enum EditorPropType { Property, Array, SO }
    public class EditorProp {
        EditorPropType editorPropType;
        public SerializedProperty prop;
        SerializedObject obj;
        bool _wasChanged;
        Dictionary<string, EditorProp> name2Prop = new Dictionary<string, EditorProp>();

        public void ResetChanged () {
            _wasChanged = false;
            foreach (var e in arrayElements) e.ResetChanged();
            foreach (var n in name2Prop.Keys) name2Prop[n].ResetChanged();
        }
        public bool IsChanged() {
            if (_wasChanged) return true;
            foreach (var e in arrayElements) {
                if (e.IsChanged()) return true;
            }
            foreach (var n in name2Prop.Keys) {
                if (name2Prop[n].IsChanged()) return true;
            }
            return false;
        }


        public EditorProp (SerializedProperty prop) {
            this.prop = prop;
            editorPropType = prop.isArray && prop.propertyType != SerializedPropertyType.String ? EditorPropType.Array : EditorPropType.Property;
            //Debug.Log(editorPropType);
            if (editorPropType == EditorPropType.Array) RebuildArray(); 
        }
        public EditorProp (SerializedObject obj) {
            this.obj = obj;
            editorPropType = EditorPropType.SO;
        }

        bool CheckEditorPropType (EditorPropType shouldBe) {
            if (shouldBe != editorPropType) {
                Debug.LogError ( (editorPropType == EditorPropType.SO ? obj.targetObject.name : prop.displayName) + "(" + editorPropType.ToString() + ") isnt type: " + shouldBe.ToString());
                return false;
            }
            return true;
        }
        public void SaveObject () {
            if (!CheckEditorPropType(EditorPropType.SO)) return;
            EditorUtility.SetDirty(obj.targetObject);
            obj.ApplyModifiedProperties();
            ResetChanged();
        }

        //get property/relative
        public EditorProp this [string name] {
            get {
                //if (editorPropType == EditorPropType.Array) {
                //    Debug.LogError (prop.displayName + " is an array type, use integer indexing");
                //    return null;
                //}
                EditorProp customProp;
                if (!name2Prop.TryGetValue(name, out customProp)) {
                    customProp = new EditorProp ( editorPropType == EditorPropType.SO ? obj.FindProperty ( name ) : prop.FindPropertyRelative ( name ) );
                    name2Prop.Add(name, customProp);
                }
                return customProp;
            }
        }

        #region ARRAY_PROP
            List<EditorProp> arrayElements = new List<EditorProp>();

            //index into array prop
            public EditorProp this [int index] {
                get {
                    if (!CheckForArray(false)) return null;
                    if (index < 0 || index >= arraySize) {
                        Debug.LogError("Array index out of range");
                        return null;
                    }
                    return arrayElements[index];
                }
            }
            bool CheckForArray(bool change) {
                if (!CheckEditorPropType(EditorPropType.Array)) return false;
                if (change) _wasChanged = true;
                return true;
            }
            void RebuildArray () {
                arrayElements = new List<EditorProp>().Generate(arraySize, i => new EditorProp( prop.GetArrayElementAtIndex(i) ) );
            }
            public bool ContainsElementsWithDuplicateNames (out string duplicateName, string checkNameField="name") {
                duplicateName = string.Empty;
                if (!CheckForArray(false)) return false;
                int l = arraySize;
                for (int i = 0; i < l; i++) {
                    string oName = this[i][checkNameField].stringValue;
                    for (int x = i+1; x < l; x++) {
                        if (oName == this[x][checkNameField].stringValue) {
                            duplicateName = oName;
                            return true;
                        }
                    }
                }
                return false;
            }

            public void Clear () {
                if (!CheckForArray(true)) return;
                prop.ClearArray();
                arrayElements.Clear();
            }
            public EditorProp AddNew () {
                if (!CheckForArray(true)) return null;
                int l = prop.arraySize;
                prop.InsertArrayElementAtIndex(l);
                arrayElements.Add( new EditorProp( prop.GetArrayElementAtIndex(l) ) );
                return arrayElements.Last();
            }
            public void DeleteAt (int index) {
                if (!CheckForArray(true)) return;
                prop.DeleteArrayElementAtIndex(index);
                RebuildArray(); 
            }
        #endregion

        #region GETTERS_SETTERS
            #region GETTERS
                public int intValue { get { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return 0;
                    return prop.intValue;
                } }
                public float floatValue { get { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return 0;
                    return prop.floatValue; 
                } }
                public bool boolValue { get { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return false;
                    return prop.boolValue; 
                } }
                public string stringValue { get { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return "";
                    return prop.stringValue; 
                } }
                public int enumValueIndex { get { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return 0;
                    return prop.enumValueIndex; 
                } }
                public int arraySize { get { 
                    if (!CheckEditorPropType(EditorPropType.Array)) return 0;
                    return prop.arraySize; 
                } }
            #endregion
            #region SETTERS
                public void SetEnumValue (int value) { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return;
                    prop.enumValueIndex = value; 
                }
                public void SetValue (bool value) { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return;
                    prop.boolValue = value; 
                }
                public void SetValue (float value) { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return;
                    prop.floatValue = value; 
                }
                public void SetValue (int value) { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return;
                    prop.intValue = value; 
                }
                public void SetValue (Object value) { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return;
                    prop.objectReferenceValue = value; 
                }
                public void SetValue (string value) { 
                    if (!CheckEditorPropType(EditorPropType.Property)) return;
                    prop.stringValue = value; 
                }
            #endregion
        #endregion

        public void CopyProp (EditorProp copy) {
            if (!CheckEditorPropType(EditorPropType.Property)) return;

            SerializedProperty c = copy.prop;
            if (prop.propertyType != c.propertyType) {
                Debug.LogError("Incompatible types (" + prop.propertyType + ", " + c.propertyType + ")");
                return;
            }

            //_wasChanged = true; 
            switch (prop.propertyType){
                case SerializedPropertyType.Integer	            :   prop.intValue              =    c.intValue              ;break;
                case SerializedPropertyType.Boolean	            :   prop.boolValue             =    c.boolValue             ;break;
                case SerializedPropertyType.Float	            :   prop.floatValue            =    c.floatValue            ;break;
                case SerializedPropertyType.String	            :   prop.stringValue           =    c.stringValue           ;break;
                case SerializedPropertyType.Color	            :   prop.colorValue            =    c.colorValue            ;break;
                case SerializedPropertyType.ObjectReference	    :   prop.objectReferenceValue  =    c.objectReferenceValue  ;break;
                case SerializedPropertyType.Enum	            :   prop.enumValueIndex        =    c.enumValueIndex        ;break;
                case SerializedPropertyType.Vector2	            :   prop.vector2Value          =    c.vector2Value          ;break;
                case SerializedPropertyType.Vector3	            :   prop.vector3Value          =    c.vector3Value          ;break;
                case SerializedPropertyType.Vector4	            :   prop.vector4Value          =    c.vector4Value          ;break;
                case SerializedPropertyType.Rect	            :   prop.rectValue             =    c.rectValue             ;break;
                case SerializedPropertyType.AnimationCurve	    :   prop.animationCurveValue   =    c.animationCurveValue   ;break;
                case SerializedPropertyType.Bounds	            :   prop.boundsValue           =    c.boundsValue           ;break;
                case SerializedPropertyType.Quaternion	        :   prop.quaternionValue       =    c.quaternionValue       ;break;
                case SerializedPropertyType.ExposedReference    :   prop.exposedReferenceValue =    c.exposedReferenceValue ;break;
                case SerializedPropertyType.Vector2Int          :   prop.vector2IntValue       =    c.vector2IntValue       ;break;
                case SerializedPropertyType.Vector3Int          :   prop.vector3IntValue       =    c.vector3IntValue       ;break;
                case SerializedPropertyType.RectInt	            :   prop.rectIntValue          =    c.rectIntValue          ;break;
                case SerializedPropertyType.BoundsInt	        :   prop.boundsIntValue        =    c.boundsIntValue        ;break;
                
                default:Debug.LogError("Not implemented: " + prop.propertyType);break;
            }
        }
    }
}