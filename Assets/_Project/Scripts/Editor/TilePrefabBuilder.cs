#if UNITY_EDITOR
using System.IO;
using Presentation;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Regenerates <c>Assets/Resources/Prefabs/TileView.prefab</c> as the
    /// sprite-tile prefab: <c>BoxCollider2D</c> + two <c>SpriteRenderer</c>
    /// children (Base + Fruit) wired into <see cref="TileView"/>'s serialized
    /// fields. Run once after pulling the sprite conversion via
    /// <c>Tools ▸ Rebuild Tile Prefab</c>; also chained into
    /// <c>Tools ▸ Build Game Scenes</c>.
    /// </summary>
    public static class TilePrefabBuilder
    {
        private const string PrefabFolder = "Assets/Resources/Prefabs";
        private const string PrefabPath = "Assets/Resources/Prefabs/TileView.prefab";
        private const string TileThemePath = "Assets/Resources/TileTheme_Default.asset";

        [MenuItem("Tools/Rebuild Tile Prefab")]
        public static void Rebuild()
        {
            EnsureFolder(PrefabFolder);

            var baseSprite = LoadDefaultBaseSprite();

            var root = new GameObject("TileView");
            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1f);
            var tileView = root.AddComponent<TileView>();

            var baseGO = new GameObject("Base");
            baseGO.transform.SetParent(root.transform, false);
            var baseRenderer = baseGO.AddComponent<SpriteRenderer>();
            baseRenderer.sortingOrder = 0;
            if (baseSprite != null)
            {
                baseRenderer.sprite = baseSprite;
                ScaleRendererToUnitSize(baseRenderer);
            }

            var fruitGO = new GameObject("Fruit");
            fruitGO.transform.SetParent(root.transform, false);
            // 0.3 matches Presentation.TrayController._iconScale default so a
            // fruit sprite is the same visible size on the board and in the
            // tray. Change both together if you retune.
            fruitGO.transform.localScale = Vector3.one * 0.3f;
            var fruitRenderer = fruitGO.AddComponent<SpriteRenderer>();
            fruitRenderer.sortingOrder = 1;

            var so = new SerializedObject(tileView);
            so.FindProperty("_baseRenderer").objectReferenceValue = baseRenderer;
            so.FindProperty("_fruitRenderer").objectReferenceValue = fruitRenderer;
            so.FindProperty("_collider").objectReferenceValue = collider;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (File.Exists(PrefabPath)) AssetDatabase.DeleteAsset(PrefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TilePrefabBuilder] Rebuilt {PrefabPath}.");
        }

        private static Sprite LoadDefaultBaseSprite()
        {
            var theme = AssetDatabase.LoadAssetAtPath<TileThemeSO>(TileThemePath);
            return theme != null ? theme.BaseTileSprite : null;
        }

        private static void ScaleRendererToUnitSize(SpriteRenderer renderer)
        {
            if (renderer.sprite == null) return;
            var size = renderer.sprite.bounds.size;
            var maxDim = Mathf.Max(size.x, size.y);
            if (maxDim <= 0.0001f) return;
            var scale = 1f / maxDim;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
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
