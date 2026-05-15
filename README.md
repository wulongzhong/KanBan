# KanBan

基于 **.NET 9** 与 **Avalonia 12** 的跨平台桌面应用脚手架，采用 **MVVM** 架构。

## 技术栈

| 组件 | 说明 |
|------|------|
| .NET 9 | 运行时与 SDK 目标框架 `net9.0` |
| Avalonia | 跨平台 UI 框架（Windows / macOS / Linux） |
| CommunityToolkit.Mvvm | MVVM 基类与源生成器（`ObservableObject` 等） |
| Fluent 主题 | Avalonia 内置 Fluent 风格 |

## 快速开始

```powershell
# 还原依赖并编译
dotnet build KanBan.sln

# 运行
dotnet run --project KanBan.csproj
```

要求：已安装 [.NET 9 SDK](https://dotnet.microsoft.com/download)。

## 目录结构

```
KanBan/
├── Assets/              # 静态资源（图标、图片等）
├── Models/              # 数据模型（占位，待业务实现）
├── ViewModels/          # 视图模型（MVVM 中的 VM）
├── Views/               # 视图（XAML 界面）
├── bin/                 # 编译输出（由构建生成，已 git 忽略）
├── obj/                 # 中间文件（由构建生成，已 git 忽略）
├── App.axaml            # 应用级 XAML（主题、全局样式、ViewLocator）
├── App.axaml.cs         # 应用逻辑（启动主窗口、注入 DataContext）
├── Program.cs           # 程序入口
├── ViewLocator.cs       # 视图模型 → 视图的自动映射
├── KanBan.csproj        # 项目定义与 NuGet 依赖
├── KanBan.sln           # Visual Studio / Rider 解决方案
├── app.manifest         # Windows 应用程序清单
└── .gitignore           # Git 忽略规则
```

### `Assets/`

存放嵌入到程序集中的静态资源。在 `KanBan.csproj` 中通过 `<AvaloniaResource Include="Assets\**" />` 打包，可在 XAML 中用路径引用，例如主窗口图标：

```xml
Icon="/Assets/avalonia-logo.ico"
```

常见用途：应用图标、图片、字体文件等。

### `Models/`

存放与 UI 无关的**数据模型**（实体、DTO、枚举等）。当前为空目录，仅在项目中占位，便于后续添加看板列、卡片等业务类型。

### `ViewModels/`

**视图模型**层，负责：

- 向视图暴露可绑定属性与命令
- 调用模型或服务等业务逻辑
- 不直接操作控件

| 文件 | 作用 |
|------|------|
| `ViewModelBase.cs` | 所有 ViewModel 的基类，继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，提供属性变更通知 |
| `MainWindowViewModel.cs` | 主窗口对应的 ViewModel，例如 `Greeting` 等绑定属性 |

命名约定：`FooViewModel` 对应视图 `Foo`（见 `ViewLocator`）。

### `Views/`

**视图**层，用 Avalonia XAML（`.axaml`）描述界面布局与绑定。

| 文件 | 作用 |
|------|------|
| `MainWindow.axaml` | 主窗口 UI 定义（控件树、`{Binding ...}`） |
| `MainWindow.axaml.cs` | 主窗口代码隐藏，通常只调用 `InitializeComponent()` |

`.axaml` 类似 WPF 的 XAML；`x:DataType` 指定编译期绑定类型，配合项目中的 `AvaloniaUseCompiledBindingsByDefault` 启用编译绑定，减少运行时错误。

### `bin/` 与 `obj/`

| 目录 | 含义 |
|------|------|
| `bin/` | 最终输出（如 `KanBan.dll`、可执行文件） |
| `obj/` | 编译中间产物（生成的代码、缓存） |

均由 `dotnet build` 生成，已在 `.gitignore` 中排除，无需提交到版本库。

## 特殊文件说明

### `Program.cs`

应用程序**入口**。配置 Avalonia 应用构建器并启动桌面生命周期：

- `BuildAvaloniaApp()`：平台检测、字体、Debug 下开发者工具等
- `StartWithClassicDesktopLifetime(args)`：以经典桌面模式运行（单主窗口应用）

### `App.axaml` / `App.axaml.cs`

应用级配置，相当于「整个程序的全局设置」。

**`App.axaml`**

- `FluentTheme`：全局 Fluent 主题
- `RequestedThemeVariant`：跟随系统 / 固定浅色或深色
- `Application.DataTemplates` 注册 `ViewLocator`，用于按 ViewModel 类型自动解析 View

**`App.axaml.cs`**

- `Initialize()`：加载应用 XAML
- `OnFrameworkInitializationCompleted()`：框架就绪后创建 `MainWindow` 并设置 `DataContext = new MainWindowViewModel()`

### `ViewLocator.cs`

实现 `IDataTemplate`，在需要显示某个 `ViewModelBase` 时，按命名规则查找对应 View：

- `KanBan.ViewModels.MainWindowViewModel` → `KanBan.Views.MainWindow`

通过反射创建视图实例。若找不到类型，会显示 `Not Found: ...` 占位文本。新增页面时保持 **ViewModel 与 View 成对命名** 即可自动关联。

### `KanBan.csproj`

.NET 项目文件，主要配置：

| 配置项 | 含义 |
|--------|------|
| `OutputType` = `WinExe` | 生成 Windows 可执行程序（其他平台仍为对应可执行格式） |
| `TargetFramework` = `net9.0` | 目标 .NET 9 |
| `ApplicationManifest` | 关联 `app.manifest`（Windows） |
| `AvaloniaUseCompiledBindingsByDefault` | 默认启用编译期绑定 |

NuGet 包：`Avalonia`、`Avalonia.Desktop`、`Avalonia.Themes.Fluent`、`Avalonia.Fonts.Inter`、`CommunityToolkit.Mvvm` 等。

### `KanBan.sln`

解决方案文件，将 `KanBan.csproj` 组织为一个可在 Visual Studio、Rider、VS Code（C# 扩展）中打开的解决方案。多项目时可在此继续 `dotnet sln add`。

### `app.manifest`

**仅 Windows** 使用的应用程序清单，声明兼容的 Windows 版本等。项目已引用；删除可能导致窗口透明度或嵌入控件异常。

### `.gitignore`

由 `dotnet new gitignore` 生成，忽略 `bin/`、`obj/`、`.vs/`、用户本地配置、NuGet 缓存等，避免将构建产物和 IDE 私有文件提交到 Git。

## MVVM 数据流（简要）

```
View (MainWindow.axaml)
    ↕ 数据绑定 {Binding Greeting}
ViewModel (MainWindowViewModel)
    ↕ 读写
Model（待添加于 Models/）
```

启动链：`Program.Main` → `App` 初始化 → 创建 `MainWindow` + `MainWindowViewModel` → XAML 绑定显示。

## 扩展建议

- 在 `Models/` 添加看板、列、任务等类型
- 在 `ViewModels/`、`Views/` 按功能拆分新页面，并保持 `XxxViewModel` / `Xxx` 命名配对
- 复杂列表与命令可使用 CommunityToolkit.Mvvm 的 `[ObservableProperty]`、`RelayCommand` 等源生成特性
