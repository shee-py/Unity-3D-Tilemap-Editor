# 3D Tilemap Editor 包移植指南

## 概述

本指南说明如何将3D Tilemap Editor从Unity项目的Plugins文件夹迁移到Unity Package Manager的远程包。

## 核心问题

当插件从`Assets/Plugins/TilemapEditor`迁移到Unity Package Manager时，所有硬编码的路径引用都会失效，因为包的位置会从`Assets/Plugins/`变为`Packages/com.ethan.tilemap-editor-3d/`。

## 已解决的问题

### 1. 硬编码路径问题

**问题位置：** 
- `TilemapEditorWindow.cs:97` - UXML文件加载路径
- `TilemapEditorWindow.cs:102` - USS样式表加载路径  
- `TilemapEditorWindow.uxml:3` - 样式表引用路径

**解决方案：**
添加了动态路径解析方法`GetPackageRootPath()`，该方法：
1. 首先尝试通过Unity Package Manager API获取包路径
2. 如果失败（在Plugins环境），则回退到硬编码路径
3. 支持两种环境的无缝切换

### 2. UXML静态路径问题

**解决方案：**
- 移除UXML文件中的硬编码样式表引用
- 改为在C#代码中动态加载样式表
- 添加注释说明原因

## 使用方法

### 作为Unity Package使用

1. 在Unity项目中打开Package Manager
2. 点击 "+" -> "Add package from git URL"
3. 输入：`https://github.com/shee-py/Unity-3D-Tilemap-Editor.git`
4. 包将自动安装到`Packages/`文件夹

### 作为本地Plugins使用

插件仍然兼容传统的Plugins文件夹部署方式，无需任何额外配置。

## 技术细节

### GetPackageRootPath() 方法

```csharp
private string GetPackageRootPath()
{
    // 首先尝试从Unity Package Manager获取包路径
    var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Assets/Plugins/TilemapEditor");
    if (packageInfo != null)
    {
        return packageInfo.assetPath;
    }

    // 如果是从Plugins文件夹运行，直接返回Plugins路径
    return "Assets/Plugins/TilemapEditor";
}
```

### 兼容性保证

- ✅ Unity 2021.3及以上版本
- ✅ 支持Plugins文件夹部署
- ✅ 支持Package Manager远程部署
- ✅ 支持Package Manager本地部署
- ✅ 向后兼容现有项目

### 路径解析流程

1. 调用`GetPackageRootPath()`
2. 尝试通过PackageManager API查找包信息
3. 如果找到包信息，使用`packageInfo.assetPath`
4. 如果未找到，回退到`Assets/Plugins/TilemapEditor`
5. 拼接子路径加载资源

## 注意事项

1. **Assembly定义文件**：如果使用了Assembly Definition Files，确保引用正确
2. **依赖项**：确保所有依赖项在package.json中正确声明
3. **示例文件**：示例文件应放在`Samples~/`文件夹中
4. **版本控制**：使用语义化版本控制

## 故障排除

### 资源加载失败

如果遇到资源加载失败：
1. 检查包是否正确安装
2. 确认Unity版本兼容性
3. 查看Console中的具体错误信息
4. 尝试重新导入包

### 路径解析错误

如果路径解析有问题：
1. 确认`GetPackageRootPath()`方法正常工作
2. 检查PackageManager API是否可用
3. 验证包的安装位置

## 支持

如遇到问题，请在GitHub仓库创建Issue：
https://github.com/shee-py/Unity-3D-Tilemap-Editor/issues