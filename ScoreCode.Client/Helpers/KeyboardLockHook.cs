using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// Installs a low-level (WH_KEYBOARD_LL) keyboard hook that swallows the OS
/// task-switching / window-management shortcuts for the duration of an exam:
/// Alt+Tab, the Windows keys, Ctrl+Esc, Alt+Esc and Alt+F4.
///
/// A borderless Topmost window alone does NOT stop these — the shell handles
/// them before the app ever sees a WPF key event, so a global hook is required.
/// Note: Ctrl+Alt+Del (the secure attention sequence) cannot be blocked by any
/// user-mode hook; that path is instead caught by the window's focus-loss handler.
///
/// Must be created on the UI thread (it relies on that thread's message loop)
/// and disposed when the exam ends to restore normal key behaviour.
/// </summary>
public sealed class KeyboardLockHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;

    private const uint LLKHF_ALTDOWN = 0x20;

    private const int VK_TAB     = 0x09;
    private const int VK_CONTROL = 0x11;
    private const int VK_ESCAPE  = 0x1B;
    private const int VK_F4      = 0x73;
    private const int VK_LWIN    = 0x5B;
    private const int VK_RWIN    = 0x5C;

    // Keep a strong reference so the delegate isn't garbage-collected while hooked.
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly Action<string>? _onBlocked;
    private bool _disposed;

    /// <param name="onBlocked">
    /// Invoked (on the UI thread) with a short description each time a shortcut is
    /// swallowed. Best-effort — keep it fast and non-throwing.
    /// </param>
    public KeyboardLockHook(Action<string>? onBlocked = null)
    {
        _onBlocked = onBlocked;
        _proc = HookCallback;
        _hookId = SetHook(_proc);
    }

    public bool IsInstalled => _hookId != IntPtr.Zero;

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            GetModuleHandle(curModule.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (TryDescribeBlocked(data, out var description))
                {
                    try { _onBlocked?.Invoke(description); } catch { /* never break the hook */ }
                    return 1; // swallow the key — do not pass to the shell
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool TryDescribeBlocked(KBDLLHOOKSTRUCT data, out string description)
    {
        var vk = (int)data.vkCode;
        bool alt = (data.flags & LLKHF_ALTDOWN) != 0;
        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

        description = vk switch
        {
            VK_LWIN or VK_RWIN                   => "Windows key",
            VK_TAB when alt                       => "Alt+Tab",
            VK_ESCAPE when alt                    => "Alt+Esc",
            VK_ESCAPE when ctrl                   => "Ctrl+Esc",
            VK_F4 when alt                        => "Alt+F4",
            _                                     => string.Empty
        };
        return description.Length > 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    // ── Win32 ──────────────────────────────────────────────────────────────
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
