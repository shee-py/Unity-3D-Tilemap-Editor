using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TilemapEditor.Data;
using Sirenix.OdinInspector;

namespace TilemapEditor.Runtime
{
    /// <summary>
    /// 网格地图组件，挂载于场景中，作为地图容器
    /// </summary>
    [System.Serializable]
    public class GridMap : SerializedMonoBehaviour
    {
        [BoxGroup("Grid Settings")]
        [SerializeField] private Vector3Int gridSize = new Vector3Int(50, 1, 50);
        [BoxGroup("Grid Settings")]
        [SerializeField] private float cellSize = 1f;
        [BoxGroup("Grid Settings")]
        [SerializeField] private Vector3 gridOffset = Vector3.zero;

        [BoxGroup("Display Settings")]
        [SerializeField] private bool showGrid = true;
        [BoxGroup("Display Settings")]
        [ShowIf("showGrid")]
        [SerializeField] private Color gridColor = Color.white;

        [Title("Map Data")]
        [AssetSelector] // 移除固定路径限制，允许选择任何位置的TilePalette资源
        [SerializeField] private TilePalette currentPalette;

        [ShowInInspector]
        [ReadOnly]
        [ProgressBar(0, "@gridSize.x * gridSize.y * gridSize.z", Height = 20)]
        public int TileCount => tileDataList.Count;

        [DetailedInfoBox("Tile Data (Read-only view of placed tiles)",

            "This list shows the raw data for each tile placed on the map. It is automatically managed by the editor tools.")]
        [ListDrawerSettings(IsReadOnly = true, NumberOfItemsPerPage = 5)]
        [SerializeField]
        private List<TileData> tileDataList = new List<TileData>();

        // 运行时缓存
        private Dictionary<Vector3Int, GameObject> instantiatedTiles = new Dictionary<Vector3Int, GameObject>();
        private Dictionary<Vector3Int, TileData> gridLookup = new Dictionary<Vector3Int, TileData>();

        #region 属性
        public Vector3Int GridSize
        {

            get => gridSize;

            set => gridSize = value;
        }


        public float CellSize
        {

            get => cellSize;

            set => cellSize = Mathf.Max(0.1f, value);
        }


        public Vector3 GridOffset
        {

            get => gridOffset;

            set => gridOffset = value;
        }


        public List<TileData> TileDataList => tileDataList;


        public TilePalette CurrentPalette
        {

            get => currentPalette;

            set => currentPalette = value;
        }


        public bool ShowGrid
        {

            get => showGrid;

            set => showGrid = value;
        }


        public Color GridColor
        {

            get => gridColor;

            set => gridColor = value;

        }
        #endregion

        #region Unity生命周期
        private void Awake()
        {
            RebuildGridLookup();
        }

        private void Start()
        {
            LoadMap();
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(0.1f, cellSize);
            gridSize = new Vector3Int(
                Mathf.Max(1, gridSize.x),
                Mathf.Max(1, gridSize.y),
                Mathf.Max(1, gridSize.z)
            );
        }
        #endregion

        #region 坐标转换
        /// <summary>
        /// 世界坐标转网格坐标
        /// </summary>
        public Vector3Int WorldToGrid(Vector3 worldPosition)
        {
            Vector3 localPos = worldPosition - gridOffset;
            return new Vector3Int(
                Mathf.FloorToInt(localPos.x / cellSize),
                Mathf.FloorToInt(localPos.y / cellSize),
                Mathf.FloorToInt(localPos.z / cellSize)
            );
        }

        /// <summary>
        /// 网格坐标转世界坐标（返回网格单元格的中心点）
        /// </summary>
        public Vector3 GridToWorld(Vector3Int gridPosition)
        {
            return new Vector3(
                (gridPosition.x + 0.5f) * cellSize,
                (gridPosition.y + 0.5f) * cellSize,
                (gridPosition.z + 0.5f) * cellSize
            ) + gridOffset;
        }

        /// <summary>
        /// 检查网格坐标是否在范围内
        /// </summary>
        public bool IsValidGridPosition(Vector3Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < gridSize.x &&
                   gridPos.y >= 0 && gridPos.y < gridSize.y &&
                   gridPos.z >= 0 && gridPos.z < gridSize.z;
        }
        #endregion

