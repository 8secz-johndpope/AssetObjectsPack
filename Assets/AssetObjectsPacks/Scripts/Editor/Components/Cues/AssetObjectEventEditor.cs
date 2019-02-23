﻿using UnityEngine;
using UnityEditor;
namespace AssetObjectsPacks {
    [CustomEditor(typeof(Cue))]
    public class CueEditor : Editor
    {
        GUIContent snap_style_gui = new GUIContent("Snap Style", "If the event should wait for the player to snap to the event transform before being considered ready");
        GUIContent pos_smooth_gui = new GUIContent("Position Time (s)");
        GUIContent rot_smooth_gui = new GUIContent("Rotation Time (s)");
        new Cue target;
        EditorProp so;
        void OnEnable () {
            this.target = base.target as Cue;
            so = new EditorProp( serializedObject );
        }
        public override void OnInspectorGUI() {
            //base.OnInspectorGUI();

            GUIUtils.StartCustomEditor();

            //GUIUtils.Space(1);

            GUIUtils.StartBox(0);

            EditorGUI.indentLevel++;
            

            GUIUtils.DrawProp(so[Cue.sendMessageField], new GUIContent("Send Message: ", "Method should take a Transform for parameter"));

            GUIUtils.DrawProp(so[Cue.snap_player_style_field], snap_style_gui);
            if (target.snapPlayerStyle == Cue.SnapPlayerStyle.Smooth) {
                EditorGUI.indentLevel++;
                GUIUtils.DrawProp(so[Cue.smooth_pos_time_field], pos_smooth_gui);
                GUIUtils.DrawProp(so[Cue.smooth_rot_time_field], rot_smooth_gui);
                EditorGUI.indentLevel--;
            }
            GUIUtils.DrawProp(so[Cue.playlist_field]);
            if (target.playlist == null) {
                //GUIUtils.BeginIndent(1);
                //GUIUtils.Space(2);
                GUIUtils.DrawArrayProp( so[Cue.event_packs_field] );

                //GUIUtils.EndIndent();
            }
            EditorGUI.indentLevel--;
            
            GUIUtils.EndBox(0);

            GUIUtils.EndCustomEditor(so);
                
        }
    }
}