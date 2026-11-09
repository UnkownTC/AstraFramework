# AstraFramework

基于 LabAPI 的 SCP: Secret Laboratory 服务端框架：自定义角色 / 阵营 / 波次 / 物品 / 效果 / 技能，外加托管补丁、事件与方法级 Harmony 声明。

## 结构

```
src/Astra.Framework/     主程序集（唯一 DLL，按域分目录：Roles/Teams/Waves/Items/Effects/...）
src/Astra.Analyzers/     Roslyn 分析器（约束契约与用法）
libs/                    只读依赖（游戏与 LabAPI 的公开化程序集）
tools/vsbuild.py         构建入口（VS MSBuild 包装，net481 必需）
docs/                    STANDARD / API / DEPLOY
```

## 构建

```bash
python tools/vsbuild.py src/Astra.Framework/Astra.Framework.csproj -p:Configuration=Release -p:Platform=x64
```

`net481` 工程必须用完整的 .NET Framework MSBuild（`dotnet build` 对 COM 引用会报 MSB4803），因此统一走 `tools/vsbuild.py`。

## 设计要点

- **按域内聚**：`Astra.Roles` / `Astra.Teams` / `Astra.Waves` / `Astra.Items` … 一个域一个命名空间
- **默认公开**：业务需要调用或继承的类型为 `public`，仅框架调度内部化
- **调用一跳**：业务 → 管理器，无门面层
- 无 `Systems` / `Abstractions` / `Apis` 分层命名空间

详见 [docs/STANDARD.md](docs/STANDARD.md)。

## 版本

`MAJOR.MINOR.PATCH`；公开签名变更升 MAJOR。
