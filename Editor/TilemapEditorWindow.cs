using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using TilemapEditor.Runtime;

namespace TilemapEditor.Editor
{
    /// <summary>
    /// 瓦片地图编辑器主窗口 - UI Toolkit版本
    /// </summary>
    public class TilemapEditorWindow : EditorWindow
    {
        #region 字段
        [SerializeField]
        private TilePalette selectedPalette;
        [SerializeField] private int selectedTileIndex = -1;
        [SerializeField]
        private GridMap currentGridMap;

        // UI状态
        private readonly int previewSize = 64;
        private readonly int buttonsPerRow = 4;

        // 工具状态
        [SerializeField] private bool isPaintMode = true;
        [SerializeField] private bool isEraseMode = false;
        [SerializeField] private bool isPickerMode = false; // 新增：拾取器模式
        [SerializeField] private Quaternion currentRotation = Quaternion.identity;
        [SerializeField] private AdvancedTool currentAdvancedTool = AdvancedTool.None;
        [SerializeField] private bool isSmartStackingEnabled = true;
 
         // 多层级控制
        [SerializeField] private int currentYLevel = 0;
        [SerializeField] private bool isLayerIsolationActive = false; // 新增：层级隔离

        // 预览缓存
        private Dictionary<GameObject, Texture2D> previewCache = new Dictionary<GameObject, Texture2D>();

        // UI Elements 引用
        private ObjectField paletteField;
        private IntegerField selectedTileIndexField;
        private ObjectField gridMapField;
        private Button paintModeBtn;
        private Button eraseModeBtn;
        private Button pickerModeBtn; // 新增
        private EnumField advancedToolField;
        private Toggle smartStackingToggle;
        private IntegerField currentLayerField;
        private Toggle layerIsolationToggle; // 新增
        private Label rotationDisplay;
        private Label tileCountLabel;
        private VisualElement tileGrid;
        private Label selectedTileInfo;
        private Button loadMapBtn;
        private Button saveMapBtn;
        private Button clearAllBtn;
        private Button createGridMapBtn;
        private Button decreaseLayerBtn;
        private Button increaseLayerBtn;
        private Button resetRotationBtn;
        private Button rotate90Btn;
        #endregion

        #region 属性
        public TilePalette SelectedPalette => selectedPalette;
        public GameObject SelectedTilePrefab => selectedPalette?.GetPrefab(selectedTileIndex);
        public int SelectedTileIndex => selectedTileIndex;
        public bool IsPaintMode => isPaintMode;
        public bool IsEraseMode => isEraseMode;
        public bool IsPickerMode => isPickerMode; // 新增
        public Quaternion CurrentRotation => currentRotation;
        public GridMap CurrentGridMap => currentGridMap;
        public AdvancedTool CurrentAdvancedTool => currentAdvancedTool;
        public int CurrentYLevel => currentYLevel;
        public bool IsSmartStackingEnabled => isSmartStackingEnabled;
        public bool IsLayerIsolationActive => isLayerIsolationActive; // 新增
        #endregion

        #region 菜单入口
        [MenuItem("Tools/Tilemap Editor/3D Tilemap Editor")]
        public static TilemapEditorWindow ShowWindow()
        {
            TilemapEditorWindow window = GetWindow<TilemapEditorWindow>("3D Tilemap Editor");
            window.minSize = new Vector2(300, 400);
            window.Show();
            return window;
        }
        #endregion

        #region Unity编辑器生命周期
        public void CreateGUI()
        {
            // 获取包的根路径
            string packagePath = GetPackageRootPath();
            
            // 加载UXML文件
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{packagePath}/Editor/TilemapEditorWindow.uxml");
            VisualElement root = visualTree.Instantiate();
            rootVisualElement.Add(root);

            // 加载USS样式
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{packagePath}/Editor/TilemapEditorWindow.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            // 获取UI元素引用
            SetupUIReferences();

            // 绑定事件
            BindUIEvents();

            // 初始化UI状态
            InitializeUI();
        }

