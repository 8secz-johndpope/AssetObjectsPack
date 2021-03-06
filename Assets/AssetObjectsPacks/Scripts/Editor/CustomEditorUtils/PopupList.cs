﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace AssetObjectsPacks {
    public class PopupList : PopupWindowContent {
        public class ListElement {
            public GUIContent m_Content;
            public bool selected;
            public ListElement(string text, bool selected) {
                m_Content = new GUIContent(text);
                this.selected = selected;
            }
        }
        public class InputData{
            public HashSet<ListElement> m_ListElements = new HashSet<ListElement>();
            public System.Action<ListElement> m_OnSelectCallback;
            public void NewOrMatchingElement(string label, bool selected) {
                foreach (var element in m_ListElements) {
                    if (element.m_Content.text == label) {
                        element.selected = selected;
                        return;
                    }
                }
                m_ListElements.Add(new ListElement(label, selected));
            }
        }
        const float k_LineHeight = 16, k_Margin = 5;
        GUIStyle menuItem = "MenuItem", background = "grey_border";
        InputData m_Data;
        int m_SelectedCompletionIndex = 0;
        public PopupList(InputData inputData){
            m_Data = inputData;
            m_SelectedCompletionIndex = -1;
        }
        public override Vector2 GetWindowSize(){
            return new Vector2(150f, m_Data.m_ListElements.Count * k_LineHeight + 2 * k_Margin);
        }
        public override void OnGUI(Rect rect){
            UnityEngine.Event evt = UnityEngine.Event.current;
            // We do not use the layout event
            if (evt.type == EventType.Layout) return;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape){
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
            DrawList(rect);
            // Background with 1 pixel border (rendered above content)
            if (evt.type == EventType.Repaint) background.Draw(new Rect(rect.x, rect.y, rect.width, rect.height), false, false, false, false);
        }
        void DrawList(Rect rect){
            UnityEngine.Event evt = UnityEngine.Event.current;
            int i = -1;
            foreach (var element in m_Data.m_ListElements) {
                i++;
                Rect label_rect = new Rect(rect.x, rect.y + k_Margin + i * k_LineHeight, rect.width, k_LineHeight);
                switch (evt.type){
                    case EventType.Repaint:{
                        bool isHover = i == m_SelectedCompletionIndex;
                        menuItem.Draw(label_rect, element.m_Content, isHover, element.selected, element.selected, false);                            
                    }
                    break;
                    case EventType.MouseDown:{
                        if (evt.button == 0) {
                            if (label_rect.Contains(evt.mousePosition)) {
                                if (m_Data.m_OnSelectCallback != null) m_Data.m_OnSelectCallback(element);
                                evt.Use();
                                editorWindow.Close();
                            }
                        }
                    }
                    break;
                    case EventType.MouseMove:{
                        if (label_rect.Contains(evt.mousePosition)){
                            m_SelectedCompletionIndex = i;
                            evt.Use();
                        }
                    }
                    break;
                }
            }
        }
    }
}