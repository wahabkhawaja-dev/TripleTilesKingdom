#if UNITY_EDITOR
using System.Collections.Generic;
using LevelSystem.Data;
using LevelSystem.Generation;
using UnityEditor;
using UnityEngine;
// LevelDefinitionBuilder lives in LevelSystem.Generation — kept explicit here so
// the Auto-fix button call site is easy to trace.

namespace LevelSystem.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="LevelDefinitionSO"/>: default field UI, plus an
    /// "Apply Preset" button, a compact validation report, and a top-down pyramid
    /// preview drawn from the actual coordinates <see cref="LevelDefinitionBuilder"/>
    /// would emit (base = big filled cells, upper layers = smaller inset cells with
    /// hue shift). Nothing here writes into the level at runtime — this is purely
    /// authoring UI.
    /// </summary>
    [CustomEditor(typeof(LevelDefinitionSO))]
    public sealed class LevelDefinitionSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var def = (LevelDefinitionSO)target;

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(def.PresetTemplate == null))
                {
                    if (GUILayout.Button("Apply Preset → Layers", GUILayout.Height(24)))
                    {
                        Undo.RecordObject(def, "Apply Level Preset");
                        def.Layers = def.PresetTemplate.BuildLayers();
                        EditorUtility.SetDirty(def);
                    }
                }
                if (GUILayout.Button("Reset to Classic 5-4-3", GUILayout.Height(24)))
                {
                    Undo.RecordObject(def, "Reset Layers");
                    def.Layers = new List<LayerDefinition>
                    {
                        LayerDefinition.Rect(5, 5),
                        LayerDefinition.Rect(4, 4),
                        LayerDefinition.Rect(3, 3),
                    };
                    EditorUtility.SetDirty(def);
                }
                if (GUILayout.Button("Auto-fix authored layers", GUILayout.Height(24)))
                {
                    Undo.RecordObject(def, "Auto-fix Layers");
                    def.Layers = LevelDefinitionBuilder.SolveCompleteGridLayers(
                        def.Layers, Mathf.Max(2, def.MatchCount));
                    EditorUtility.SetDirty(def);
                }
            }

            EditorGUILayout.Space(8);
            LevelPreviewDrawer.Draw(def);

            EditorGUILayout.Space(4);
            LevelPreviewDrawer.DrawValidation(def);
        }
    }
}
#endif