        #region 瓦片操作
        /// <summary>
        /// 在指定位置放置瓦片
        /// </summary>
        public bool PlaceTile(Vector3Int gridPos, GameObject prefab, Quaternion rotation, int paletteIndex = -1)
        {
            if (!IsValidGridPosition(gridPos) || prefab == null)
                return false;

            return PlaceTileInternal(gridPos, prefab, rotation, paletteIndex);
        }


        /// <summary>
        /// 在指定位置放置瓦片（用于智能堆叠，不限制Y轴范围）
        /// </summary>
        public bool PlaceTileForStacking(Vector3Int gridPos, GameObject prefab, Quaternion rotation, int paletteIndex = -1)
        {
            if (!IsValidGridPositionForStacking(gridPos) || prefab == null)
                return false;

            return PlaceTileInternal(gridPos, prefab, rotation, paletteIndex);
        }


        /// <summary>
        /// 检查网格位置是否适合智能堆叠（不限制Y轴范围）
        /// </summary>
        private bool IsValidGridPositionForStacking(Vector3Int gridPos)
        {
            // 智能堆叠只检查X和Z轴是否在范围内，Y轴不做限制
            return gridPos.x >= 0 && gridPos.x < gridSize.x &&
                   gridPos.z >= 0 && gridPos.z < gridSize.z;
        }


        /// <summary>
        /// 内部的瓦片放置实现
        /// </summary>
        private bool PlaceTileInternal(Vector3Int gridPos, GameObject prefab, Quaternion rotation, int paletteIndex)
        {
            // 移除已存在的瓦片
            RemoveTile(gridPos);

            // 创建新的瓦片数据
            string prefabGUID = GetPrefabGUID(prefab);
            TileData tileData = new TileData(gridPos, prefabGUID, rotation, paletteIndex);

            // 添加到数据列表

            tileDataList.Add(tileData);
            gridLookup[gridPos] = tileData;

            // 实例化预制体
            Vector3 worldPos = GridToWorld(gridPos);
            GameObject instance = Instantiate(prefab, worldPos, rotation, transform);
            instance.name = $"Tile_{gridPos.x}_{gridPos.y}_{gridPos.z}";

            // 添加TileInfoComponent以便反向查找

            var tileInfo = instance.AddComponent<TileInfoComponent>();
            tileInfo.GridPosition = gridPos;
            tileInfo.ParentGridMap = this;

            instantiatedTiles[gridPos] = instance;

            // 注册到撤销系统
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Tile");
#endif


            return true;
        }

        /// <summary>
        /// 移除指定位置的瓦片
        /// </summary>
        public bool RemoveTile(Vector3Int gridPos)
        {
            if (!gridLookup.ContainsKey(gridPos))
                return false;

            // 移除实例化的对象
            if (instantiatedTiles.ContainsKey(gridPos))
            {
                if (instantiatedTiles[gridPos] != null)
                {
#if UNITY_EDITOR
                    // 注册到撤销系统，这样撤销时会恢复对象

                    UnityEditor.Undo.DestroyObjectImmediate(instantiatedTiles[gridPos]);
#else
                    if (Application.isPlaying)
                        Destroy(instantiatedTiles[gridPos]);
                    else
                        DestroyImmediate(instantiatedTiles[gridPos]);
#endif
                }
                instantiatedTiles.Remove(gridPos);
            }

            // 移除数据
            TileData tileData = gridLookup[gridPos];
            tileDataList.Remove(tileData);
            gridLookup.Remove(gridPos);

            return true;
        }

        /// <summary>
        /// 获取指定位置的瓦片数据
        /// </summary>
        public TileData GetTileData(Vector3Int gridPos)
        {
            gridLookup.TryGetValue(gridPos, out TileData tileData);
            return tileData;
        }

        /// <summary>
        /// 检查指定位置是否有瓦片
        /// </summary>
        public bool HasTile(Vector3Int gridPos)
        {
            return gridLookup.ContainsKey(gridPos);
        }

