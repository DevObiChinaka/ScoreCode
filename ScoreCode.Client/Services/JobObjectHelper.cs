using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScoreCode.Client.Services;

/// <summary>
/// Creates a Windows Job Object with CPU time and memory limits,
/// assigns a process to it, and reports whether the process was killed
/// due to resource violations.
/// </summary>
public sealed class JobObjectHelper : IDisposable
{
    private IntPtr _jobHandle;

    /// <summary>
    /// Create a Job Object with hard resource limits.
    /// </summary>
    /// <param name="cpuLimitSeconds">Max CPU time in seconds (per-process).</param>
    /// <param name="memoryLimitBytes">Max committed memory in bytes.</param>
    public JobObjectHelper(int cpuLimitSeconds = 5, long memoryLimitBytes = 256 * 1024 * 1024)
    {
        _jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (_jobHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create Job Object.");

        // Convert CPU limit to 100-nanosecond intervals
        long cpuLimit100ns = (long)cpuLimitSeconds * 10_000_000L;

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                PerProcessUserTimeLimit = cpuLimit100ns,
                LimitFlags = JOB_OBJECT_LIMIT_PROCESS_TIME |
                             JOB_OBJECT_LIMIT_PROCESS_MEMORY |
                             JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            },
            ProcessMemoryLimit = (UIntPtr)memoryLimitBytes
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
            if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                    extendedInfoPtr, (uint)length))
            {
                throw new InvalidOperationException(
                    $"Failed to set Job Object limits. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    /// <summary>Assign a running process to this Job Object.</summary>
    public bool AssignProcess(Process process)
    {
        try
        {
            return AssignProcessToJobObject(_jobHandle, process.Handle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determine WHY a process exited — was it killed by the Job Object?
    /// </summary>
    public (bool timedOut, bool memoryExceeded) GetExitReason(Process process)
    {
        // If the process exited with specific codes, check Job Object counters
        try
        {
            int length = Marshal.SizeOf(typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION));
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try
            {
                if (QueryInformationJobObject(_jobHandle,
                        JobObjectInfoType.BasicAccountingInformation, ptr, (uint)length, IntPtr.Zero))
                {
                    var info = Marshal.PtrToStructure<JOBOBJECT_BASIC_ACCOUNTING_INFORMATION>(ptr);
                    // If total user time is at or near the limit, it was a timeout
                    bool timedOut = info.TotalUserTime >= 
                        ((JOBOBJECT_BASIC_LIMIT_INFORMATION)GetBasicLimitInfo()).PerProcessUserTimeLimit * 9 / 10;
                    // Memory exceeded is harder to detect — if exit code suggests termination
                    bool memoryExceeded = !timedOut && process.ExitCode != 0 && process.ExitCode != 1;
                    return (timedOut, memoryExceeded);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch { /* fall through */ }

        // Heuristic: non-zero exit after forced kill → check if it was resource-based
        if (process.ExitCode < 0)
            return (true, false); // Negative exit codes usually mean forced termination
        return (false, false);
    }

    private JOBOBJECT_BASIC_LIMIT_INFORMATION GetBasicLimitInfo()
    {
        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr ptr = Marshal.AllocHGlobal(length);
        try
        {
            if (QueryInformationJobObject(_jobHandle,
                    JobObjectInfoType.ExtendedLimitInformation, ptr, (uint)length, IntPtr.Zero))
            {
                return Marshal.PtrToStructure<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>(ptr)
                    .BasicLimitInformation;
            }
        }
        finally { Marshal.FreeHGlobal(ptr); }
        return default;
    }

    public void Dispose()
    {
        if (_jobHandle != IntPtr.Zero)
        {
            CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }
    }

    // ── Win32 P/Invoke ────────────────────────────────────────────────────

    private const uint JOB_OBJECT_LIMIT_PROCESS_TIME     = 0x00000002;
    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY   = 0x00000100;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private enum JobObjectInfoType
    {
        BasicAccountingInformation = 1,
        BasicLimitInformation = 2,
        ExtendedLimitInformation = 9,
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType,
        IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll")]
    private static extern bool QueryInformationJobObject(IntPtr hJob, JobObjectInfoType infoType,
        IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength, IntPtr lpReturnLength);

    [DllImport("kernel32.dll")]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    // ── Structs ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
    {
        public long TotalUserTime;
        public long TotalKernelTime;
        public long ThisPeriodTotalUserTime;
        public long ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount;
        public uint TotalProcesses;
        public uint ActiveProcesses;
        public uint TotalTerminatedProcesses;
    }
}
