using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using SnappyLayout.Models;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>Global kısayol tuşları (RegisterHotKey).</summary>
    public sealed class HotkeyService : IDisposable
    {
        private readonly HwndSource _source;
        private readonly Dictionary<int, string> _idToAction = new Dictionary<int, string>();
        private int _nextId = 1;
        private bool _suspended;

        /// <summary>Eylem kimliği; ör. "ShowFlyout", "ws:{guid}", "vp:{guid}"</summary>
        public event Action<string> Pressed;

        /// <summary>Kaydı başarısız olan (başka program tarafından kullanılan) eylemler.</summary>
        public HashSet<string> Failed { get; } = new HashSet<string>();
        public event Action RegistrationChanged;

        public HotkeyService()
        {
            var p = new HwndSourceParameters("SnappyLayoutHotkeys") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) /* HWND_MESSAGE */ };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_idToAction.TryGetValue(id, out var action))
                {
                    handled = true;
                    try { Pressed?.Invoke(action); } catch (Exception ex) { Logger.Log("Hotkey action: " + ex); }
                }
            }
            return IntPtr.Zero;
        }

        public static IEnumerable<(string Action, Hotkey Key)> AllBindings()
        {
            var d = Storage.Data;
            foreach (var kv in d.Hotkeys) yield return (kv.Key, kv.Value);
            foreach (var p in d.VirtualPresets) yield return ("vp:" + p.Id, p.Hotkey);
            foreach (var w in d.Workspaces) yield return ("ws:" + w.Id, w.Hotkey);
        }

        public void ReRegister()
        {
            UnregisterAll();
            Failed.Clear();
            if (_suspended) { RegistrationChanged?.Invoke(); return; }
            foreach (var (action, key) in AllBindings())
            {
                if (key == null || key.IsEmpty) continue;
                int id = _nextId++;
                if (_nextId > 0xBFFF) _nextId = 1;
                if (RegisterHotKey(_source.Handle, id, key.Modifiers | MOD_NOREPEAT, key.Key))
                    _idToAction[id] = action;
                else
                    Failed.Add(action);
            }
            RegistrationChanged?.Invoke();
        }

        /// <summary>Kısayol düzenlenirken tuşların yakalanabilmesi için geçici olarak askıya alır.</summary>
        public void Suspend(bool suspend)
        {
            if (_suspended == suspend) return;
            _suspended = suspend;
            ReRegister();
        }

        /// <summary>Aynı kombinasyonu kullanan başka bir eylem var mı?</summary>
        public static string FindDuplicate(string action, Hotkey key)
        {
            if (key == null || key.IsEmpty) return null;
            return AllBindings().Where(b => b.Action != action && b.Key != null && b.Key.SameAs(key)).Select(b => b.Action).FirstOrDefault();
        }

        private void UnregisterAll()
        {
            foreach (var id in _idToAction.Keys.ToList()) UnregisterHotKey(_source.Handle, id);
            _idToAction.Clear();
        }

        public void Dispose()
        {
            UnregisterAll();
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }
}