        /// <summary>
        /// 清空所有瓦片
        /// </summary>
        [Button(ButtonSizes.Large), GUIColor(1, 0.6f, 0.6f)]
        public void ClearAllTiles()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorUtility.DisplayDialog("Clear All Tiles",
                "Are you sure you want to clear all tiles from this map?",

                "Yes", "No"))
            {
                return;
            }
#endif

            // 销毁所有实例化的对象

            foreach (var kvp in instantiatedTiles)
            {
                if (kvp.Value != null)
                {
#if UNITY_EDITOR
                    // 注册到撤销系统，这样撤销时会恢复对象

                    UnityEditor.Undo.DestroyObjectImmediate(kvp.Value);
#else
                    if (Application.isPlaying)
                        Destroy(kvp.Value);
                    else
                        DestroyImmediate(kvp.Value);
#endif
                }
            }

            // 清空数据
            tileDataList.Clear();
            instantiatedTiles.Clear();
            gridLookup.Clear();
        }
        #endregion

        #region 地图加载与保存
        /// <summary>
        /// 加载地图（根据TileData列表重新生成地图）
        /// </summary>
        [Button(ButtonSizes.Large), ButtonGroup("Map Actions")]
        public void LoadMap()
        {
            // 清空当前实例
            ClearInstantiatedTiles();

            // 重建查找表
            RebuildGridLookup();

            // 根据数据重新实例化瓦片
            foreach (TileData tileData in tileDataList)
            {
                if (tileData.IsValid())
                {
                    GameObject prefab = GetPrefabFromGUID(tileData.PrefabGUID, tileData.PaletteIndex);
                    if (prefab != null)
                    {
                        Vector3 worldPos = GridToWorld(tileData.GridPosition);
                        GameObject instance = Instantiate(prefab, worldPos, tileData.Rotation, transform);
                        instance.name = $"Tile_{tileData.GridPosition.x}_{tileData.GridPosition.y}_{tileData.GridPosition.z}";

                        // 添加TileInfoComponent以便反向查找

                        var tileInfo = instance.AddComponent<TileInfoComponent>();
                        tileInfo.GridPosition = tileData.GridPosition;
                        tileInfo.ParentGridMap = this;

                        instantiatedTiles[tileData.GridPosition] = instance;
                    }
                }
            }
        }

        /// <summary>
        /// 保存地图（数据已经在放置时实时更新）
        /// </summary>
        [Button(ButtonSizes.Large), ButtonGroup("Map Actions")]
        public void SaveMap()
        {
            // 验证数据完整性
            tileDataList.RemoveAll(data => !data.IsValid());
            RebuildGridLookup();
        }

        /// <summary>
        /// 导出地图为JSON文件
        /// </summary>
        [Button("Export to JSON", ButtonSizes.Large), ButtonGroup("JSON Actions"), GUIColor(0.4f, 0.8f, 1f)]
        public void ExportMapToJSON()
        {
#if UNITY_EDITOR
            // 获取保存路径
            string defaultName = gameObject.name + "_map";
            string path = UnityEditor.EditorUtility.SaveFilePanel(
                "Export Map to JSON",

                Application.dataPath,
                defaultName,

                "json");

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                // 创建地图数据
                string paletteName = currentPalette != null ? currentPalette.PaletteName : "";
                string paletteGUID = currentPalette != null ? GetAssetGUID(currentPalette) : "";


                MapData mapData = new MapData(
                    gameObject.name,
                    gridSize,
                    cellSize,
                    gridOffset,
                    paletteName,
                    paletteGUID,
                    tileDataList.ToArray()
                );

                // 序列化为JSON
                string json = JsonUtility.ToJson(mapData, true);

                // 写入文件

                System.IO.File.WriteAllText(path, json);


                UnityEngine.Debug.Log($"地图已导出到: {path}");
                UnityEditor.EditorUtility.DisplayDialog("导出成功", $"地图已成功导出到:\n{path}", "确定");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"导出地图失败: {e.Message}");
                UnityEditor.EditorUtility.DisplayDialog("导出失败", $"导出地图时发生错误:\n{e.Message}", "确定");
            }