        private void OnEnable()
        {
            // 订阅场景视图事件
            SceneView.duringSceneGui += OnSceneGUI;

            // 查找当前场景中的GridMap
            FindCurrentGridMap();
        }

        private void OnDisable()
        {
            // 取消订阅场景视图事件
            SceneView.duringSceneGui -= OnSceneGUI;

            // 清理预览缓存
            ClearPreviewCache();

            // 确保层级隔离被关闭
            if (isLayerIsolationActive)
            {
                isLayerIsolationActive = false;
                currentGridMap?.UpdateLayerVisibility(currentYLevel, false);
            }
        }
        #endregion

        #region UI设置
        private void SetupUIReferences()
        {
            // 获取所有UI元素的引用
            paletteField = rootVisualElement.Q<ObjectField>("palette-field");
            selectedTileIndexField = rootVisualElement.Q<IntegerField>("selected-tile-index");
            gridMapField = rootVisualElement.Q<ObjectField>("gridmap-field");
            paintModeBtn = rootVisualElement.Q<Button>("paint-mode-btn");
            eraseModeBtn = rootVisualElement.Q<Button>("erase-mode-btn");
            pickerModeBtn = rootVisualElement.Q<Button>("picker-mode-btn"); // 新增
            advancedToolField = rootVisualElement.Q<EnumField>("advanced-tool-field");
            smartStackingToggle = rootVisualElement.Q<Toggle>("smart-stacking-toggle");
            currentLayerField = rootVisualElement.Q<IntegerField>("current-layer-field");
            layerIsolationToggle = rootVisualElement.Q<Toggle>("layer-isolation-toggle"); // 新增
            rotationDisplay = rootVisualElement.Q<Label>("rotation-display");
            tileCountLabel = rootVisualElement.Q<Label>("tile-count-label");
            tileGrid = rootVisualElement.Q<VisualElement>("tile-palette-grid");
            selectedTileInfo = rootVisualElement.Q<Label>("selected-tile-info");
            loadMapBtn = rootVisualElement.Q<Button>("load-map-btn");
            saveMapBtn = rootVisualElement.Q<Button>("save-map-btn");
            clearAllBtn = rootVisualElement.Q<Button>("clear-all-btn");
            createGridMapBtn = rootVisualElement.Q<Button>("create-gridmap-btn");
            decreaseLayerBtn = rootVisualElement.Q<Button>("decrease-layer-btn");
            increaseLayerBtn = rootVisualElement.Q<Button>("increase-layer-btn");
            resetRotationBtn = rootVisualElement.Q<Button>("reset-rotation-btn");
            rotate90Btn = rootVisualElement.Q<Button>("rotate-90-btn");
        }

        private void BindUIEvents()
        {
            // 绑定对象字段事件
            paletteField.RegisterValueChangedCallback(OnPaletteChanged);
            gridMapField.RegisterValueChangedCallback(OnGridMapChanged);

            // 绑定工具模式按钮事件
            paintModeBtn.clicked += SelectPaintMode;
            eraseModeBtn.clicked += SelectEraseMode;
            pickerModeBtn.clicked += SelectPickerMode; // 新增
 
             // 绑定高级工具事件
            advancedToolField.RegisterValueChangedCallback(OnAdvancedToolChanged);

            // 绑定智能堆叠事件
            smartStackingToggle.RegisterValueChangedCallback(OnSmartStackingChanged);
 
             // 绑定层级控制事件
            currentLayerField.RegisterValueChangedCallback(OnCurrentLayerChanged);
            layerIsolationToggle.RegisterValueChangedCallback(OnLayerIsolationChanged); // 新增
            decreaseLayerBtn.clicked += DecreaseLayer;
            increaseLayerBtn.clicked += IncreaseLayer;

            // 绑定旋转控制事件
            resetRotationBtn.clicked += ResetRotation;
            rotate90Btn.clicked += Rotate90;

            // 绑定网格地图操作事件
            loadMapBtn.clicked += LoadMap;
            saveMapBtn.clicked += SaveMap;
            clearAllBtn.clicked += ClearAll;
            createGridMapBtn.clicked += CreateNewGridMap;
        }

