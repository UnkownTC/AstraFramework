# AGENTS.md — AstraFramework 协作约定

> 适用于本仓库全部代码。优先级：本文件 > `docs/STANDARD.md` > 代码注释 > 个人习惯。
> **宽松原则**：本仓库是库，不是服务，绝大多数约束只求「可追溯、可编译、不破坏使用者」，不设强制评审门禁。

---

## 0. 项目定位

AstraFramework 是 SCP: Secret Laboratory 服务端框架库（LabAPI 之上），提供自定义角色 / 阵营 / 波次 / 物品 / 效果 / 技能，以及托管补丁与声明式事件。

| 是 | 不是 |
|---|---|
| 可被任意插件引用的独立库 | Site19 的业务代码仓库 |
| 只包含抽象与机制 | 具体角色/波次实现（放业务包） |
| 服务端逻辑 | 客户端模组 |

---

## 1. 文件地图

| 路径 | 职责 | 何时看 |
|---|---|---|
| `AGENTS.md` | 本文件：协作与提交约定 | 每次动手前 |
| `docs/STANDARD.md` | 架构规范：命名空间、可见性、依赖方向 | 新增类型/移动文件前 |
| `docs/API.md` | 对外 API 速查 | 改公开签名时 |
| `src/Astra.Framework/` | 主程序集（按域分目录） | — |
| `src/Astra.Analyzers/` | Roslyn 分析器（AST 系列规则） | 改规则时 |
| `libs/` | 只读依赖（游戏与 LabAPI 公开化程序集） | 需要新引用时 |
| `tools/vsbuild.py` | 构建入口（net481 必需） | 每次构建 |
| `tools/ApiDoc.cs` | `docs/API.md` 自动段生成器（Roslyn 扫描公开 API） | 改公开签名后 |
| `artifacts/` | 构建日志与产物（不提交） | 排查失败时 |

---

## 2. 三条铁律

1. **可回滚**：任何改动前，当前状态应能通过 `git checkout` 恢复；不制造不可逆状态。
2. **不破坏使用者**：公开类型的删除/改名/签名变更属于破坏性变更，需升 MAJOR 并在提交信息注明。
3. **可追溯**：每次改动必须提交，提交信息包含改动范围与动机。

除此之外不设额外流程——不强制 PR、不强制任务卡、不强制双人评审。

---

## 3. 分支与提交

### 3.1 分支

| 场景 | 建议 |
|---|---|
| 单人小改动（1~2 个文件） | 直接提交 `main` |
| 结构重构 / 跨域改动 | 简短分支 `refactor/<要点>`、`feat/<要点>`，完成后合并 |
| 实验性改动 | 随意分支，推不推都行 |

不做强制分支命名规范；**唯一要求**：不要在同一分支上重写他人已推送的提交历史。

### 3.2 提交信息

```
<type>: <一句话说明>

<可选：为什么这么改、影响范围、破坏性变更提示>
```

`type`：`feat` `fix` `refactor` `perf` `docs` `build` `test` `chore`。

**必须**：能看懂改了什么。
**不必**：任务 ID、模型名、时间戳（这里不是多 Agent 协作仓）。

### 3.3 一次提交一件事

允许相邻小修合并，但不要把「改命名空间 + 修 bug + 加功能」塞进一个提交。

---

## 4. 构建与验证

```bash
python tools/vsbuild.py src/Astra.Framework/Astra.Framework.csproj -p:Configuration=Release -p:Platform=x64
python tools/vsbuild.py src/Astra.Analyzers/Astra.Analyzers.csproj -p:Configuration=Release
```

| 要求 | 强度 |
|---|---|
| 编译 0 error | **必须** |
| 不新增 warning | 建议（现有 nullable 警告可暂时共存） |
| `-warnaserror` | 发布前建议跑一次，日常不强制 |
| 分析器自测 | 改规则时必须（构造正反例各一） |

**注意**：`net481` + COM/公开化程序集必须走 `tools/vsbuild.py`；`dotnet build` 会报 MSB4803 或 RuntimeIdentifier 错误。

---

## 5. 代码约定（宽松版）

### 5.1 命名空间

- 一个目录一个命名空间，目录名 = 命名空间末段（`Roles/` → `Astra.Roles`）
- **不存在** `Systems` / `Abstractions` / `Apis` / `Core` 这类技术分层
- 新增能力 → 新建域目录，不要塞进已有域

### 5.2 可见性

```
业务需要调用/继承/实现/读取 → public
只服务框架自身调度        → internal
```

- 公开类型必须有 XML 文档（`/// <summary>`），方法至少一行说明
- `internal` 类型不得出现在任何 `public` 签名中（编译器会拦，不要用 `InternalsVisibleTo` 绕过）

### 5.3 依赖方向

| 允许 | 避免 |
|---|---|
| 域内自由引用 | 域之间静态方法互调 |
| 任意域 → `Enums` / `Attributes` / `Extensions` / `Events`(参数) | 基类反向依赖业务域管理器 |
| `Loader` 决定注册顺序 | 域间循环依赖 |

跨域协作优先用：注册顺序 → 事件 → 显式注入。

### 5.4 风格

- 优先 `file-scoped namespace`，`using` 分组（System → Astra → LabApi → 其它）
- 一个文件一个公开类型
- 不为「可能将来用得上」而抽象；不写空的预留接口

---

## 6. API 变更规则

| 变更 | 版本 | 额外要求 |
|---|---|---|
| 新增公开类型/方法 | MINOR | — |
| 修改公开签名 / 删除 | **MAJOR** | 提交信息标注 `BREAKING`，`docs/API.md` 同步 |
| 修内部实现 | PATCH | — |

新增公开 API 前先问一句：「业务真的需要直接调用它吗？」——能 internal 就 internal。

---

## 7. 分析器规则

| ID | 内容 | 级别 |
|---|---|---|
| AST1001~1004 | `[EventMethod]` 签名 / override / 可访问性 / 事件存在 | Error（AST1003 为 Warning） |
| AST2001~2003 | `[PatchMethod]` 静态 / 补丁类型 / 目标存在 | Error |
| AST2101 | `[ManagedPatch]` 缺目标声明 | Error |
| AST3001 | 版本特性用在非补丁成员 | Info |
| AST5001~5005 | 契约范围（事件基类 / 实例化契约 / 静态类） | Error（AST5003 为 Warning） |

**豁免**：确有正当理由时，用 `#pragma warning disable ASTxxxx` 并附一行注释说明原因，不要静默全局关闭。

---

## 8. 交付说明格式（简版）

改动完成时给出：

1. **改了什么**（文件/类型清单）
2. **怎么验证的**（构建结果、必要的实机验证点）
3. **有什么风险**（破坏性变更、未验证假设）

不需要五段式报告，也不需要任务卡；说清楚这三点即可。

---

## 9. 边界

| 不做 | 说明 |
|---|---|
| 修改 Site19 业务仓库 | 本仓独立，业务切换由其自行决定 |
| 引入客户端模组 | 服务端框架，客户端表现只能近似 |
| 自建远程仓库 / 推送 | 由仓库所有者操作 |
| 在 `libs/` 里改 DLL | 只读区；需要新版就新增目录并登记 |
