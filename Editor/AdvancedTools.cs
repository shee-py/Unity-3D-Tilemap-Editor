using UnityEngine;
using System.Collections.Generic;
using TilemapEditor.Runtime;

namespace TilemapEditor.Editor
{
    /// <summary>
    /// 高级绘制工具
    /// </summary>
    public enum AdvancedTool
    {
        None,
        PaintBucket,    // 油漆桶工具
        RectangleFill,  // 矩形填充
        LineTool,       // 直线工具
        CircleTool      // 圆形工具
    }

    /// <summary>
    /// 高级工具处理器
    /// </summary>
    public static class AdvancedTools
    {
        private static Vector3Int? startPosition = null;
        private static bool isDrawing = false;
        private static List<Vector3Int> previewPositions = new List<Vector3Int>();

        /// <summary>
        /// 处理高级工具操作
        /// </summary>
        public static void HandleAdvancedTool(AdvancedTool tool, TilemapEditorWindow editorWindow, Vector3Int gridPos, bool isMouseDown, bool isMouseUp)
        {
            switch (tool)
            {
                case AdvancedTool.PaintBucket:
                    if (isMouseDown)
                        HandlePaintBucket(editorWindow, gridPos);
                    break;

                case AdvancedTool.RectangleFill:
                    HandleRectangleFill(editorWindow, gridPos, isMouseDown, isMouseUp);
                    break;

                case AdvancedTool.LineTool:
                    HandleLineTool(editorWindow, gridPos, isMouseDown, isMouseUp);
                    break;

                case AdvancedTool.CircleTool:
                    HandleCircleTool(editorWindow, gridPos, isMouseDown, isMouseUp);
                    break;
            }
        }

        /// <summary>
        /// 获取预览位置列表
        /// </summary>
        public static List<Vector3Int> GetPreviewPositions()
        {
            return previewPositions;
        }

        /// <summary>
        /// 重置工具状态
        /// </summary>
        public static void ResetToolState()
        {
            startPosition = null;
            isDrawing = false;
            previewPositions.Clear();
        }

        #region 油漆桶工具
        /// <summary>
        /// 处理油漆桶工具
        /// </summary>
        private static void HandlePaintBucket(TilemapEditorWindow editorWindow, Vector3Int startPos)
        {
            GridMap gridMap = editorWindow.CurrentGridMap;
            if (gridMap == null) return;

            // 获取起始位置的瓦片类型
            var startTileData = gridMap.GetTileData(startPos);
            string targetGUID = startTileData?.PrefabGUID ?? string.Empty;

            // 如果要填充的类型和当前画笔相同，则不执行
            if (editorWindow.SelectedTilePrefab != null)
            {
                string brushGUID = GetPrefabGUID(editorWindow.SelectedTilePrefab);
                if (targetGUID == brushGUID) return;
            }

            // 执行泛洪填充
            var fillPositions = FloodFill(gridMap, startPos, targetGUID);
            
            if (fillPositions.Count > 0)
            {
                // 开始撤销组
                UnityEditor.Undo.IncrementCurrentGroup();
                int undoGroup = UnityEditor.Undo.GetCurrentGroup();
                UnityEditor.Undo.SetCurrentGroupName("Paint Bucket Fill");

                // 记录GridMap数据的撤销操作
                UnityEditor.Undo.RecordObject(gridMap, "Paint Bucket Fill Data");
                
                // 应用填充
                foreach (Vector3Int pos in fillPositions)
                {
                    if (editorWindow.IsPaintMode && editorWindow.SelectedTilePrefab != null)
                    {
                        gridMap.PlaceTile(pos, editorWindow.SelectedTilePrefab, editorWindow.CurrentRotation, editorWindow.SelectedTileIndex);
                    }
                    else if (editorWindow.IsEraseMode)
                    {
                        gridMap.RemoveTile(pos);
                    }
                }

                UnityEditor.EditorUtility.SetDirty(gridMap);

                // 结束撤销组
                UnityEditor.Undo.CollapseUndoOperations(undoGroup);
            }
        }

