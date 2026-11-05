using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace BrokenScreenResizer
{
    // ===================================================================================
    // Fare kancası ve pencere olayları için ayrı iş parçacığı (v1.7).
    // Ana pencere meşgul olsa bile (ör. başka bir programdan yanıt beklerken) fare takılmaz,
    // oyunlarda imleç gecikmesi olmaz. Pencere olayları sayesinde görünmeyen ekrana düşen
    // pencereler beklemeden, düştükleri anda geri alınır.
    // ===================================================================================
    class HookThread
    {
        // true dönerse fare hareketi engellenir (imleci gereken yere filtre kendisi koyar)
        public delegate bool MouseFilter(int x, int y);

        delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);

        const uint WINEVENT_OUTOFCONTEXT = 0;
        const uint EVENT_SYSTEM_FOREGROUND = 0x3, EVENT_SYSTEM_MOVESIZEEND = 0xB, EVENT_SYSTEM_MINIMIZEEND = 0x17;
        const uint EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        const uint GA_ROOT = 2;

        readonly MouseFilter mouse;
        readonly Action<IntPtr> windowEvent;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        readonly List<IntPtr> winHooks = new List<IntPtr>();
        Thread thread;
        volatile uint threadId;
        IntPtr mouseHook = IntPtr.Zero;
        Win.LowLevelMouseProc mouseProc;   // çöp toplayıcı silmesin diye alanda tutulur
        WinEventProc winProc;

        public HookThread(MouseFilter mouse, Action<IntPtr> windowEvent)
        {
            this.mouse = mouse;
            this.windowEvent = windowEvent;
        }

        public bool MouseHooked { get { return mouseHook != IntPtr.Zero; } }

        public void Start()
        {
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
            ready.WaitOne(3000);
        }

        public void Stop()
        {
            if (thread == null) return;
            if (threadId != 0) VNative.PostThreadMessage(threadId, VNative.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(2000);
            thread = null;
        }

        void Run()
        {
            threadId = VNative.GetCurrentThreadId();
            try
            {
                if (mouse != null)
                {
                    mouseProc = MouseProc;
                    mouseHook = Win.SetWindowsHookEx(Win.WH_MOUSE_LL, mouseProc, Win.GetModuleHandle(null), 0);
                }
                if (windowEvent != null)
                {
                    winProc = WinProc;
                    foreach (var ev in new[] { EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MINIMIZEEND, EVENT_OBJECT_SHOW, EVENT_OBJECT_LOCATIONCHANGE })
                    {
                        var h = SetWinEventHook(ev, ev, IntPtr.Zero, winProc, 0, 0, WINEVENT_OUTOFCONTEXT);
                        if (h != IntPtr.Zero) winHooks.Add(h);
                    }
                }
            }
            catch { }
            ready.Set();

            VNative.MSG msg;
            while (VNative.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                VNative.TranslateMessage(ref msg);
                VNative.DispatchMessage(ref msg);
            }

            foreach (var h in winHooks) UnhookWinEvent(h);
            winHooks.Clear();
            if (mouseHook != IntPtr.Zero) { Win.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
        }

        IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt64() == Win.WM_MOUSEMOVE)
            {
                // MSLLHOOKSTRUCT.pt: yapıya dönüştürmeden okunur (her harekette bellek ayırmasın)
                int x = Marshal.ReadInt32(lParam, 0), y = Marshal.ReadInt32(lParam, 4);
                try { if (mouse(x, y)) return new IntPtr(1); } catch { }
            }
            return Win.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        void WinProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            // sadece üst düzey pencerelerin kendisi (imleç, düzeltme işareti, alt pencereler değil)
            if (hwnd == IntPtr.Zero || idObject != 0 || idChild != 0) return;
            try
            {
                if (GetAncestor(hwnd, GA_ROOT) != hwnd) return;
                windowEvent(hwnd);
            }
            catch { }
        }
    }
}
