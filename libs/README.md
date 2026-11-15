# libs/ — 只读依赖

本目录只保留构建与运行所必需的二进制，供 `src/Astra.Framework` 与下游插件引用。

## 目录

| 路径 | 内容 |
|---|---|
| `PublicizedAssemblies/` | 游戏、LabAPI 与 HintServiceMeow 的公开化参考程序集（6 个 DLL） |
| `audio/current/` | `NAudio.Core.dll`、`SecretLabNAudio.Core.dll` |
| `harmony/2.4.2/` | Harmony 运行时 |
| `unity/current/` | `UnityEngine.CoreModule.dll`、`UnityEngine.PhysicsModule.dll`、`UnityEngine.SharedInternalsModule.dll` |
| `net-crypto/current/` | `YamlDotNet.dll` |
| `scpsl-server/current/` | `Pooling.dll` |

## 约束

- 只读：不得修改目录内 DLL；需要新版本时新增目录并登记。
- 新增依赖必须先确认构建或运行需要，再提交到本目录，并在引用方 `.csproj` 用 `$(LibsRoot)` 定位。
- 禁止在项目文件中硬编码仓库外的绝对路径。
