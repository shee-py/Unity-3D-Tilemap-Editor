using UnityEngine;

namespace TilemapEditor.Runtime
{
    /// <summary>
    /// 附加到实例化的瓦片GameObject上，用于存储其在网格中的关键信息。
    /// </summary>
    public class TileInfoComponent : MonoBehaviour
    {
        public Vector3Int GridPosition;
        public GridMap ParentGridMap;
    }
}