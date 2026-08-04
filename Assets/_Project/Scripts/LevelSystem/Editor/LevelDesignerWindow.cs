#if UNITY_EDITOR
using System.IO;
using LevelSystem.Data;
using LevelSystem.Generation;
using UnityEditor;
using UnityEngine;

namespace LevelSystem.EditorTools
{
    /// <summary>
    /// Master Level Designer (Tools ▸ Level Designer). One window, four tabs:
    ///
    ///   Overview  — collection status, quick actions, and inline how-to.
    ///   Batch     — configure a difficulty curve and generate N levels at once. This
    ///               is the intended primary workflow: fill the collection with a full
    ///               ramp in seconds, then tweak individual levels afterward.
    ///   Levels    — per-level list + full inspector for the selected level.
    ///   Presets   — quick browser of every LevelPresetSO in the project, so a
    ///               designer can drop a shape onto the selected level from here too.
    ///
    /// Every write goes through Undo (Undo.RecordObject / RegisterCreatedObjectUndo)
    /// and calls AssetDatabase.SaveAssets after batch operations, so nothing is lost
    /// if Unity is force-killed mid-authoring.
    /// </summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        private const string DefaultCollectionPath = "Assets/Resources/Levels/LevelCollection.asset";
        private const string LevelsFolder = "Assets/Resources/Levels";

        private enum Tab { Overview, Batch, Levels, Presets }

        [SerializeField] private Tab _tab = Tab.Overview;
        [SerializeField] private LevelCollectionSO _collection;
        [SerializeField] private int _selectedIndex;
        [SerializeField] private LevelPresetSO _presetToApply;
        [SerializeField] private DifficultyCurve _curve = new DifficultyCurve();
        [SerializeField] private int _batchCount = 50;
        [SerializeField] private bool _batchReplaceExisting = true;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private Vector2 _mainScroll;

        [MenuItem("Tools/Level Designer")]
        public static void Open()
        {
            var w = GetWindow<LevelDesignerWindow>("Level Designer");
            w.minSize = new Vector2(820, 560);
            w.TryAutoLoadCollection();
        }

        private void OnEnable() => TryAutoLoadCollection();

        private void TryAutoLoadCollection()
        {
            if (_collection != null) return;
            _collection = AssetDatabase.LoadAssetAtPath<LevelCollectionSO>(DefaultCollectionPath);
        }

        // -------------------------------------------------------------------
        // Layout
        // -------------------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(2);
            DrawTabBar();
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case Tab.Overview: DrawOverviewTab(); break;
                case Tab.Batch: DrawBatchTab(); break;
                case Tab.Levels: DrawLevelsTab(); break;
                case Tab.Presets: DrawPresetsTab(); break;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Collection:", GUILayout.Width(70));
                var newCol = (LevelCollectionSO)EditorGUILayout.ObjectField(_collection, typeof(LevelCollectionSO), false, GUILayout.Width(280));
                if (newCol != _collection) { _collection = newCol; _selectedIndex = 0; }