#endif
        }

        /// <summary>
        /// 从JSON文件导入地图
        /// </summary>
        [Button("Import from JSON", ButtonSizes.Large), ButtonGroup("JSON Actions"), GUIColor(1f, 0.8f, 0.4f)]
        public void ImportMapFromJSON()
        {
#if UNITY_EDITOR
            // 获取文件路径
            string path = UnityEditor.EditorUtility.OpenFilePanel(
                "Import Map from JSON",

                Application.dataPath,

                "json");

            if (string.IsNullOrEmpty(path))
                return;

            // 确认导入操作
            if (!UnityEditor.EditorUtility.DisplayDialog("导入地图",
                "导入地图将清空当前所有瓦片数据，此操作不可撤销。\n确定要继续吗？",

                "确定", "取消"))
                return;

            try
            {
                // 记录撤销操作
                UnityEditor.Undo.RecordObject(this, "Import Map from JSON");

                // 读取JSON文件
                string json = System.IO.File.ReadAllText(path);
                MapData mapData = JsonUtility.FromJson<MapData>(json);

                if (mapData == null)
                {
                    throw new System.Exception("无法解析JSON文件，文件格式可能不正确");
                }

                // 清空当前地图
                ClearAllTiles();

                // 应用地图设置
                gridSize = mapData.gridSize;
                cellSize = mapData.cellSize;
                gridOffset = mapData.gridOffset;

                // 尝试找到对应的调色板
                if (!string.IsNullOrEmpty(mapData.paletteGUID))
                {
                    string palettePath = UnityEditor.AssetDatabase.GUIDToAssetPath(mapData.paletteGUID);
                    if (!string.IsNullOrEmpty(palettePath))
                    {
                        TilePalette palette = UnityEditor.AssetDatabase.LoadAssetAtPath<TilePalette>(palettePath);
                        if (palette != null)
                        {
                            currentPalette = palette;
                        }
                    }
                }

                // 导入瓦片数据
                if (mapData.tiles != null)
                {
                    foreach (TileData tileData in mapData.tiles)
                    {
                        if (tileData.IsValid())
                        {
                            tileDataList.Add(tileData);
                        }
                    }
                }

                // 重建查找表并加载地图
                RebuildGridLookup();
                LoadMap();

                UnityEngine.Debug.Log($"地图已从 {path} 导入成功");
                UnityEditor.EditorUtility.DisplayDialog("导入成功",

                    $"地图 '{mapData.mapName}' 已成功导入!\n" +
                    $"瓦片数量: {mapData.tiles?.Length ?? 0}\n" +
                    $"创建时间: {mapData.creationTime}", "确定");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"导入地图失败: {e.Message}");
                UnityEditor.EditorUtility.DisplayDialog("导入失败", $"导入地图时发生错误:\n{e.Message}", "确定");
            }
#endif
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 重建网格查找表
        /// </summary>
        private void RebuildGridLookup()
        {
            gridLookup.Clear();
            foreach (TileData tileData in tileDataList)
            {
                if (tileData.IsValid())
                {
                    gridLookup[tileData.GridPosition] = tileData;
                }
            }
        }

        /// <summary>
        /// 同步实例化对象与序列化数据（用于撤销后重建）
        /// </summary>
        public void SyncInstantiatedTiles()
        {
            // 清空当前实例
            ClearInstantiatedTiles();

            // 重建查找表
            RebuildGridLookup();

            // 根据序列化数据重新实例化瓦片
            foreach (TileData tileData in tileDataList)
            {
                if (tileData.IsValid())
                {
                    GameObject prefab = GetPrefabFromGUID(tileData.PrefabGUID, tileData.PaletteIndex);
                    if (prefab != null)
                    {
                        Vector3 worldPos = GridToWorld(tileData.GridPosition);
                        GameObject instance = Instantiate(prefab, worldPos, tileData.Rotation, transform);
                        instance.name = $"Tile_{tileData.GridPosition.x}_{tileData.GridPosition.y}_{tileData.GridPosition.z}";

                        // 添加TileInfoComponent以便反向查找

                        var tileInfo = instance.AddComponent<TileInfoComponent>();
                        tileInfo.GridPosition = tileData.GridPosition;
                        tileInfo.ParentGridMap = this;

                        instantiatedTiles[tileData.GridPosition] = instance;

                        // 注册到撤销系统，这样撤销时会自动销毁这些对象
#if UNITY_EDITOR
                        UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Sync Tiles");
#endif
                    }
                }
            }
        }

        /// <summary>
        /// 清空实例化的瓦片（不清空数据）
        /// </summary>
        private void ClearInstantiatedTiles()
        {
            foreach (var kvp in instantiatedTiles)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying)
                        Destroy(kvp.Value);
                    else
                        DestroyImmediate(kvp.Value);
                }
            }
            instantiatedTiles.Clear();
        }

        /// <summary>
        /// 获取预制体的GUID
        /// </summary>
        private string GetPrefabGUID(GameObject prefab)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(prefab));
