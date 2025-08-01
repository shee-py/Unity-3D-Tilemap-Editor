using UnityEngine;
using UnityEditor;
using TilemapEditor.Runtime;

namespace TilemapEditor.Editor
{
    /// <summary>
    /// 场景交互处理器，负责处理场景视图中的绘制和交互逻辑
    /// </summary>
    public static class SceneInteractionHandler
    {
        private static Vector3Int? hoveredGridPosition = null;
        private static Vector3Int? lastPaintedPosition = null;
        private static bool isDragging = false;

        /// <summary>
        /// 处理场景视图交互
        /// </summary>
        public static void HandleSceneInteraction(TilemapEditorWindow editorWindow)
        {
            if (editorWindow.CurrentGridMap == null) return;

            Event e = Event.current;
            GridMap gridMap = editorWindow.CurrentGridMap;

            // 获取鼠标在场景中的射线
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // 计算与网格平面的交点
            Vector3Int? gridPos = GetGridPositionFromRay(mouseRay, gridMap);

            // 更新悬停位置
            hoveredGridPosition = gridPos;

            // 处理鼠标事件
            HandleMouseEvents(e, editorWindow, gridPos);

            // 绘制网格和预览
            DrawGrid(gridMap);
            DrawTilePreview(editorWindow, gridPos);

            // 绘制高级工具预览
            if (editorWindow.CurrentAdvancedTool != AdvancedTool.None)
            {
                AdvancedTools.DrawPreviewPositions(gridMap, Color.yellow);
            }

            // 标记场景视图需要重绘
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 处理鼠标事件
        /// </summary>
        private static void HandleMouseEvents(Event e, TilemapEditorWindow editorWindow, Vector3Int? gridPos)
        {
            if (!gridPos.HasValue) return;

            Vector3Int pos = gridPos.Value;
            GridMap gridMap = editorWindow.CurrentGridMap;

            // 处理高级工具
            if (editorWindow.CurrentAdvancedTool != AdvancedTool.None)
            {
                bool isMouseDown = (e.type == EventType.MouseDown && e.button == 0);
                bool isMouseUp = (e.type == EventType.MouseUp && e.button == 0);

                AdvancedTools.HandleAdvancedTool(editorWindow.CurrentAdvancedTool, editorWindow, pos, isMouseDown, isMouseUp);

                if (isMouseDown || isMouseUp)
                {
                    e.Use();
                }
                return;
            }

            // 拾取器模式处理
            if (editorWindow.IsPickerMode)
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PickTile(editorWindow, e);
                    e.Use();
                }
                return;
            }

