# Astra.Framework 开发说明

本文件原先承载 Azure Framework 时代的架构与实现说明，其中 `Astra.Interfaces`、`Astra.Abstractions`、`Astra.Apis`、`Astra.Systems`、`EventManager.Instance` 等内容在 Astra 2.0 / 3.0 中已移除，旧文仅保留在 git 历史中。

现行文档入口：

| 文档 | 内容 |
|---|---|
| [`AGENTS.md`](../../AGENTS.md) | 协作、提交、构建与交付约定 |
| [`docs/STANDARD.md`](../../docs/STANDARD.md) | 现行框架规范（命名空间、可见性、依赖、分析器、构建） |
| [`docs/API.md`](../../docs/API.md) | 公开 API 速查（含自动生成段） |
| [`docs/ASTRA-PENDING.md`](../../docs/ASTRA-PENDING.md) | 待补能力与已知修复留档 |

构建（net481 必须走封装脚本）：

```bash
python tools/vsbuild.py src/Astra.Framework/Astra.Framework.csproj -p:Configuration=Release -p:Platform=x64
python tools/vsbuild.py src/Astra.Analyzers/Astra.Analyzers.csproj -p:Configuration=Release
```