        private void InitializeUI()
        {
            // 设置初始值
            paletteField.value = selectedPalette;
            selectedTileIndexField.value = selectedTileIndex;
            gridMapField.value = currentGridMap;
            advancedToolField.Init(currentAdvancedTool);
            smartStackingToggle.value = isSmartStackingEnabled;
            currentLayerField.value = currentYLevel;
            layerIsolationToggle.value = isLayerIsolationActive; // 新增
 
             // 更新UI状态
            UpdateToolModeButtons();
            UpdateRotationDisplay();
            UpdateTilePalette();
            UpdateGridMapButtons();
        }
        #endregion

        #region 事件处理
        private void OnPaletteChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            selectedPalette = evt.newValue as TilePalette;
            selectedTileIndex = -1;
            selectedTileIndexField.value = selectedTileIndex;
            ClearPreviewCache();
            UpdateTilePalette();
        }

        private void OnGridMapChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            currentGridMap = evt.newValue as GridMap;
            UpdateGridMapButtons();
            ClampYLevel();
        }

        private void SelectPaintMode()
        {
            isPaintMode = true;
            isEraseMode = false;
            isPickerMode = false;
            currentAdvancedTool = AdvancedTool.None;
            advancedToolField.value = currentAdvancedTool;
            AdvancedTools.ResetToolState();
            UpdateToolModeButtons();
        }

        private void SelectEraseMode()
        {
            isPaintMode = false;
            isEraseMode = true;
            isPickerMode = false;
            currentAdvancedTool = AdvancedTool.None;
            advancedToolField.value = currentAdvancedTool;
            AdvancedTools.ResetToolState();
            UpdateToolModeButtons();
        }

        private void SelectPickerMode()
        {
            isPaintMode = false;
            isEraseMode = false;
            isPickerMode = true;
            currentAdvancedTool = AdvancedTool.None;
            advancedToolField.value = currentAdvancedTool;
            AdvancedTools.ResetToolState();
            UpdateToolModeButtons();
        }

        private void OnAdvancedToolChanged(ChangeEvent<System.Enum> evt)
        {
            currentAdvancedTool = (AdvancedTool)evt.newValue;
            AdvancedTools.ResetToolState();
            if (currentAdvancedTool != AdvancedTool.None)
            {
                // 使用高级工具时，我们仍然处于“绘制”的意图下
                isPaintMode = true;
                isEraseMode = false;
                isPickerMode = false;
            }
            else
            {
                // 当没有选择高级工具时，回到标准的绘制模式
                isPaintMode = true;
                isEraseMode = false;
                isPickerMode = false;
            }
            UpdateToolModeButtons();
        }

        private void OnSmartStackingChanged(ChangeEvent<bool> evt)
        {
            isSmartStackingEnabled = evt.newValue;
        }

        private void OnCurrentLayerChanged(ChangeEvent<int> evt)
        {
            currentYLevel = evt.newValue;
            ClampYLevel();
            if (isLayerIsolationActive)
            {
                currentGridMap?.UpdateLayerVisibility(currentYLevel, true);
            }
        }

        private void OnLayerIsolationChanged(ChangeEvent<bool> evt)
        {
            isLayerIsolationActive = evt.newValue;
            currentGridMap?.UpdateLayerVisibility(currentYLevel, isLayerIsolationActive);
        }

        private void DecreaseLayer()
        {
            currentYLevel--;
            ClampYLevel();
            currentLayerField.value = currentYLevel;
            if (isLayerIsolationActive)
            {
                currentGridMap?.UpdateLayerVisibility(currentYLevel, true);
            }
        }

        private void IncreaseLayer()
        {
            currentYLevel++;
            ClampYLevel();
            currentLayerField.value = currentYLevel;
            if (isLayerIsolationActive)
            {
                currentGridMap?.UpdateLayerVisibility(currentYLevel, true);
            }
        }

        private void ResetRotation()
        {
            currentRotation = Quaternion.identity;
            UpdateRotationDisplay();
        }

        private void Rotate90()
        {
            currentRotation *= Quaternion.Euler(0, 90, 0);
            UpdateRotationDisplay();
        }

        private void LoadMap()
        {
            if (currentGridMap != null)
            {
                currentGridMap.LoadMap();
            }
        }

        private void SaveMap()
        {
            if (currentGridMap != null)
            {
                currentGridMap.SaveMap();
                EditorUtility.SetDirty(currentGridMap);
            }
        }

        private void ClearAll()
        {
            if (currentGridMap != null && EditorUtility.DisplayDialog("Clear All Tiles",
                "Are you sure you want to clear all tiles?",
                "Yes", "No"))
            {
                Undo.RecordObject(currentGridMap, "Clear All Tiles");
                currentGridMap.ClearAllTiles();
                EditorUtility.SetDirty(currentGridMap);
            }
        }

        private void CreateNewGridMap()
        {
            GameObject gridMapObj = new GameObject("Grid Map");
            currentGridMap = gridMapObj.AddComponent<GridMap>();

            // 设置默认值
            currentGridMap.CurrentPalette = selectedPalette;

            // 更新UI
            gridMapField.value = currentGridMap;

            // 选中新创建的对象
            Selection.activeGameObject = gridMapObj;

            // 标记场景为已修改
            EditorUtility.SetDirty(gridMapObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            UpdateGridMapButtons();
        }
        #endregion

        #region UI更新
        private void UpdateToolModeButtons()
        {
            // 更新按钮选中状态
            paintModeBtn.RemoveFromClassList("selected");
            eraseModeBtn.RemoveFromClassList("selected");
            pickerModeBtn.RemoveFromClassList("selected");
 
             if (isPaintMode)
            {
                paintModeBtn.AddToClassList("selected");
            }
            else if (isEraseMode)
            {
                eraseModeBtn.AddToClassList("selected");
            }
            else if (isPickerMode)
            {
                pickerModeBtn.AddToClassList("selected");
            }
        }

        private void UpdateRotationDisplay()
        {
            rotationDisplay.text = $"{currentRotation.eulerAngles.y:F0}°";
        }

        private void UpdateTilePalette()
        {
            // 清空现有的瓦片网格
            tileGrid.Clear();

            if (selectedPalette == null)
            {
                tileCountLabel.text = "瓦片数量: 0";
                selectedTileInfo.text = "未选择瓦片库";
                return;
            }

            tileCountLabel.text = $"瓦片数量: {selectedPalette.Count}";

            if (selectedPalette.Count == 0)
            {
                selectedTileInfo.text = "瓦片库为空";
                return;
            }

            // 创建瓦片按钮
            for (int i = 0; i < selectedPalette.Count; i++)
            {
                GameObject prefab = selectedPalette.GetPrefab(i);
                if (prefab == null) continue;

                CreateTileButton(prefab, i);
            }

            UpdateSelectedTileInfo();
        }

        private void CreateTileButton(GameObject prefab, int index)
        {
            var button = new Button();
            button.AddToClassList("tile-button");

            // 获取预览图
            Texture2D preview = GetPreviewTexture(prefab);
            if (preview != null)
            {
                button.style.backgroundImage = new StyleBackground(preview);
            }

            button.tooltip = prefab.name;

            // 设置选中状态
            if (selectedTileIndex == index)
            {
                button.AddToClassList("selected");
            }

            // 绑定点击事件
            button.clicked += () =>
            {
                selectedTileIndex = index;
                selectedTileIndexField.value = selectedTileIndex;
                SelectPaintMode(); // 选择瓦片后自动切换到绘制模式
                UpdateTilePalette(); // 重新更新以显示选中状态
            };

            tileGrid.Add(button);
        }

        private void UpdateSelectedTileInfo()
        {
            if (selectedTileIndex >= 0 && selectedTileIndex < selectedPalette.Count)
            {
                GameObject selectedPrefab = selectedPalette.GetPrefab(selectedTileIndex);
                if (selectedPrefab != null)
                {
                    selectedTileInfo.text = $"已选择: {selectedPrefab.name}";
                }
                else
                {
                    selectedTileInfo.text = "选中的预制体无效";
                }
            }
            else
            {
                selectedTileInfo.text = "未选择瓦片";
            }
        }

        private void UpdateGridMapButtons()
        {
            bool hasGridMap = currentGridMap != null;
            loadMapBtn.style.display = hasGridMap ? DisplayStyle.Flex : DisplayStyle.None;
            saveMapBtn.style.display = hasGridMap ? DisplayStyle.Flex : DisplayStyle.None;
            clearAllBtn.style.display = hasGridMap ? DisplayStyle.Flex : DisplayStyle.None;
            createGridMapBtn.style.display = hasGridMap ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ClampYLevel()
        {
            int maxYLevel = (currentGridMap != null && currentGridMap.GridSize.y > 0) ? currentGridMap.GridSize.y - 1 : 0;
            currentYLevel = Mathf.Clamp(currentYLevel, 0, maxYLevel);
            if (currentLayerField != null)
            {
                currentLayerField.value = currentYLevel;
            }
        }
        #endregion

        #region 场景视图交互
        /// <summary>
        /// 场景视图GUI处理
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (selectedPalette == null || currentGridMap == null) return;

            HandleKeyboardInput();

            // 使用场景交互处理器
            SceneInteractionHandler.HandleSceneInteraction(this);

            DrawSceneGUI();
        }

        /// <summary>
        /// 处理键盘输入事件
        /// </summary>
        private void HandleKeyboardInput()
        {
            Event e = Event.current;

            // 处理键盘输入
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.R:
                        currentRotation *= Quaternion.Euler(0, 90, 0);
                        UpdateRotationDisplay();
                        e.Use();
                        Repaint();
                        break;
                    case KeyCode.B: // 使用B键切换Paint和Erase
                        if (isPaintMode) SelectEraseMode();
                        else SelectPaintMode();
                        e.Use();
                        Repaint();
                        break;
                    case KeyCode.I: // 新增：拾取器快捷键
                        SelectPickerMode();
                        e.Use();
                        Repaint();
                        break;
                    case KeyCode.Escape:
                        // 重置工具状态
                        SceneInteractionHandler.ResetInteractionState();
                        e.Use();
                        break;
                }
            }
        }

        /// <summary>
        /// 绘制场景GUI覆盖层
        /// </summary>
        private void DrawSceneGUI()
        {
            Handles.BeginGUI();

            // 显示工具信息面板
            DrawToolInfoPanel();

            // 显示快捷键帮助
            DrawHelpPanel();

            Handles.EndGUI();
        }

        /// <summary>
        /// 绘制工具信息面板
        /// </summary>
        private void DrawToolInfoPanel()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 180)); // 增加宽度和高度

            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            GUILayout.Label("3D 瓦片地图编辑器", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 显示当前模式
            string currentMode = isPaintMode ? "绘制模式" : (isEraseMode ? "擦除模式" : (isPickerMode ? "拾取模式" : "高级工具"));
            GUILayout.Label($"当前模式: {currentMode}");

            // 显示高级工具类型
            if (currentAdvancedTool != AdvancedTool.None)
            {
                string toolName = currentAdvancedTool switch
                {
                    AdvancedTool.PaintBucket => "油漆桶",
                    AdvancedTool.RectangleFill => "矩形填充",
                    AdvancedTool.LineTool => "直线工具",
                    AdvancedTool.CircleTool => "圆形工具",
                    _ => "未知工具"
                };
                GUILayout.Label($"工具: {toolName}");
            }

            // 显示当前编辑模式提示
            if (isPaintMode)
            {
                GUILayout.Label("绿色预览: 智能堆叠模式", EditorStyles.miniLabel);
                GUILayout.Label("蓝色预览: 层级建造模式", EditorStyles.miniLabel);
            }

            // 显示选中的瓦片
            if (SelectedTilePrefab != null)
                GUILayout.Label($"选中瓦片: {SelectedTilePrefab.name}");
            else if (isPaintMode)
                GUILayout.Label("未选择瓦片", EditorStyles.miniLabel);

            // 显示旋转和层级信息
            GUILayout.Label($"旋转角度: {currentRotation.eulerAngles.y:F0}°");
            GUILayout.Label($"当前层级: {currentYLevel}", EditorStyles.boldLabel);

            // 显示网格信息
            if (currentGridMap != null)
            {
                GUILayout.Label($"网格大小: {currentGridMap.GridSize}");
                GUILayout.Label($"智能堆叠: {(isSmartStackingEnabled ? "开启" : "关闭")}");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 绘制帮助面板
        /// </summary>
        private void DrawHelpPanel()
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - 270, 250, 220));

            GUI.backgroundColor = new Color(0, 0, 0, 0.5f);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            GUILayout.Label("快捷键操作:", EditorStyles.boldLabel);
            GUILayout.Label("R - 旋转瓦片");
            GUILayout.Label("B - 切换绘制/擦除模式");
            GUILayout.Label("I - 切换到拾取模式");
            GUILayout.Label("左键 - 绘制/擦除/拾取");
            GUILayout.Label("拖拽 - 连续绘制/擦除");
            GUILayout.Label("ESC - 重置工具状态");
            GUILayout.Label("▲▼ - 调整层级高度");
            GUILayout.Space(5);
            GUILayout.Label("预览颜色说明:", EditorStyles.boldLabel);
            GUILayout.Label("绿色 - 智能堆叠模式", EditorStyles.miniLabel);
            GUILayout.Label("蓝色 - 层级建造模式", EditorStyles.miniLabel);
            GUILayout.Label("红色 - 擦除预览", EditorStyles.miniLabel);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取预制体的预览图
        /// </summary>
        private Texture2D GetPreviewTexture(GameObject prefab)
        {
            if (prefab == null) return null;

            if (previewCache.ContainsKey(prefab))
                return previewCache[prefab];

            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            if (preview == null)
            {
                // 如果预览还没准备好，使用默认图标
                preview = EditorGUIUtility.ObjectContent(prefab, typeof(GameObject)).image as Texture2D;
            }

            previewCache[prefab] = preview;
            return preview;
        }

        /// <summary>
        /// 清理预览缓存
        /// </summary>
        private void ClearPreviewCache()
        {
            previewCache.Clear();
        }

        /// <summary>
        /// 查找当前场景中的GridMap
        /// </summary>
        private void FindCurrentGridMap()
        {
            if (currentGridMap == null)
            {
                currentGridMap = FindObjectOfType<GridMap>();
                if (gridMapField != null)
                {
                    gridMapField.value = currentGridMap;
                    UpdateGridMapButtons();
                }
            }
        }

        /// <summary>
        /// 公开方法，用于从外部（如SceneInteractionHandler）选择一个瓦片
        /// </summary>
        public void SelectTile(int index, Quaternion rotation)
        {
            selectedTileIndex = index;
            selectedTileIndexField.value = index;
            currentRotation = rotation;
            SelectPaintMode(); // 拾取后自动切换到绘制模式
            UpdateRotationDisplay();
            UpdateTilePalette();
            UpdateSelectedTileInfo();
        }

        /// <summary>
        /// 获取包的根路径，支持Plugins和Packages两种位置
        /// </summary>
        private string GetPackageRootPath()
        {
            // 首先尝试从Unity Package Manager获取包路径
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Assets/Plugins/TilemapEditor");
            if (packageInfo != null)
            {
                return packageInfo.assetPath;
            }
            else
            {
                return "Packages/com.ethan.tilemap-editor-3d";
            }

            // 如果是从Plugins文件夹运行，直接返回Plugins路径
            return "Assets/Plugins/TilemapEditor";
        }
        #endregion
    }
}