            // 基础工具处理
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0) // 左键
                    {
                        isDragging = true;
                        lastPaintedPosition = null;
 
                         if (editorWindow.IsPaintMode && editorWindow.SelectedTilePrefab != null)
                        {
                            PlaceTile(editorWindow, pos);
                        }
                        else if (editorWindow.IsEraseMode)
                        {
                            EraseTile(gridMap, pos);
                        }
 
                         e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && isDragging) // 左键拖拽
                    {
                        // 避免在同一位置重复绘制
                        if (lastPaintedPosition == null || lastPaintedPosition != pos)
                        {
                            if (editorWindow.IsPaintMode && editorWindow.SelectedTilePrefab != null)
                            {
                                PlaceTile(editorWindow, pos);
                            }
                            else if (editorWindow.IsEraseMode)
                            {
                                EraseTile(gridMap, pos);
                            }

                            lastPaintedPosition = pos;
                        }

                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 0) // 左键释放
                    {
                        isDragging = false;
                        lastPaintedPosition = null;
                        e.Use();
                    }
                    break;
            }
        }

        /// <summary>
        /// 从射线获取网格位置 - 混合智能模式
        /// </summary>
        private static Vector3Int? GetGridPositionFromRay(Ray ray, GridMap gridMap)
        {
            // 获取编辑器窗口实例来获取当前Y层级和智能堆叠开关状态
            TilemapEditorWindow[] windows = Resources.FindObjectsOfTypeAll<TilemapEditorWindow>();
            TilemapEditorWindow editorWindow = windows.Length > 0 ? windows[0] : null;
            int currentYLevel = editorWindow?.CurrentYLevel ?? 0;
            bool isSmartStackingEnabled = editorWindow?.IsSmartStackingEnabled ?? false;

            // 如果开启了智能堆叠，则优先尝试
            if (isSmartStackingEnabled)
            {
                // 优先模式：智能堆叠 (Physics.Raycast)
                RaycastHit hit;
                LayerMask tileLayerMask = 1 << LayerMask.NameToLayer("Default"); // 可以自定义Layer

                if (Physics.Raycast(ray, out hit, Mathf.Infinity, tileLayerMask))
                {
                    // 击中了现有瓦片，尝试智能堆叠
                    Vector3Int? stackingResult = GetStackingGridPosition(hit, gridMap);
                    if (stackingResult.HasValue)
                    {
                        return stackingResult;
                    }
                    // 如果智能堆叠失败（比如侧面碰撞），回退到显式层级模式
                }
            }

            // 后备模式：显式层级 (指定Y轴高度的虚拟平面)
            return GetExplicitLayerGridPosition(ray, gridMap, currentYLevel);
        }

        /// <summary>
        /// 智能堆叠模式：根据碰撞点和法线计算相邻网格位置（仅支持上下堆叠）
        /// </summary>
        private static Vector3Int? GetStackingGridPosition(RaycastHit hit, GridMap gridMap)
        {
            Vector3 normal = hit.normal.normalized;

            // 根据法线将碰撞点稍微向内移动，以避免边界问题
            Vector3 pointInsideObject = hit.point - normal * 0.001f;

            // 获取被击中瓦片的网格坐标
            Vector3Int hitGridPos = gridMap.WorldToGrid(pointInsideObject);

            // 只处理上下方向的堆叠
            // 如果法线主要朝上，则在上方放置
            if (normal.y > 0.7f)
            {
                // 直接在击中瓦片的上方一层放置
                Vector3Int targetGridPos = new Vector3Int(hitGridPos.x, hitGridPos.y + 1, hitGridPos.z);

                // 智能堆叠不受Y轴网格范围限制，只检查X和Z轴
                if (IsValidGridPositionForStacking(targetGridPos, gridMap))
                    return targetGridPos;
            }
            // 如果法线主要朝下，则在下方放置
            else if (normal.y < -0.7f)
            {
                // 直接在击中瓦片的下方一层放置
                Vector3Int targetGridPos = new Vector3Int(hitGridPos.x, hitGridPos.y - 1, hitGridPos.z);

                // 智能堆叠不受Y轴网格范围限制，只检查X和Z轴
                if (IsValidGridPositionForStacking(targetGridPos, gridMap))
                    return targetGridPos;
            }

            // 如果不是上下表面（即侧面），则不进行智能堆叠
            // 回退到显式层级模式的逻辑
            return null;
        }

        /// <summary>
        /// 检查网格位置是否适合智能堆叠（不限制Y轴范围）
        /// </summary>
        private static bool IsValidGridPositionForStacking(Vector3Int gridPos, GridMap gridMap)
        {
            // 智能堆叠只检查X和Z轴是否在范围内，Y轴不做限制
            return gridPos.x >= 0 && gridPos.x < gridMap.GridSize.x &&
                   gridPos.z >= 0 && gridPos.z < gridMap.GridSize.z;
        }

        /// <summary>
        /// 显式层级模式：在指定Y轴高度的虚拟平面上计算位置
        /// </summary>
        private static Vector3Int? GetExplicitLayerGridPosition(Ray ray, GridMap gridMap, int yLevel)
        {
            // 创建指定Y轴高度的平面
            float targetY = gridMap.GridOffset.y + yLevel * gridMap.CellSize;
            Plane layerPlane = new Plane(Vector3.up, new Vector3(0, targetY, 0));

            if (layerPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                Vector3Int gridPos = gridMap.WorldToGrid(hitPoint);

                // 强制设置Y坐标为指定层级
                gridPos.y = yLevel;

                // 检查是否在有效范围内
                if (gridMap.IsValidGridPosition(gridPos))
                {
                    return gridPos;
                }
            }
 
             return null;
        }

        /// <summary>
        /// 拾取场景中的瓦片
        /// </summary>
        private static void PickTile(TilemapEditorWindow editorWindow, Event e)
        {
            // 基本空值检查
            if (editorWindow?.CurrentGridMap == null)
            {
                Debug.LogWarning("拾取器无法使用：未选择GridMap");
                return;
            }

            Ray mouseRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(mouseRay, out RaycastHit hit))
            {
                TileInfoComponent tileInfo = hit.collider.GetComponent<TileInfoComponent>();
                if (tileInfo != null && 
                    tileInfo.ParentGridMap != null && 
                    tileInfo.ParentGridMap == editorWindow.CurrentGridMap)
                {
                    TileData tileData = tileInfo.ParentGridMap.GetTileData(tileInfo.GridPosition);
                    if (tileData != null && tileData.IsValid())
                    {
                        // 检查瓦片库是否存在 - 这是Packages安装模式下的关键检查
                        if (tileInfo.ParentGridMap.CurrentPalette == null)
                        {
                            Debug.LogWarning("拾取器无法使用：当前GridMap没有设置瓦片库(TilePalette)。请在GridMap组件中设置Current Palette字段");
                            return;
                        }

                        // 在瓦片库中找到对应的索引
                        int paletteIndex = tileInfo.ParentGridMap.CurrentPalette.GetPrefabIndexByGUID(tileData.PrefabGUID);
                        if (paletteIndex != -1)
                        {
                            // 调用窗口方法来更新状态
                            editorWindow.SelectTile(paletteIndex, tileData.Rotation);
                        }
                        else
                        {
                            Debug.LogWarning($"拾取的瓦片未在当前瓦片库中找到。这可能是因为：\n" +
                                           $"1. 瓦片预制体不在当前TilePalette中\n" +
                                           $"2. 程序集安装位置差异导致的GUID解析问题\n" +
                                           $"瓦片GUID: {tileData.PrefabGUID}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 放置瓦片
        /// </summary>
        private static void PlaceTile(TilemapEditorWindow editorWindow, Vector3Int gridPos)
        {
            GridMap gridMap = editorWindow.CurrentGridMap;

            // 如果目标位置已经有瓦片，则不执行任何操作
            if (gridMap.HasTile(gridPos))
            {
                return;
            }

            GameObject prefab = editorWindow.SelectedTilePrefab;
            Quaternion rotation = editorWindow.CurrentRotation;
            int paletteIndex = editorWindow.SelectedTileIndex;

            // 开始撤销组，确保数据和GameObject同步撤销
            UnityEditor.Undo.IncrementCurrentGroup();
            int undoGroup = UnityEditor.Undo.GetCurrentGroup();
            UnityEditor.Undo.SetCurrentGroupName("Place Tile");

            // 记录GridMap数据的撤销操作
            UnityEditor.Undo.RecordObject(gridMap, "Place Tile Data");

            bool success = false;

            // 检测是否为智能堆叠模式
            if (IsInSmartStackingMode())
            {
                // 检查Y轴是否超出范围
                if (gridPos.y < 0 || gridPos.y >= gridMap.GridSize.y)
                {
                    // 超出范围，不放置，并发出警告
                    Debug.LogWarning($"Tilemap Editor: Cannot place tile at {gridPos}. Y-level is outside the grid bounds (0 to {gridMap.GridSize.y - 1}).");
                    return;
                }

                // 使用智能堆叠方法（不受Y轴限制）
                success = gridMap.PlaceTileForStacking(gridPos, prefab, rotation, paletteIndex);
            }
            else
            {
                // 使用常规方法（受Y轴限制）
                success = gridMap.PlaceTile(gridPos, prefab, rotation, paletteIndex);
            }

            if (success)
            {
                // 标记对象为已修改
                EditorUtility.SetDirty(gridMap);

                // 标记场景为已修改
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            // 结束撤销组
            UnityEditor.Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// 检测当前是否处于智能堆叠模式
        /// </summary>
        private static bool IsInSmartStackingMode()
        {
            // 获取编辑器窗口实例
            TilemapEditorWindow[] windows = Resources.FindObjectsOfTypeAll<TilemapEditorWindow>();
            TilemapEditorWindow editorWindow = windows.Length > 0 ? windows[0] : null;

            // 必须同时开启了总开关，才能进入智能堆叠模式
            if (editorWindow == null || !editorWindow.IsSmartStackingEnabled)
            {
                return false;
            }

            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit hit;
            LayerMask tileLayerMask = 1 << LayerMask.NameToLayer("Default");

            if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity, tileLayerMask))
            {
                Vector3 normal = hit.normal.normalized;
                // 只有上下表面才算智能堆叠模式
                return (normal.y > 0.7f || normal.y < -0.7f);
            }

            return false;
        }

        /// <summary>
        /// 擦除瓦片
        /// </summary>
        private static void EraseTile(GridMap gridMap, Vector3Int gridPos)
        {
            // 开始撤销组，确保数据和GameObject同步撤销
            UnityEditor.Undo.IncrementCurrentGroup();
            int undoGroup = UnityEditor.Undo.GetCurrentGroup();
            UnityEditor.Undo.SetCurrentGroupName("Erase Tile");

            // 记录GridMap数据的撤销操作
            UnityEditor.Undo.RecordObject(gridMap, "Erase Tile Data");

            if (gridMap.RemoveTile(gridPos))
            {
                // 标记对象为已修改
                EditorUtility.SetDirty(gridMap);

                // 标记场景为已修改
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            // 结束撤销组
            UnityEditor.Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// 绘制网格
        /// </summary>
        private static void DrawGrid(GridMap gridMap)
        {
            if (!gridMap.ShowGrid) return;

            Handles.color = gridMap.GridColor;
            Vector3 offset = gridMap.GridOffset;
            float cellSize = gridMap.CellSize;
            Vector3Int gridSize = gridMap.GridSize;

            // 绘制网格线 - 绘制网格单元格的边界线
            // X方向的线（垂直线）
            for (int x = 0; x <= gridSize.x; x++)
            {
                Vector3 start = new Vector3(x * cellSize, 0, 0) + offset;
                Vector3 end = new Vector3(x * cellSize, 0, gridSize.z * cellSize) + offset;
                Handles.DrawLine(start, end);
            }

            // Z方向的线（水平线）
            for (int z = 0; z <= gridSize.z; z++)
            {
                Vector3 start = new Vector3(0, 0, z * cellSize) + offset;
                Vector3 end = new Vector3(gridSize.x * cellSize, 0, z * cellSize) + offset;
                Handles.DrawLine(start, end);
            }

            // 绘制网格边界（与网格线重合，不会超出）
            Vector3 center = offset + new Vector3(
                gridSize.x * cellSize * 0.5f,
                0,
                gridSize.z * cellSize * 0.5f
            );

            Vector3 size = new Vector3(
                gridSize.x * cellSize,
                0.1f,
                gridSize.z * cellSize
            );

            Handles.color = gridMap.GridColor * 0.5f;
            Handles.DrawWireCube(center, size);
        }

        /// <summary>
        /// 绘制瓦片预览
        /// </summary>
        private static void DrawTilePreview(TilemapEditorWindow editorWindow, Vector3Int? gridPos)
        {
            if (!gridPos.HasValue) return;

            if (editorWindow.IsPickerMode)
            {
                DrawPickerPreview(gridPos.Value);
                return;
            }

            if (editorWindow.IsEraseMode)
            {
                DrawErasePreview(editorWindow.CurrentGridMap, gridPos.Value);
                return;
            }
            if (editorWindow.SelectedTilePrefab == null) return;

            GridMap gridMap = editorWindow.CurrentGridMap;
            Vector3 worldPos = gridMap.GridToWorld(gridPos.Value);
            GameObject prefab = editorWindow.SelectedTilePrefab;
            Quaternion rotation = editorWindow.CurrentRotation;

            // 获取预制体的边界
            Bounds bounds = GetPrefabBounds(prefab);

            // 判断当前是哪种模式并选择预览颜色
            Color previewColor = GetPreviewColorForMode(editorWindow, gridPos.Value);

            // 绘制预览框
            Handles.color = previewColor;
            Matrix4x4 matrix = Matrix4x4.TRS(worldPos, rotation, Vector3.one);

            using (new Handles.DrawingScope(matrix))
            {
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            // 绘制网格单元格高亮
            DrawGridCellHighlight(gridMap, gridPos.Value, previewColor);
        }

        /// <summary>
        /// 根据当前模式获取预览颜色
        /// </summary>
        private static Color GetPreviewColorForMode(TilemapEditorWindow editorWindow, Vector3Int gridPos)
        {
            // 如果总开关关闭，总是返回蓝色
            if (!editorWindow.IsSmartStackingEnabled)
            {
                return new Color(0, 0.5f, 1, 0.5f); // 显式层级模式 - 蓝色
            }

            // 检测是否处于智能堆叠模式
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit hit;
            LayerMask tileLayerMask = 1 << LayerMask.NameToLayer("Default");

            if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity, tileLayerMask))
            {
                // 击中了瓦片，检查是否可以进行智能堆叠（上下表面）
                Vector3 normal = hit.normal.normalized;

                // 只有上下表面才显示绿色（智能堆叠模式）
                if (normal.y > 0.7f || normal.y < -0.7f)
                {
                    // 智能堆叠模式 - 绿色
                    return new Color(0, 1, 0, 0.5f);
                }
                else
                {
                    // 侧面碰撞，回退到显式层级模式 - 蓝色
                    return new Color(0, 0.5f, 1, 0.5f);
                }
            }
            else
            {
                // 显式层级模式 - 蓝色
                return new Color(0, 0.5f, 1, 0.5f);
            }
        }

        /// <summary>
        /// 绘制擦除预览
        /// </summary>
        private static void DrawErasePreview(GridMap gridMap, Vector3Int gridPos)
        {
            // 检查该位置是否有瓦片
            if (gridMap.HasTile(gridPos))
            {
                DrawGridCellHighlight(gridMap, gridPos, Color.red);
            }
            else
            {
                DrawGridCellHighlight(gridMap, gridPos, new Color(1, 0, 0, 0.3f));
            }
        }

        /// <summary>
        /// 绘制拾取器预览
        /// </summary>
        private static void DrawPickerPreview(Vector3Int gridPos)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(mouseRay, out RaycastHit hit))
            {
                TileInfoComponent tileInfo = hit.collider.GetComponent<TileInfoComponent>();
                if (tileInfo != null)
                {
                    // 高亮显示选中的瓦片
                    DrawGridCellHighlight(tileInfo.ParentGridMap, tileInfo.GridPosition, Color.cyan);
                }
            }
        }
 
         /// <summary>
        /// 绘制网格单元格高亮
        /// </summary>
        private static void DrawGridCellHighlight(GridMap gridMap, Vector3Int gridPos, Color color)
        {
            Vector3 worldPos = gridMap.GridToWorld(gridPos);
            float cellSize = gridMap.CellSize;

            Handles.color = color;

            // 绘制底面高亮
            Vector3[] corners = new Vector3[4]
            {
                worldPos + new Vector3(-cellSize * 0.5f, 0, -cellSize * 0.5f),
                worldPos + new Vector3(cellSize * 0.5f, 0, -cellSize * 0.5f),
                worldPos + new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f),
                worldPos + new Vector3(-cellSize * 0.5f, 0, cellSize * 0.5f)
            };

            Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.2f), color);
        }

        /// <summary>
        /// 获取预制体的边界
        /// </summary>
        private static Bounds GetPrefabBounds(GameObject prefab)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            // 尝试从渲染器获取边界
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                // 转换为本地空间
                bounds.center -= prefab.transform.position;
            }
            else
            {
                // 如果没有渲染器，使用碰撞器
                Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
                if (colliders.Length > 0)
                {
                    bounds = colliders[0].bounds;
                    foreach (Collider collider in colliders)
                    {
                        bounds.Encapsulate(collider.bounds);
                    }

                    // 转换为本地空间
                    bounds.center -= prefab.transform.position;
                }
            }

            return bounds;
        }

        /// <summary>
        /// 重置交互状态
        /// </summary>
        public static void ResetInteractionState()
        {
            hoveredGridPosition = null;
            lastPaintedPosition = null;
            isDragging = false;
        }
    }
}