                if (GUILayout.Button("Create at Resources/Levels", EditorStyles.toolbarButton, GUILayout.Width(180)))
                {
                    _collection = CreateCollectionAsset();
                    _tab = Tab.Batch;
                }
                if (GUILayout.Button("Ping Runtime Asset", EditorStyles.toolbarButton, GUILayout.Width(140)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<LevelCollectionSO>(DefaultCollectionPath);
                    if (asset != null) { _collection = asset; EditorGUIUtility.PingObject(asset); }
                    else EditorUtility.DisplayDialog("Level Designer", "No collection found at " + DefaultCollectionPath, "OK");
                }
                GUILayout.FlexibleSpace();
                if (_collection != null && GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    SaveEverything();
                }
            }
        }

        private void DrawTabBar()
        {
            var tabs = new[] { "Overview", "Batch Generator", "Levels", "Presets" };
            var index = (int)_tab;
            var newIndex = GUILayout.Toolbar(index, tabs, GUILayout.Height(28));
            if (newIndex != index) _tab = (Tab)newIndex;
        }

        // -------------------------------------------------------------------
        // Overview tab
        // -------------------------------------------------------------------

        private void DrawOverviewTab()
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            EditorGUILayout.LabelField("Master Level Designer", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "The runtime loads Assets/Resources/Levels/LevelCollection.asset at boot. This window is the single place to build and edit that collection.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                if (_collection == null)
                {
                    EditorGUILayout.LabelField("No collection loaded.");
                    if (GUILayout.Button("Create Collection at " + DefaultCollectionPath, GUILayout.Height(28)))
                    {
                        _collection = CreateCollectionAsset();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"Collection: {_collection.name}");
                    EditorGUILayout.LabelField($"Levels: {_collection.Count}");
                    var atRuntime = AssetDatabase.GetAssetPath(_collection) == DefaultCollectionPath;
                    EditorGUILayout.LabelField(atRuntime
                        ? "✓ At runtime path — the game will use this collection."
                        : "⚠ Not at " + DefaultCollectionPath + " — runtime won't find it.");
                }
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Recommended workflow", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "1. Create the collection (button above, or Batch tab does it for you).\n" +
                    "2. Go to Batch Generator, set how many levels you want (default 50 — matches the Level Select screen) and how difficulty should ramp.\n" +
                    "3. Click Generate. Every level asset is written under Assets/Resources/Levels and added to the collection in order.\n" +
                    "4. Go to Levels to tweak any individual level (change its layers, match count, tray size).\n" +
                    "5. Press Play — level index comes from PlayerPrefs['LevelToPlay'], so the existing Level Select screen just works.",
                    EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Quick actions", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_collection == null))
                    {
                        if (GUILayout.Button("Open Batch Generator", GUILayout.Height(28))) _tab = Tab.Batch;
                        if (GUILayout.Button("Edit Levels", GUILayout.Height(28))) _tab = Tab.Levels;
                        if (GUILayout.Button("Browse Presets", GUILayout.Height(28))) _tab = Tab.Presets;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // -------------------------------------------------------------------
        // Batch tab
        // -------------------------------------------------------------------

        private void DrawBatchTab()
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            if (_collection == null)
            {
                EditorGUILayout.HelpBox("Create a LevelCollection first (Overview tab).", MessageType.Warning);
                if (GUILayout.Button("Create Collection", GUILayout.Height(28)))
                {
                    _collection = CreateCollectionAsset();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField("Batch Generator", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "Set how many levels to generate and how difficulty should ramp from the first to the last. Every axis is a start value and an end value; the curve shape controls how fast it interpolates.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _batchCount = EditorGUILayout.IntSlider("How many levels", _batchCount, 1, 200);
                _batchReplaceExisting = EditorGUILayout.Toggle(
                    new GUIContent("Replace existing", "On: clears the collection and regenerates from scratch. Off: appends after the existing levels."),
                    _batchReplaceExisting);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Difficulty curve", EditorStyles.boldLabel);
            DrawCurveInspector(_curve);

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                var bigButton = new GUIStyle(GUI.skin.button) { fixedHeight = 36, fontStyle = FontStyle.Bold };
                if (GUILayout.Button($"Generate {_batchCount} levels", bigButton))
                {
                    if (EditorUtility.DisplayDialog(
                            "Batch Generate",
                            (_batchReplaceExisting ? "This will REPLACE the current " + _collection.Count + " level(s)." : "This will APPEND " + _batchCount + " levels after the current " + _collection.Count + ".") +
                            "\n\nContinue?",
                            "Generate", "Cancel"))
                    {
                        RunBatchGenerate();
                    }
                }
                if (GUILayout.Button("Preview level 0", GUILayout.Width(120), GUILayout.Height(36)))
                {
                    PreviewIndex(0);
                }
                if (GUILayout.Button($"Preview level {_batchCount - 1}", GUILayout.Width(140), GUILayout.Height(36)))
                {
                    PreviewIndex(_batchCount - 1);
                }
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preview snapshots", EditorStyles.boldLabel);
                DrawBatchSnapshot("Start (level 1)", 0);
                DrawBatchSnapshot($"Mid (level {_batchCount / 2 + 1})", _batchCount / 2);
                DrawBatchSnapshot($"End (level {_batchCount})", _batchCount - 1);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCurveInspector(DifficultyCurve c)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Rules", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.StartMatchCount = EditorGUILayout.IntField("Match count (start)", c.StartMatchCount);
                    c.EndMatchCount = EditorGUILayout.IntField("(end)", c.EndMatchCount);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.StartTraySize = EditorGUILayout.IntField("Tray size (start)", c.StartTraySize);
                    c.EndTraySize = EditorGUILayout.IntField("(end)", c.EndTraySize);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.StartTileTypes = EditorGUILayout.IntField("Tile faces (start)", c.StartTileTypes);
                    c.EndTileTypes = EditorGUILayout.IntField("(end)", c.EndTileTypes);
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Board", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.StartBaseWidth = EditorGUILayout.IntField("Base width (start)", c.StartBaseWidth);
                    c.EndBaseWidth = EditorGUILayout.IntField("(end)", c.EndBaseWidth);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.StartBaseHeight = EditorGUILayout.IntField("Base height (start)", c.StartBaseHeight);
                    c.EndBaseHeight = EditorGUILayout.IntField("(end)", c.EndBaseHeight);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.StartLayerCount = EditorGUILayout.IntField("Layers (start)", c.StartLayerCount);
                    c.EndLayerCount = EditorGUILayout.IntField("(end)", c.EndLayerCount);
                }
                c.ShrinkPerLayer = EditorGUILayout.IntField(
                    new GUIContent("Shrink per layer", "How many cells each layer shrinks by (width & height) from the layer below."),
                    c.ShrinkPerLayer);
                c.ShapeAcrossBatch = (LevelPresetSO.PresetShape)EditorGUILayout.EnumPopup("Shape", c.ShapeAcrossBatch);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Curve", EditorStyles.miniBoldLabel);
                c.CurveShape = (DifficultyCurve.Shape)EditorGUILayout.EnumPopup(
                    new GUIContent("Interpolation", "Linear = even ramp. EaseIn = gentle then steep. EaseOut = steep then gentle. EaseInOut = S-curve."),
                    c.CurveShape);
                c.ThemeId = EditorGUILayout.TextField("Theme id", c.ThemeId);
                c.DeterministicSeed = EditorGUILayout.Toggle(
                    new GUIContent("Deterministic seed", "On: identical layouts across sessions for the same level. Off: fresh shuffle every level rebuild."),
                    c.DeterministicSeed);
            }
        }

        private void DrawBatchSnapshot(string label, int index)
        {
            if (_batchCount <= 0) return;
            index = Mathf.Clamp(index, 0, _batchCount - 1);
            var scratch = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            try
            {
                BatchLevelGenerator.Configure(scratch, _curve, index, _batchCount);
                var baseLayer = scratch.Layers.Count > 0 ? scratch.Layers[0] : new LayerDefinition { Width = 0, Height = 0 };
                EditorGUILayout.LabelField(
                    $"{label}: match {scratch.MatchCount} · tray {scratch.TraySize} · base {baseLayer.Width}×{baseLayer.Height} · {scratch.Layers.Count} layers · {scratch.TotalTileCount} tiles",
                    EditorStyles.miniLabel);
            }
            finally { DestroyImmediate(scratch); }
        }

        private void PreviewIndex(int index)
        {
            if (_batchCount <= 0) return;
            index = Mathf.Clamp(index, 0, _batchCount - 1);
            var scratch = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            BatchLevelGenerator.Configure(scratch, _curve, index, _batchCount);
            scratch.name = $"Preview_L{index + 1}";
            Selection.activeObject = scratch;
            EditorGUIUtility.PingObject(scratch);
        }

        private void RunBatchGenerate()
        {
            EnsureFolder(LevelsFolder);
            Undo.RegisterCompleteObjectUndo(_collection, "Batch Generate Levels");

            if (_batchReplaceExisting)
            {
                foreach (var lvl in _collection.Levels)
                {
                    if (lvl == null) continue;
                    var path = AssetDatabase.GetAssetPath(lvl);
                    if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
                }
                _collection.Levels.Clear();
            }

            var startIndex = _collection.Count;
            for (var i = 0; i < _batchCount; i++)
            {
                var absoluteIndex = startIndex + i;
                var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/Level_{absoluteIndex + 1:000}.asset");
                var def = CreateInstance<LevelDefinitionSO>();
                BatchLevelGenerator.Configure(def, _curve, i, _batchCount);
                AssetDatabase.CreateAsset(def, path);
                _collection.Levels.Add(def);
                if (i % 8 == 0)
                {
                    EditorUtility.DisplayProgressBar("Generating levels", $"Level {i + 1}/{_batchCount}", (float)(i + 1) / _batchCount);
                }
            }

            EditorUtility.ClearProgressBar();
            SaveEverything();
            _selectedIndex = 0;
            _tab = Tab.Levels;
            Repaint();
        }

        // -------------------------------------------------------------------
        // Levels tab
        // -------------------------------------------------------------------

        private void DrawLevelsTab()
        {
            if (_collection == null)
            {
                EditorGUILayout.HelpBox("Create a LevelCollection first (Overview tab).", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLevelList();
                DrawSelectedLevel();
            }
        }

        private void DrawLevelList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(260)))
            {
                EditorGUILayout.LabelField($"Levels ({_collection.Count})", EditorStyles.boldLabel);
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));
                for (var i = 0; i < _collection.Levels.Count; i++)
                {
                    var level = _collection.Levels[i];
                    var label = level != null
                        ? $"{i + 1:000} · {(string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName)}"
                        : $"{i + 1:000} · <missing>";
                    var was = _selectedIndex == i;
                    var now = GUILayout.Toggle(was, label, "Button");
                    if (now && !was) _selectedIndex = i;
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ New")) AddNewLevel();
                    using (new EditorGUI.DisabledScope(_collection.Count == 0))
                    {
                        if (GUILayout.Button("Duplicate")) DuplicateSelected();
                        if (GUILayout.Button("Remove")) RemoveSelected();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_selectedIndex <= 0))
                    {
                        if (GUILayout.Button("↑ Up")) Move(-1);
                    }
                    using (new EditorGUI.DisabledScope(_selectedIndex >= _collection.Count - 1))
                    {
                        if (GUILayout.Button("↓ Down")) Move(+1);
                    }
                }
            }
        }

        private void DrawSelectedLevel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                if (_selectedIndex < 0 || _selectedIndex >= _collection.Count)
                {
                    EditorGUILayout.HelpBox("Select a level on the left.", MessageType.Info);
                }
                else
                {
                    var def = _collection.Levels[_selectedIndex];
                    if (def == null)
                    {
                        EditorGUILayout.HelpBox("Missing asset at this index. Remove it or reassign.", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(def.name, EditorStyles.boldLabel);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _presetToApply = (LevelPresetSO)EditorGUILayout.ObjectField("Preset", _presetToApply, typeof(LevelPresetSO), false);
                            using (new EditorGUI.DisabledScope(_presetToApply == null))
                            {
                                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                                {
                                    Undo.RecordObject(def, "Apply Preset");
                                    def.Layers = _presetToApply.BuildLayers();
                                    def.PresetTemplate = _presetToApply;
                                    EditorUtility.SetDirty(def);
                                }
                            }
                        }

                        EditorGUILayout.Space(6);
                        var editor = UnityEditor.Editor.CreateEditor(def);
                        editor.OnInspectorGUI();
                        DestroyImmediate(editor);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // -------------------------------------------------------------------
        // Presets tab
        // -------------------------------------------------------------------

        private void DrawPresetsTab()
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            EditorGUILayout.LabelField("Presets", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "Reusable shapes. Create one with Assets ▸ Create ▸ TripleTilesKingdom ▸ Level Preset. Apply from the Levels tab or directly from the buttons below.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            var guids = AssetDatabase.FindAssets("t:LevelPresetSO");
            if (guids.Length == 0)
            {
                EditorGUILayout.HelpBox("No LevelPresetSO assets found in project.", MessageType.Info);
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<LevelPresetSO>(path);
                if (preset == null) continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(preset.name, GUILayout.Width(180));
                    EditorGUILayout.LabelField(
                        $"{preset.Shape} · base {preset.BaseWidth}×{preset.BaseHeight} · {preset.LayerCount}L · shrink {preset.ShrinkPerLayer}",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("Ping", GUILayout.Width(60)))
                    {
                        EditorGUIUtility.PingObject(preset);
                    }
                    using (new EditorGUI.DisabledScope(_collection == null || _selectedIndex < 0 || _selectedIndex >= _collection.Count || _collection.Levels[_selectedIndex] == null))
                    {
                        if (GUILayout.Button("Apply to selected", GUILayout.Width(140)))
                        {
                            var def = _collection.Levels[_selectedIndex];
                            Undo.RecordObject(def, "Apply Preset");
                            def.Layers = preset.BuildLayers();
                            def.PresetTemplate = preset;
                            EditorUtility.SetDirty(def);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // -------------------------------------------------------------------
        // Collection ops
        // -------------------------------------------------------------------

        private LevelCollectionSO CreateCollectionAsset()
        {
            EnsureFolder(LevelsFolder);
            var existing = AssetDatabase.LoadAssetAtPath<LevelCollectionSO>(DefaultCollectionPath);
            if (existing != null) return existing;
            var asset = CreateInstance<LevelCollectionSO>();
            AssetDatabase.CreateAsset(asset, DefaultCollectionPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private void AddNewLevel()
        {
            EnsureFolder(LevelsFolder);
            var index = _collection.Count;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/Level_{index + 1:000}.asset");
            var def = CreateInstance<LevelDefinitionSO>();
            def.LevelId = index + 1;
            def.DisplayName = $"Level {index + 1}";
            AssetDatabase.CreateAsset(def, path);
            Undo.RegisterCreatedObjectUndo(def, "New Level");
            _collection.Levels.Add(def);
            EditorUtility.SetDirty(_collection);
            AssetDatabase.SaveAssets();
            _selectedIndex = index;
        }

        private void DuplicateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _collection.Count) return;
            var src = _collection.Levels[_selectedIndex];
            if (src == null) return;
            EnsureFolder(LevelsFolder);
            var index = _collection.Count;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/Level_{index + 1:000}.asset");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(src), path);
            var copy = AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>(path);
            copy.LevelId = index + 1;
            copy.DisplayName = $"Level {index + 1}";
            _collection.Levels.Add(copy);
            EditorUtility.SetDirty(copy);
            EditorUtility.SetDirty(_collection);
            AssetDatabase.SaveAssets();
            _selectedIndex = index;
        }

        private void RemoveSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _collection.Count) return;
            var level = _collection.Levels[_selectedIndex];
            _collection.Levels.RemoveAt(_selectedIndex);
            if (level != null &&
                EditorUtility.DisplayDialog("Delete asset?", $"Also delete the underlying asset '{level.name}'?", "Delete", "Keep"))
            {
                var path = AssetDatabase.GetAssetPath(level);
                if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
            }
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _collection.Count - 1);
            EditorUtility.SetDirty(_collection);
        }

        private void Move(int delta)
        {
            var to = _selectedIndex + delta;
            if (to < 0 || to >= _collection.Count) return;
            (_collection.Levels[_selectedIndex], _collection.Levels[to]) = (_collection.Levels[to], _collection.Levels[_selectedIndex]);
            _selectedIndex = to;
            EditorUtility.SetDirty(_collection);
        }

        private void SaveEverything()
        {
            EditorUtility.SetDirty(_collection);
            foreach (var lvl in _collection.Levels)
            {
                if (lvl != null) EditorUtility.SetDirty(lvl);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
