# 更好的君王之剑（Better Sovereign Blade）

简体中文 | [English](README.md)

Better Sovereign Blade 增强了储君“君王之剑”的交互、视觉反馈与自定义能力，将原本偏被动的视觉效果变成响应迅速、可由玩家直接操控的战斗元素。

本 Mod 主要改进三个方面：

- 直接操控
- 清晰的目标反馈
- 完整的视觉自定义

## 概述

原版君王之剑的概念很有特色，但交互性有限。

本 Mod 将大剑扩展为可操控实体。它不再只是静态视觉效果，而是会动态响应玩家输入，并明确呈现当前的目标选择意图。

此外，本 Mod 还加入了灵活的颜色自定义系统，让你可以按喜好调整大剑本体和相关特效的外观。

## 功能

### 拖动大剑

你可以直接在战场上拖动君王之剑。

- 使用鼠标自由调整大剑位置
- 带来更直接的实体操控感
- 让大剑更像真正的武器，而不只是视觉效果

### 悬浮目标指示

拿起君王之剑卡牌时：

- 大剑会悬浮在敌人上方
- 自动朝向当前目标

这能在战斗中提供即时、直观的目标反馈。

### 拖剑选择目标并出牌

卡牌可用时：

- 拖动大剑本体，而不是拖动卡牌
- 将大剑移动到指定敌人处
- 松开鼠标后出牌

与默认操作相比，这种方式能带来更沉浸、更直接的目标选择体验。

### 自定义大剑颜色

你可以完整自定义大剑的视觉外观：

- 选择预设配色主题
- 手动调整各个组件的颜色
- 自定义大剑模型、斩击特效和能量特效

无论只是细微调整，还是设计完全独特的风格，都可以按自己的偏好配置。

## 为什么制作这个 Mod？

本 Mod 希望让君王之剑变得：

- 更具交互性
- 响应更直接
- 表现力更丰富

通过结合直接操控、清晰反馈和视觉自定义，大剑会真正成为战斗中的主动元素，而不再只是被动特效。

## 兼容性

- 专为储君的君王之剑设计
- 不修改其他卡牌
- 与大多数玩法类 Mod 兼容

游戏更新，或其他 Mod 同时修改相同 UI/VFX 方法时，仍可能出现兼容问题。

## 说明

- 只影响交互和视觉表现
- 不改变平衡或卡牌机制
- 当前 Mod 清单版本：**0.1.0**

## 安装

1. 从 [Nexus Mods 页面](https://www.nexusmods.com/slaythespire2/mods/48?tab=description)下载最新版本。
2. 如果不存在，请创建 `<Slay the Spire 2>/mods/BetterSovereignBlade/`。
3. 将 `BetterSovereignBlade.dll` 和 `BetterSovereignBlade.json` 放入该目录。
4. 启动游戏，在**设置 → Mod 设置**中确认本 Mod 已启用。

Windows 和 Linux 的本地 `mods` 文件夹位于游戏安装目录中。如需临时禁用全部 Mod，可使用游戏启动参数 `-nomods`。

## 配置

打开游戏的常规设置页面，Better Sovereign Blade 会添加以下选项：

- 整体颜色预设；
- 动态 RGB 与变化速度；
- 拖动时的剑身旋转和拖尾特效；
- 各组件颜色和逐通道 RGB 开关。

选择**默认（不更改）**预设可保留游戏原版配色。

## 从源码构建

要求：

- .NET SDK 9.0 或更高版本；
- 本地已安装《杀戮尖塔 2》，且 `data_sts2_windows_x86_64` 中存在 `sts2.dll` 和 `0Harmony.dll`。

设置游戏目录后构建：

```powershell
$env:STS2_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2'
dotnet build .\BetterSovereignBlade.sln -c Release
```

也可以只为本次构建传入路径：

```powershell
dotnet build .\BetterSovereignBlade.sln -c Release -p:Sts2Dir='D:\SteamLibrary\steamapps\common\Slay the Spire 2'
```

构建成功后，项目会把 DLL 和 Mod 清单自动复制到 `<Sts2Dir>/mods/BetterSovereignBlade/`。

## 链接

- [Nexus Mods](https://www.nexusmods.com/slaythespire2/mods/48?tab=description)
- [源代码与问题反馈](https://github.com/SparkUiX/BetterSovereignBlade)

## 许可证

源代码采用 [MIT License](LICENSE) 开源。

《杀戮尖塔 2》及其相关名称和素材的权利归各自权利人所有。本项目是非官方社区作品，与 Mega Crit 无隶属关系，也未获得其背书。
