# libs/PublicizedAssemblies/ — 公开化依赖

存放经 `AssemblyPublicizer` 公开化后的只读参考程序集，供补丁代码调用非公开成员（替代反射）。

## 文件

```text
libs/PublicizedAssemblies/
├─ Assembly-CSharp-Publicized.dll
├─ Assembly-CSharp-firstpass-Publicized.dll
├─ CommandSystem.Core-Publicized.dll
├─ HintServiceMeow-Publicized.dll
├─ LabApi-Publicized.dll
├─ Mirror-Publicized.dll
└─ README.md
```

| DLL | 来源 | 用途 |
|---|---|---|
| `Assembly-CSharp-Publicized.dll` | SCP: Secret Laboratory 服务端程序集 | 游戏运行时类型 |
| `Assembly-CSharp-firstpass-Publicized.dll` | 同上 | 游戏运行时类型 |
| `CommandSystem.Core-Publicized.dll` | 同上 | 命令系统 |
| `HintServiceMeow-Publicized.dll` | HintServiceMeow 6.0.0.0 | HUD 提示 |
| `LabApi-Publicized.dll` | LabAPI 1.1.7 | 服务端插件 API |
| `Mirror-Publicized.dll` | SCP: Secret Laboratory 服务端程序集 | 网络同步 |

## 约束

- 只读、入库；变更属于共享依赖变更，需重新公开化并标注来源版本。
- 命名约定为 `<程序集名>-Publicized.dll`；如需多版本并存，再启用子目录分层。
- 引用方设置 `<Private>false</Private>`，运行时由服务端提供原程序集。
