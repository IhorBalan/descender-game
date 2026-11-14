# Starts the Unity editor on Windows without letting it, or anything it starts,
# take the keyboard and mouse from whatever the user is doing. unity-editor.sh
# (the pre-push hook and tools/unity/run-editor.sh) starts every editor run on
# Windows through this script.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File unity-background.ps1 <Unity.exe> <arguments...>
#
# Even with -batchmode the editor opens a console window of its own a few
# seconds after it starts, and Windows makes that window the foreground one: a
# game being played stops receiving input until it is clicked again. Helper
# processes (shader compiler, package manager, licensing, crash handler) and any
# dialog the editor shows can do the same. Two things keep all of them away:
#
# - The editor runs on a desktop of its own, created here in the same window
#   station and never switched to. Every process it starts inherits that
#   desktop, and a window on a desktop that is not the input desktop can never
#   become the foreground window or show on screen, whatever it asks for. The
#   GPU is not tied to a desktop, so the PlayMode tests keep their graphics
#   device.
# - Its STARTUPINFO asks for hidden windows (SW_HIDE), so the console it opens
#   is hidden too. UNITY_BACKGROUND_SAME_DESKTOP=1 keeps only this one.
#
# Every visible window that appears on the editor's desktop is reported on
# stderr with its process ("unity-background: ... window shown by ..."): on the
# user's desktop it would have come to the front. UNITY_BACKGROUND_WINDOW_LOG=<file>
# also appends those lines to a file.
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, string device, IntPtr devmode, uint flags,
        uint access, IntPtr security);

    [DllImport("user32.dll")]
    private static extern bool CloseDesktop(IntPtr desktop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetProcessWindowStation();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder info, int length,
        out int needed);

    [DllImport("user32.dll")]
    private static extern bool EnumDesktopWindows(IntPtr desktop, EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder text, int length);

    private const uint CREATE_SUSPENDED = 0x4;
    private const int STARTF_USESHOWWINDOW = 0x1;
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const uint GENERIC_ALL = 0x10000000;
    private const int UOI_NAME = 2;
    private const uint WAIT_TIMEOUT = 0x102;

    // Runs the command line to its end, on a desktop of its own unless sameDesktop, with its
    // windows hidden, and returns its exit code. Visible windows on that desktop go to report.
    public static int Run(string commandLine, bool sameDesktop, Action<string> report)
    {
        var startup = new STARTUPINFO();
        startup.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        startup.dwFlags = STARTF_USESHOWWINDOW;
        startup.wShowWindow = 0; // SW_HIDE

        var desktop = IntPtr.Zero;
        if (!sameDesktop)
        {
            var name = "descender-unity-" + Process.GetCurrentProcess().Id;
            desktop = CreateDesktop(name, null, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
            if (desktop == IntPtr.Zero)
            {
                report("could not create a desktop for the editor (error " + Marshal.GetLastWin32Error()
                    + "); starting it on this one with its windows hidden");
            }
            else
            {
                startup.lpDesktop = WindowStationName() + "\\" + name;
            }
        }

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
        var seen = new HashSet<IntPtr>();
        while (WaitForSingleObject(process.hProcess, 250) == WAIT_TIMEOUT)
        {
            if (desktop != IntPtr.Zero)
            {
                ReportNewWindows(desktop, seen, report);
            }
        }
        int exitCode;
        GetExitCodeProcess(process.hProcess, out exitCode);
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        CloseHandle(job); // ends whatever the editor left running
        if (desktop != IntPtr.Zero)
        {
            CloseDesktop(desktop);
        }
        return exitCode;
    }

    private static string WindowStationName()
    {
        var name = new StringBuilder(256);
        int needed;
        if (GetUserObjectInformation(GetProcessWindowStation(), UOI_NAME, name, name.Capacity * 2, out needed))
        {
            return name.ToString();
        }
        return "WinSta0";
    }

    private static void ReportNewWindows(IntPtr desktop, HashSet<IntPtr> seen, Action<string> report)
    {
        var found = new List<IntPtr>();
        EnumWindowsProc collect = (window, parameter) =>
        {
            if (IsWindowVisible(window) && seen.Add(window))
            {
                found.Add(window);
            }
            return true;
        };
        EnumDesktopWindows(desktop, collect, IntPtr.Zero);
        GC.KeepAlive(collect);
        foreach (var window in found)
        {
            int processId;
            GetWindowThreadProcessId(window, out processId);
            string processName;
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
            }
            catch (ArgumentException)
            {
                processName = "(exited)";
            }
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            var windowClass = new StringBuilder(256);
            GetClassName(window, windowClass, windowClass.Capacity);
            if (windowClass.ToString().StartsWith("UAC_InputIndicator") || windowClass.ToString() == "UAC Input Indicator")
            {
                continue; // the input-method indicator every GUI thread gets; it never takes focus
            }
            report("window shown by " + processName + " (pid " + processId + "), class " + windowClass
                + ", title '" + title + "'; it stayed on the editor's own desktop");
        }
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

$windowLog = $env:UNITY_BACKGROUND_WINDOW_LOG
$report = [Action[string]] {
    param($message)
    $line = "unity-background: $(Get-Date -Format 'HH:mm:ss') $message"
    [Console]::Error.WriteLine($line)
    if ($windowLog) {
        Add-Content -LiteralPath $windowLog -Value $line -Encoding utf8
    }
}

try {
    exit [BackgroundProcess]::Run($commandLine, [bool]$env:UNITY_BACKGROUND_SAME_DESKTOP, $report)
} catch {
    [Console]::Error.WriteLine("unity-background: could not start the editor in the background: $($_.Exception.Message)")
    exit 1
}
