using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace TilemapEditor.Data
{
    /// <summary>
    /// 瓦片库，存储预制体引用列表
    /// </summary>
    [CreateAssetMenu(fileName = "New Tile Palette", menuName = "Tilemap Editor/Tile Palette")]
    public class TilePalette : SerializedScriptableObject
    {
        [Title("Palette Settings")]
        [SerializeField] private string paletteName = "New Palette";

        [Title("Tile Prefabs")]
        [InfoBox("请勿将场景中的实例拖拽到此处，请直接从 Project 面板拖拽预制体，否则会导致引用丢失。", InfoMessageType.Warning)]
        [ListDrawerSettings(OnTitleBarGUI = "DrawTitleBarGUI")]
        [SerializeField]
        private List<GameObject> tilePrefabs = new List<GameObject>();

        #region Properties
        public string PaletteName
        {
            get => paletteName;
            set => paletteName = value;
        }

        public List<GameObject> TilePrefabs
        {
            get => tilePrefabs;
            set => tilePrefabs = value;
        }

        /// <summary>
        /// 获取预制体数量
        /// </summary>
        [ShowInInspector]
        [DisplayAsString]
        [PropertyOrder(0)]
        public int Count => tilePrefabs.Count;
        #endregion

        #region Public Methods
        /// <summary>
        /// 通过索引获取预制体
        /// </summary>
        public GameObject GetPrefab(int index)
        {
            if (index >= 0 && index < tilePrefabs.Count)
                return tilePrefabs[index];
            return null;
        }

        /// <summary>
        /// 添加预制体到库中
        /// </summary>
        public void AddPrefab(GameObject prefab)
        {
            if (prefab != null && !tilePrefabs.Contains(prefab))
            {
                tilePrefabs.Add(prefab);
            }
        }

        /// <summary>
        /// 从库中移除预制体
        /// </summary>
        public void RemovePrefab(GameObject prefab)
        {
            tilePrefabs.Remove(prefab);
        }

        /// <summary>
        /// 从库中移除预制体（通过索引）
        /// </summary>
        public void RemovePrefabAt(int index)
        {
            if (index >= 0 && index < tilePrefabs.Count)
            {
                tilePrefabs.RemoveAt(index);
            }
        }

        /// <summary>
        /// 清空库
        /// </summary>
        public void ClearPrefabs()
        {
            tilePrefabs.Clear();
        }

        /// <summary>
        /// 获取预制体的索引
        /// </summary>
        public int GetPrefabIndex(GameObject prefab)
        {
            return tilePrefabs.IndexOf(prefab);
        }

        /// <summary>
        /// 检查是否包含指定预制体
        /// </summary>
        public bool ContainsPrefab(GameObject prefab)
        {
            return tilePrefabs.Contains(prefab);
        }

        /// <summary>
        /// 通过GUID获取预制体的索引
        /// </summary>
        public int GetPrefabIndexByGUID(string guid)
        {
            for (int i = 0; i < tilePrefabs.Count; i++)
            {
                if (tilePrefabs[i] == null) continue;
#if UNITY_EDITOR
                string prefabGUID = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(tilePrefabs[i]));
                if (prefabGUID == guid)
                {
                    return i;
                }
#endif
            }
            return -1;
        }

        /// <summary>
        /// 验证库的有效性 - 清理空引用的预制体
        /// </summary>
        [Button(ButtonSizes.Large), ButtonGroup("Actions")]
        public void ValidatePalette()
        {
            // 移除空引用
            tilePrefabs.RemoveAll(prefab => prefab == null);
        }

        [InfoBox("提示：验证功能用于清理库中的空引用，移除已被删除或丢失的预制体引用", InfoMessageType.Info)]
        [PropertyOrder(100)]
        [ShowInInspector]
        private void ValidationTip() { }
        #endregion

        #region Odin Methods
        private void DrawTitleBarGUI()
        {
            // 标题栏GUI可以在这里添加其他功能
        }

        [Button(ButtonSizes.Large), ButtonGroup("Actions")]
        private void SortByName()
        {
            tilePrefabs.Sort((a, b) => a.name.CompareTo(b.name));
        }

        [Button(ButtonSizes.Large), ButtonGroup("Actions")]
        [GUIColor(1, 0.6f, 0.6f)]
        private void ClearAllPrefabs()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.DisplayDialog("清空所有预制体", "确定要清空此库中的所有预制体吗？", "是", "否"))
            {
                tilePrefabs.Clear();
            }
#else
            tilePrefabs.Clear();
#endif
        }

        private void OnValidate()
        {
            ValidatePalette();
        }
        #endregion
    }
}