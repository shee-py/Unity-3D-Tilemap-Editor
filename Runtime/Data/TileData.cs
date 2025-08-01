using UnityEngine;
using Sirenix.OdinInspector;

namespace TilemapEditor.Runtime
{
    /// <summary>
    /// 存储单个瓦片的信息，包括网格坐标、预制体GUID和旋转
    /// </summary>
    [System.Serializable]
    public class TileData
    {
        [SerializeField, ReadOnly]
        private Vector3Int gridPosition;

        [SerializeField, ReadOnly]
        private string prefabGUID;

        [SerializeField, ReadOnly]
        private Quaternion rotation;

        [SerializeField, ReadOnly]
        private int paletteIndex;

        // 公共属性访问器
        public Vector3Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }

        public string PrefabGUID
        {
            get => prefabGUID;
            set => prefabGUID = value;
        }

        public Quaternion Rotation
        {
            get => rotation;
            set => rotation = value;
        }

        public int PaletteIndex
        {
            get => paletteIndex;
            set => paletteIndex = value;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public TileData()
        {
            gridPosition = Vector3Int.zero;
            prefabGUID = string.Empty;
            rotation = Quaternion.identity;
            paletteIndex = -1;
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public TileData(Vector3Int position, string guid, Quaternion rot, int index = -1)
        {
            gridPosition = position;
            prefabGUID = guid;
            rotation = rot;
            paletteIndex = index;
        }

        /// <summary>
        /// 检查瓦片数据是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(prefabGUID);
        }

        /// <summary>
        /// 获取世界坐标位置
        /// </summary>
        public Vector3 GetWorldPosition(float gridSize = 1f)
        {
            return new Vector3(gridPosition.x * gridSize, gridPosition.y * gridSize, gridPosition.z * gridSize);
        }
    }

    /// <summary>
    /// 地图数据包装类，用于JSON序列化整个地图
    /// </summary>
    [System.Serializable]
    public class MapData
    {
        [SerializeField]
        public string mapName;

        [SerializeField]
        public Vector3Int gridSize;

        [SerializeField]
        public float cellSize;

        [SerializeField]
        public Vector3 gridOffset;

        [SerializeField]
        public string paletteName;

        [SerializeField]
        public string paletteGUID;

        [SerializeField]
        public TileData[] tiles;

        [SerializeField]
        public string creationTime;

        [SerializeField]
        public string version;

        public MapData()
        {
            mapName = "Untitled Map";
            gridSize = Vector3Int.one;
            cellSize = 1f;
            gridOffset = Vector3.zero;
            paletteName = "";
            paletteGUID = "";
            tiles = new TileData[0];
            creationTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            version = "1.0";
        }

        public MapData(string name, Vector3Int size, float cell, Vector3 offset,
                      string pName, string pGUID, TileData[] tileArray)
        {
            mapName = name;
            gridSize = size;
            cellSize = cell;
            gridOffset = offset;
            paletteName = pName;
            paletteGUID = pGUID;
            tiles = tileArray;
            creationTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            version = "1.0";
        }
    }
}