#else
            return prefab.name; // 运行时回退方案
#endif
        }

        /// <summary>
        /// 获取任意资源的GUID
        /// </summary>
        private string GetAssetGUID(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(asset));
#else
            return asset.name; // 运行时回退方案
#endif
        }

        /// <summary>
        /// 通过GUID获取预制体
        /// </summary>
        private GameObject GetPrefabFromGUID(string guid, int paletteIndex)
        {
#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null) return prefab;
#endif

            // 如果GUID方式失败，尝试从当前瓦片库获取
            if (currentPalette != null && paletteIndex >= 0)
            {
                return currentPalette.GetPrefab(paletteIndex);
            }

            return null;
        }

        [Button, PropertyOrder(-1)]
        private void OpenTilemapEditor()
        {
#if UNITY_EDITOR
            // 使用反射来避免直接引用Editor命名空间
            var editorWindowType = System.Type.GetType("TilemapEditor.Editor.TilemapEditorWindow, Assembly-CSharp-Editor-firstpass");
            if (editorWindowType == null)
            {
                // 尝试从当前程序集查找
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    editorWindowType = assembly.GetType("TilemapEditor.Editor.TilemapEditorWindow");
                    if (editorWindowType != null) break;
                }
            }


            if (editorWindowType != null)
            {
                // 调用ShowWindow方法
                var showWindowMethod = editorWindowType.GetMethod("ShowWindow",

                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (showWindowMethod != null)
                {
                    var window = showWindowMethod.Invoke(null, null);

                    // 设置selectedPalette字段

                    var paletteField = editorWindowType.GetField("selectedPalette",

                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (paletteField != null && window != null)
                    {
                        paletteField.SetValue(window, currentPalette);
                    }

                    // 设置currentGridMap字段

                    var gridMapField = editorWindowType.GetField("currentGridMap",

                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (gridMapField != null && window != null)
                    {
                        gridMapField.SetValue(window, this);
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Could not find TilemapEditorWindow. Make sure the editor scripts are properly compiled.");
            }
#endif
        }

        /// <summary>
        /// 更新所有瓦片实例的可见性以实现层级隔离
        /// </summary>
        public void UpdateLayerVisibility(int currentLayer, bool isIsolationActive)
        {
            if (instantiatedTiles == null) return;

            foreach (var kvp in instantiatedTiles)
            {
                GameObject tileObject = kvp.Value;
                if (tileObject == null) continue;

                if (isIsolationActive)
                {
                    // 如果激活了层级隔离，只显示当前层级的瓦片
                    bool shouldBeVisible = kvp.Key.y == currentLayer;
                    if (tileObject.activeSelf != shouldBeVisible)
                    {
                        tileObject.SetActive(shouldBeVisible);
                    }
                }
                else
                {
                    // 如果关闭了层级隔离，确保所有瓦片都可见
                    if (!tileObject.activeSelf)
                    {
                        tileObject.SetActive(true);
                    }
                }
            }
        }
        #endregion

        #region 调试绘制
        private void OnDrawGizmosSelected()
        {
            if (!showGrid) return;

            Gizmos.color = gridColor;
            Vector3 center = transform.position + gridOffset + new Vector3(
                (gridSize.x) * cellSize * 0.5f,
                (gridSize.y) * cellSize * 0.5f,
                (gridSize.z) * cellSize * 0.5f
            );


            Vector3 size = new Vector3(
                (gridSize.x) * cellSize,
                (gridSize.y) * cellSize,
                (gridSize.z) * cellSize
            );


            Gizmos.DrawWireCube(center, size);
        }
        #endregion
    }
}