        /// <summary>
        /// 泛洪填充算法
        /// </summary>
        private static HashSet<Vector3Int> FloodFill(GridMap gridMap, Vector3Int startPos, string targetGUID)
        {
            var result = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            var visited = new HashSet<Vector3Int>();

            queue.Enqueue(startPos);
            visited.Add(startPos);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                
                // 检查当前位置是否匹配目标类型
                var currentTileData = gridMap.GetTileData(current);
                string currentGUID = currentTileData?.PrefabGUID ?? string.Empty;
                
                if (currentGUID == targetGUID)
                {
                    result.Add(current);
                    
                    // 检查四个方向的邻居
                    Vector3Int[] neighbors = {
                        current + Vector3Int.right,
                        current + Vector3Int.left,
                        current + Vector3Int.forward,
                        current + Vector3Int.back
                    };
                    
                    foreach (Vector3Int neighbor in neighbors)
                    {
                        if (gridMap.IsValidGridPosition(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return result;
        }
        #endregion

        #region 矩形填充工具
        /// <summary>
        /// 处理矩形填充工具
        /// </summary>
        private static void HandleRectangleFill(TilemapEditorWindow editorWindow, Vector3Int currentPos, bool isMouseDown, bool isMouseUp)
        {
            if (isMouseDown)
            {
                startPosition = currentPos;
                isDrawing = true;
                previewPositions.Clear();
            }
            else if (isDrawing)
            {
                if (startPosition.HasValue)
                {
                    previewPositions = GetRectanglePositions(startPosition.Value, currentPos);
                    
                    if (isMouseUp)
                    {
                        // 开始撤销组
                        UnityEditor.Undo.IncrementCurrentGroup();
                        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
                        UnityEditor.Undo.SetCurrentGroupName("Rectangle Fill");

                        // 记录GridMap数据的撤销操作
                        UnityEditor.Undo.RecordObject(editorWindow.CurrentGridMap, "Rectangle Fill Data");
                        
                        // 应用矩形填充
                        ApplyPositions(editorWindow, previewPositions);
                        
                        // 结束撤销组
                        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
                        
                        ResetToolState();
                    }
                }
            }
        }

        /// <summary>
        /// 获取矩形区域内的所有位置
        /// </summary>
        private static List<Vector3Int> GetRectanglePositions(Vector3Int start, Vector3Int end)
        {
            var positions = new List<Vector3Int>();
            
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    positions.Add(new Vector3Int(x, start.y, z));
                }
            }
            
            return positions;
        }
        #endregion

        #region 直线工具
        /// <summary>
        /// 处理直线工具
        /// </summary>
        private static void HandleLineTool(TilemapEditorWindow editorWindow, Vector3Int currentPos, bool isMouseDown, bool isMouseUp)
        {
            if (isMouseDown)
            {
                startPosition = currentPos;
                isDrawing = true;
                previewPositions.Clear();
            }
            else if (isDrawing)
            {
                if (startPosition.HasValue)
                {
                    previewPositions = GetLinePositions(startPosition.Value, currentPos);
                    
                    if (isMouseUp)
                    {
                        // 开始撤销组
                        UnityEditor.Undo.IncrementCurrentGroup();
                        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
                        UnityEditor.Undo.SetCurrentGroupName("Line Tool");

                        // 记录GridMap数据的撤销操作
                        UnityEditor.Undo.RecordObject(editorWindow.CurrentGridMap, "Line Tool Data");
                        
                        // 应用直线
                        ApplyPositions(editorWindow, previewPositions);
                        
                        // 结束撤销组
                        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
                        
                        ResetToolState();
                    }
                }
            }
        }

        /// <summary>
        /// 获取直线上的所有位置（Bresenham算法的3D版本）
        /// </summary>
        private static List<Vector3Int> GetLinePositions(Vector3Int start, Vector3Int end)
        {
            var positions = new List<Vector3Int>();
            
            int dx = Mathf.Abs(end.x - start.x);
            int dz = Mathf.Abs(end.z - start.z);
            
            int x = start.x;
            int z = start.z;
            
            int stepX = start.x < end.x ? 1 : -1;
            int stepZ = start.z < end.z ? 1 : -1;
            
            if (dx > dz)
            {
                int err = dx / 2;
                while (x != end.x)
                {
                    positions.Add(new Vector3Int(x, start.y, z));
                    err -= dz;
                    if (err < 0)
                    {
                        z += stepZ;
                        err += dx;
                    }
                    x += stepX;
                }
            }
            else
            {
                int err = dz / 2;
                while (z != end.z)
                {
                    positions.Add(new Vector3Int(x, start.y, z));
                    err -= dx;
                    if (err < 0)
                    {
                        x += stepX;
                        err += dz;
                    }
                    z += stepZ;
                }
            }
            
            positions.Add(end);
            return positions;
        }
        #endregion

        #region 圆形工具
        /// <summary>
        /// 处理圆形工具
        /// </summary>
        private static void HandleCircleTool(TilemapEditorWindow editorWindow, Vector3Int currentPos, bool isMouseDown, bool isMouseUp)
        {
            if (isMouseDown)
            {
                startPosition = currentPos;
                isDrawing = true;
                previewPositions.Clear();
            }
            else if (isDrawing)
            {
                if (startPosition.HasValue)
                {
                    float radius = Vector3.Distance(
                        new Vector3(startPosition.Value.x, 0, startPosition.Value.z),
                        new Vector3(currentPos.x, 0, currentPos.z)
                    );
                    
                    previewPositions = GetCirclePositions(startPosition.Value, Mathf.RoundToInt(radius));
                    
                    if (isMouseUp)
                    {
                        // 开始撤销组
                        UnityEditor.Undo.IncrementCurrentGroup();
                        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
                        UnityEditor.Undo.SetCurrentGroupName("Circle Tool");

                        // 记录GridMap数据的撤销操作
                        UnityEditor.Undo.RecordObject(editorWindow.CurrentGridMap, "Circle Tool Data");
                        
                        // 应用圆形
                        ApplyPositions(editorWindow, previewPositions);
                        
                        // 结束撤销组
                        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
                        
                        ResetToolState();
                    }
                }
            }
        }

        /// <summary>
        /// 获取圆形上的所有位置
        /// </summary>
        private static List<Vector3Int> GetCirclePositions(Vector3Int center, int radius)
        {
            var positions = new List<Vector3Int>();
            var addedPositions = new HashSet<Vector3Int>();
            
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    float distance = Mathf.Sqrt(x * x + z * z);
                    if (distance <= radius + 0.5f && distance >= radius - 0.5f)
                    {
                        Vector3Int pos = new Vector3Int(center.x + x, center.y, center.z + z);
                        if (!addedPositions.Contains(pos))
                        {
                            positions.Add(pos);
                            addedPositions.Add(pos);
                        }
                    }
                }
            }
            
            return positions;
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 应用位置列表到网格地图
        /// </summary>
        private static void ApplyPositions(TilemapEditorWindow editorWindow, List<Vector3Int> positions)
        {
            GridMap gridMap = editorWindow.CurrentGridMap;
            if (gridMap == null) return;

            foreach (Vector3Int pos in positions)
            {
                if (!gridMap.IsValidGridPosition(pos)) continue;

                if (editorWindow.IsPaintMode && editorWindow.SelectedTilePrefab != null)
                {
                    gridMap.PlaceTile(pos, editorWindow.SelectedTilePrefab, editorWindow.CurrentRotation, editorWindow.SelectedTileIndex);
                }
                else if (editorWindow.IsEraseMode)
                {
                    gridMap.RemoveTile(pos);
                }
            }

            UnityEditor.EditorUtility.SetDirty(gridMap);
        }

        /// <summary>
        /// 获取预制体的GUID
        /// </summary>
        private static string GetPrefabGUID(GameObject prefab)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(prefab));
#else
            return prefab.name;
#endif
        }

        /// <summary>
        /// 绘制预览位置
        /// </summary>
        public static void DrawPreviewPositions(GridMap gridMap, Color color)
        {
            if (previewPositions.Count == 0) return;

            UnityEditor.Handles.color = color;
            
            foreach (Vector3Int gridPos in previewPositions)
            {
                if (gridMap.IsValidGridPosition(gridPos))
                {
                    Vector3 worldPos = gridMap.GridToWorld(gridPos);
                    float cellSize = gridMap.CellSize;
                    
                    Vector3[] corners = new Vector3[4]
                    {
                        worldPos + new Vector3(-cellSize * 0.5f, 0, -cellSize * 0.5f),
                        worldPos + new Vector3(cellSize * 0.5f, 0, -cellSize * 0.5f),
                        worldPos + new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f),
                        worldPos + new Vector3(-cellSize * 0.5f, 0, cellSize * 0.5f)
                    };
                    
                    UnityEditor.Handles.DrawSolidRectangleWithOutline(corners, 
                        new Color(color.r, color.g, color.b, 0.3f), color);
                }
            }
        }
        #endregion
    }
} 