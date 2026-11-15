<div align="center">

# AstraFramework

**SCP: Secret Laboratory 服务端框架库**

基于 LabAPI · 自定义内容 · 托管补丁 · 声明式事件

![Version](https://img.shields.io/badge/version-3.7.0-2ea043)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.8.1-512bd4)
![Platform](https://img.shields.io/badge/platform-SCP%3A%20SL%20server-6f42c1)
![License](https://img.shields.io/badge/license-MIT-yellow)

</div>

AstraFramework 是运行在 SCP: Secret Laboratory 服务端的**框架库**，不是独立插件。它在 LabAPI 之上提供自定义角色、阵营、波次、物品、效果与技能，并统一管理 Harmony 补丁、事件方法和注册生命周期。

> 本仓库只包含抽象与机制；具体角色、物品、波次等实现应放在业务插件中。

## 特性

| 领域 | 提供的能力 |
|---|---|
| 角色 / 阵营 / 波次 | `CustomRole`、`TeamRef`、`CustomWave`；波次调度、权重波次、支援波池、原版波次适配 |
| 物品 | `CustomItem` / `CustomFirearm` 及装备、拾取、使用、换弹、瞄准、射击等扩展钩子 |
| 效果 / 技能 | `CustomEffect` 叠加与控制器、`CustomAbility` 及玩家技能管理、HumeShield 扩展 |
| 事件 | `[EventMethod]` 声明式订阅、增强事件处理器、事件生命周期管理 |
| 补丁 | `[PatchMethod]` / `[ManagedPatch]` 托管 Harmony 补丁，按版本与状态查询 |
| 注册生命周期 | `Astra.Lifecycle`：按程序集记录注册来源，插件禁用时协作式撤销 |
| 位置 / 刷怪点 | `PositionEntry`、`SpawnPointEntry`、`ISpawnOnRoundStart` |
| 分析器 | Roslyn 分析器校验事件、补丁与契约用法（AST1001–AST5005） |

## 环境要求

- Windows
- .NET Framework **4.8.1**
- SCP: Secret Laboratory 专用服务器与 LabAPI
- Python 3（构建脚本）
- Visual Studio Build Tools / 完整 .NET Framework MSBuild

游戏与 LabAPI 公开化程序集已随仓库放在 `libs/`，构建不依赖本机 Steam 安装目录。

## 构建

```bash
# 框架库
python tools/vsbuild.py src/Astra.Framework/Astra.Framework.csproj -p:Configuration=Release -p:Platform=x64

# Roslyn 分析器
python tools/vsbuild.py src/Astra.Analyzers/Astra.Analyzers.csproj -p:Configuration=Release
```

`net481` 工程必须使用完整的 .NET Framework MSBuild；`dotnet build` 对 COM 引用会报 `MSB4803`，因此统一走 `tools/vsbuild.py`。

## 产物

所有编译输出与中间文件统一收进 `artifacts/`：

| 类型 | 路径 |
|---|---|
| 框架主程序集 | `artifacts/build/Astra.Framework/<Platform>/<Configuration>/Astra.Framework.dll` |
| 分析器 | `artifacts/build/Astra.Analyzers/[<Platform>/]<Configuration>/netstandard2.0/Astra.Analyzers.dll` |
| 构建日志 | `artifacts/logs/vsbuild-<时间戳>.log` |

发布时从 `artifacts/build/...` 取 DLL；`artifacts/` 不提交。

## 项目结构

```text
src/Astra.Framework/     框架主程序集（按域分目录）
  Roles/ Teams/ Waves/  自定义角色、阵营、波次
  Items/ Effects/       自定义物品、效果
  Abilities/ HumeShield/技能与护盾扩展
  Events/ Patching/     事件与托管补丁
  Lifecycle/ Positions/ 注册生命周期、位置与刷怪点
  Commands/ Loader/     RA 指令、启动与自动注册
src/Astra.Analyzers/     Roslyn 分析器
docs/                    架构规范与公开 API
libs/                    只读依赖（游戏 / LabAPI 公开化程序集）
tools/                   vsbuild.py、ApiDoc.cs
```

## 文档

- [架构规范](docs/STANDARD.md) — 命名空间、可见性、依赖方向与构建约定
- [公开 API 速查](docs/API.md) — 对外类型与成员清单
- [待补清单](docs/ASTRA-PENDING.md) — 尚未完成的能力与设计缺口
- [协作约定](AGENTS.md) — 提交、构建与交付规范

`docs/API.md` 的生成段由 `dotnet run tools/ApiDoc.cs` 维护；可用 `dotnet run tools/ApiDoc.cs -- --check` 校验文档漂移。

## 版本

当前版本：**3.7.0**

采用 `MAJOR.MINOR.PATCH`：

- 新增公开类型 / 方法：MINOR
- 修改或删除公开签名：MAJOR（需标注 `BREAKING` 并同步 `docs/API.md`）
- 内部实现修复：PATCH

## 许可证

本项目使用 [MIT License](LICENSE)。
