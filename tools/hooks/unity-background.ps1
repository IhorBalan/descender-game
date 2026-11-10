# Starts the Unity editor for tools/hooks/pre-push on Windows without letting it
# take the keyboard and mouse from whatever the user is doing.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File unity-background.ps1 <Unity.exe> <arguments...>
#
# Even with -batchmode the editor opens a console window of its own a few
# seconds after it starts, and Windows makes that window the foreground one: a
# game being played stops receiving input until it is clicked again. Started
# with a STARTUPINFO that asks for hidden windows (SW_HIDE), the editor creates
# that console hidden and nothing comes to the foreground. A plain "&" in sh
# cannot ask for that, hence this script. The PlayMode tests keep their
# graphics device: nothing else about the editor changes.
#
# The editor also goes in a job object that ends with this script, so stopping
# the script (the hook does that on Ctrl-C and on a stalled run) stops the editor
# and everything it started. The script exits with the editor's exit code.

$ErrorActionPreference = 'Stop'

if ($args.Count -lt 1) {
    [Console]::Error.WriteLine('usage: unity-background.ps1 <Unity.exe> <arguments...>')
    exit 2
}

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class BackgroundProcess
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processSecurity,
        IntPtr threadSecurity, bool inheritHandles, uint flags, IntPtr environment, string directory,
        ref STARTUPINFO startup, out PROCESS_INFORMATION process);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr security, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool GetExitCodeProcess(IntPtr process, out int exitCode);

    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr process, int exitCode);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private const uint CREATE_SUSPENDED = 0x4;
    private const int STARTF_USESHOWWINDOW = 0x1;
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    // Runs the command line to its end, with its windows hidden, and returns its exit code.
    public static int Run(string commandLine)
    {
        var startup = new STARTUPINFO();
        startup.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        startup.dwFlags = STARTF_USESHOWWINDOW;
        startup.wShowWindow = 0; // SW_HIDE

        var job = CreateJobObject(IntPtr.Zero, null);
        var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        limits.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        if (job == IntPtr.Zero || !SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref limits,
                Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION))))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "the job object could not be set up");
        }

        PROCESS_INFORMATION process;
        if (!CreateProcess(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false,
                CREATE_SUSPENDED, IntPtr.Zero, null, ref startup, out process))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");
        }
        if (!AssignProcessToJobObject(job, process.hProcess))
        {
            var error = Marshal.GetLastWin32Error();
            TerminateProcess(process.hProcess, 1);
            throw new Win32Exception(error, "AssignProcessToJobObject failed");
        }
        ResumeThread(process.hThread);
        WaitForSingleObject(process.hProcess, 0xFFFFFFFF);
        int exitCode;
        GetExitCodeProcess(process.hProcess, out exitCode);
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        CloseHandle(job); // ends whatever the editor left running
        return exitCode;
    }
}
'@

# One command line from the arguments, quoted the way CommandLineToArgvW reads them.
$commandLine = ($args | ForEach-Object {
    if ($_ -match '[\s"]' -or $_ -eq '') {
        '"' + (($_ -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
    } else {
        $_
    }
}) -join ' '

try {
    exit [BackgroundProcess]::Run($commandLine)
} catch {
    [Console]::Error.WriteLine("pre-push: could not start the editor in the background: $($_.Exception.Message)")
    exit 1
}
