# 更新日志

本文档记录了项目的所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
并且本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

### 计划新增
- 支持自定义笔刷形状
- 添加地形工具（高度图支持）
- 实现瓦片动画系统
- 支持多人协作编辑

## [1.0.1] - 2025-08-01

### 修复
- **[重大修复]** 解决远程包安装时的编译错误
- 将Data命名空间类移动到Runtime程序集中
- 修复Assembly Definition文件配置问题
- 更新所有命名空间引用以支持远程包安装

### 改进
- 添加智能资源加载系统，同时支持本地和远程包路径
- 优化包结构以符合Unity Package标准
- 添加Samples~/和Documentation~/目录
- 更新package.json配置，支持示例导入

### 技术变更
- 创建TilemapEditor.Runtime.asmdef和TilemapEditor.Editor.asmdef
- 将TileData和TilePalette类从TilemapEditor.Data移动到TilemapEditor.Runtime命名空间
- 修复过时的API使用（FindObjectOfType → FindFirstObjectByType）

## [1.0.0] - 2024-12-20

### 新增
- 完整的撤销/重做支持
- 直观的瓦片绘制系统
- 网格对齐系统
- 多层级建造功能
- 实时预览系统
- 旋转支持（4向旋转）
- 数据持久化到GridMap组件
- 地图导出/导入JSON功能
- 混合智能模式（智能堆叠 + 显式层级）
- 多种绘制工具：
  - 基础画笔
  - 擦除工具
  - 油漆桶（泛洪填充）
  - 矩形填充
  - 直线工具
  - 圆形工具
- 瓦片拾取器（Eyedropper Tool）
- 层级隔离功能
- 完整的编辑器窗口UI
- 快捷键支持
- 瓦片库管理系统

### 功能
- 智能堆叠模式：自动在瓦片上下方堆叠
- 显式层级模式：在指定Y轴高度建造
- 实时预览：不同模式显示不同颜色
- 完整的数据管理：加载、保存、清空、导出、导入
- 层级控制：调整编辑高度，支持层级隔离
- 旋转控制：支持瓦片旋转放置

### 技术特性
- 基于Unity EditorWindow的自定义编辑器
- ScriptableObject的瓦片库系统
- MonoBehaviour的网格地图组件
- 完整的撤销系统集成
- JSON序列化支持
- Unity UIElements界面

---

## 版本说明

### 版本号格式
本项目使用语义化版本号：`主版本号.次版本号.修订号`

- **主版本号**：不兼容的API修改
- **次版本号**：向下兼容的功能性新增
- **修订号**：向下兼容的问题修正

### 发布类型
- `[新增]` - 新功能
- `[变更]` - 现有功能的变更
- `[弃用]` - 即将移除的功能
- `[移除]` - 已移除的功能
- `[修复]` - 问题修复
- `[安全]` - 安全性修复