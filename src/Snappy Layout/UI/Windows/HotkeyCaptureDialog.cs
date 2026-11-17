using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnappyLayout.Core;
using SnappyLayout.Models;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.UI.Windows
{
    /// <summary>Kısayol tuş kombinasyonu yakalama iletişim kutusu.</summary>
    public sealed class HotkeyCaptureDialog : Window
    {
        public Hotkey ResultHotkey { get; private set; }
        private readonly string _actionKey;
        private readonly TextBlock _previewText;
        private readonly TextBlock _warnText;
        private readonly Button _btnSave;

        public HotkeyCaptureDialog(string actionKey, Hotkey current)
        {
            _actionKey = actionKey;
            ResultHotkey = current?.Clone() ?? new Hotkey();

            Title = Loc.T("HkChange");
            Width = 440;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            SetResourceReference(BackgroundProperty, "CardBgBrush");

            var root = new StackPanel { Margin = new Thickness(20) };

            var prompt = new TextBlock
            {
                Text = Loc.T("HkPressKeys"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 14)
            };
            prompt.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(prompt);

            var boxBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(14, 16, 14, 16),
                Margin = new Thickness(0, 0, 0, 10)
            };
            boxBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
            boxBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

            _previewText = new TextBlock
            {
                Text = ResultHotkey.IsEmpty ? "—" : ResultHotkey.ToString(),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _previewText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            boxBorder.Child = _previewText;
            root.Children.Add(boxBorder);

            _warnText = new TextBlock
            {
                FontSize = 11.5,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _warnText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
            root.Children.Add(_warnText);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var btnClear = new Button
            {
                Content = Loc.T("HkClear"),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnClear.Click += (s, e) =>
            {
                ResultHotkey = new Hotkey();
                DialogResult = true;
            };

            var btnCancel = new Button
            {
                Content = Loc.T("Cancel"),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.Click += (s, e) => DialogResult = false;

            _btnSave = new Button
            {
                Content = Loc.T("Save"),
                Padding = new Thickness(18, 6, 18, 6),
                FontWeight = FontWeights.SemiBold
            };
            _btnSave.Click += (s, e) => DialogResult = true;

            btnRow.Children.Add(btnClear);
            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(_btnSave);
            root.Children.Add(btnRow);

            Content = root;

            Loaded += (s, e) =>
            {
                ThemeManager.ApplyWindowChrome(this);
                App.Instance?.Hotkeys?.Suspend(true);
            };
            Closed += (s, e) => App.Instance?.Hotkeys?.Suspend(false);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                DialogResult = false;
                return;
            }
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
                return;

            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= MOD_SHIFT;
            if (IsKeyDown(VK_LWIN)) mods |= MOD_WIN;

            // En az bir değiştirici tuş (veya F1..F12) zorunlu olsun
            bool isFunc = key >= Key.F1 && key <= Key.F12;
            if (mods == 0 && !isFunc) return;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            ResultHotkey = new Hotkey(mods, vk);
            _previewText.Text = ResultHotkey.ToString();

            var dup = HotkeyService.FindDuplicate(_actionKey, ResultHotkey);
            if (dup != null)
            {
                _warnText.Text = Loc.T("HkConflict");
                _warnText.Visibility = Visibility.Visible;
            }
            else
            {
                _warnText.Visibility = Visibility.Collapsed;
            }
        }
    }
}
