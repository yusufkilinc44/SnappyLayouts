using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SnappyLayout.Models;
using SnappyLayout.Native;

namespace SnappyLayout.Services
{
    /// <summary>
    /// Açık (Light), Koyu (Dark) ve Windows Varsayılanı (System) tema yöneticisi.
    /// </summary>
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = true;
        public static event Action ThemeChanged;
        public static event Action WindowsThemeChanged;

        static ThemeManager()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (Storage.Data?.Settings?.Theme == ThemeChoice.System)
                    {
                        Apply(ThemeChoice.System);
                    }
                    WindowsThemeChanged?.Invoke();
                }));
            };
        }

        public static bool DetectWindowsDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("SystemUsesLightTheme") is int sysVal)
                    return sysVal == 0;
                if (key?.GetValue("AppsUseLightTheme") is int appVal)
                    return appVal == 0;
            }
            catch { }
            return true;
        }

        /// <summary>
        /// Verilen ResourceDictionary içine belirtilen temanın (dark/light) tüm fırça ve kontrol stillerini yazar.
        /// </summary>
        public static void PopulateBrushes(ResourceDictionary r, bool dark)
        {
            if (r == null) return;
            if (dark)
            {
                SetBrush(r, "AppBgBrush", "#0F141C");
                SetBrush(r, "SidebarBgBrush", "#141B26");
                SetBrush(r, "CardBgBrush", "#192231");
                SetBrush(r, "CardHoverBgBrush", "#202C40");
                SetBrush(r, "CardBorderBrush", "#2A374E");
                SetBrush(r, "ControlBgBrush", "#222E42");
                SetBrush(r, "ControlHoverBgBrush", "#2D3D57");
                SetBrush(r, "ControlPressedBgBrush", "#182232");
                SetBrush(r, "ControlBorderBrush", "#3D5170");
                SetBrush(r, "InputBgBrush", "#121924");
                SetBrush(r, "TextPrimaryBrush", "#F2F6FC");
                SetBrush(r, "TextSecondaryBrush", "#B0C0D8");
                SetBrush(r, "TextMutedBrush", "#7A8CA8");
                SetBrush(r, "AccentBrush", "#3B82F6");
                SetBrush(r, "AccentHoverBrush", "#60A5FA");
                SetBrush(r, "AccentSubtleBrush", "#1E3A6E");
                SetBrush(r, "PermanentBrush", "#10B981");
                SetBrush(r, "PermanentSubtleBrush", "#0F382C");
                SetBrush(r, "WarningBrush", "#F59E0B");
                SetBrush(r, "DangerBrush", "#EF4444");
                SetBrush(r, "ZoneFillBrush", "#24344D");
                SetBrush(r, "ZoneHoverBrush", "#3B82F6");
                SetBrush(r, "ZoneStrokeBrush", "#5B7AA6");
                SetBrush(r, "FlyoutBgBrush", "#2C2C2C");
                SetBrush(r, "FlyoutBorderBrush", "#454545");
                SetBrush(r, "Win11SurfaceBrush", "#2C2C2C");
                SetBrush(r, "Win11BorderBrush", "#474747");
                SetBrush(r, "Win11PillBgBrush", "#383838");
                SetBrush(r, "Win11PillBorderBrush", "#505050");
                SetBrush(r, "Win11PillActiveBgBrush", "#005FB8");
                SetBrush(r, "Win11PillActiveBorderBrush", "#60CDFF");
                SetBrush(r, "Win11ZoneFillBrush", "#464646");
                SetBrush(r, "Win11ZoneStrokeBrush", "#6D6D6D");
                SetBrush(r, "Win11ZoneHoverBrush", "#0078D4");
                SetBrush(r, "Win11ZoneHoverStrokeBrush", "#60CDFF");
                SetBrush(r, "Win11PermFillBrush", "#22463A");
                SetBrush(r, "Win11PermStrokeBrush", "#34D399");
                SetBrush(r, "Win11PermHoverBrush", "#10B981");

                // Fluent dahili fırça anahtarlarını da senkronize et
                SetBrush(r, "TextFillColorPrimaryBrush", "#F2F6FC");
                SetBrush(r, "TextFillColorSecondaryBrush", "#B0C0D8");
                SetBrush(r, "ControlFillColorDefaultBrush", "#222E42");
                SetBrush(r, "ControlFillColorSecondaryBrush", "#2D3D57");
                SetBrush(r, "ControlStrokeColorDefaultBrush", "#3D5170");
            }
            else
            {
                SetBrush(r, "AppBgBrush", "#F1F5F9");
                SetBrush(r, "SidebarBgBrush", "#E2E8F0");
                SetBrush(r, "CardBgBrush", "#FFFFFF");
                SetBrush(r, "CardHoverBgBrush", "#EFF6FF");
                SetBrush(r, "CardBorderBrush", "#CBD5E1");
                SetBrush(r, "ControlBgBrush", "#F8FAFC");
                SetBrush(r, "ControlHoverBgBrush", "#E2E8F0");
                SetBrush(r, "ControlPressedBgBrush", "#CBD5E1");
                SetBrush(r, "ControlBorderBrush", "#94A3B8");
                SetBrush(r, "InputBgBrush", "#FFFFFF");
                SetBrush(r, "TextPrimaryBrush", "#0F172A");
                SetBrush(r, "TextSecondaryBrush", "#334155");
                SetBrush(r, "TextMutedBrush", "#64748B");
                SetBrush(r, "AccentBrush", "#2563EB");
                SetBrush(r, "AccentHoverBrush", "#1D4ED8");
                SetBrush(r, "AccentSubtleBrush", "#DBEAFE");
                SetBrush(r, "PermanentBrush", "#059669");
                SetBrush(r, "PermanentSubtleBrush", "#D1FAE5");
                SetBrush(r, "WarningBrush", "#D97706");
                SetBrush(r, "DangerBrush", "#DC2626");
                SetBrush(r, "ZoneFillBrush", "#CBD5E1");
                SetBrush(r, "ZoneHoverBrush", "#2563EB");
                SetBrush(r, "ZoneStrokeBrush", "#475569");
                SetBrush(r, "FlyoutBgBrush", "#F3F3F3");
                SetBrush(r, "FlyoutBorderBrush", "#D1D1D1");
                SetBrush(r, "Win11SurfaceBrush", "#F3F3F3");
                SetBrush(r, "Win11BorderBrush", "#CCCCCC");
                SetBrush(r, "Win11PillBgBrush", "#EAEAEA");
                SetBrush(r, "Win11PillBorderBrush", "#C8C8C8");
                SetBrush(r, "Win11PillActiveBgBrush", "#D6ECFF");
                SetBrush(r, "Win11PillActiveBorderBrush", "#0067C0");
                SetBrush(r, "Win11ZoneFillBrush", "#E3E3E3");
                SetBrush(r, "Win11ZoneStrokeBrush", "#B4B4B4");
                SetBrush(r, "Win11ZoneHoverBrush", "#0067C0");
                SetBrush(r, "Win11ZoneHoverStrokeBrush", "#005A9E");
                SetBrush(r, "Win11PermFillBrush", "#D1FAE5");
                SetBrush(r, "Win11PermStrokeBrush", "#059669");
                SetBrush(r, "Win11PermHoverBrush", "#059669");

                // Fluent dahili fırça anahtarlarını da senkronize et
                SetBrush(r, "TextFillColorPrimaryBrush", "#0F172A");
                SetBrush(r, "TextFillColorSecondaryBrush", "#334155");
                SetBrush(r, "ControlFillColorDefaultBrush", "#F8FAFC");
                SetBrush(r, "ControlFillColorSecondaryBrush", "#E2E8F0");
                SetBrush(r, "ControlStrokeColorDefaultBrush", "#94A3B8");
            }
        }

        /// <summary>
        /// Uygulama temasından bağımsız olarak bir pencereye (örn. SnapFlyoutWindow) doğrudan Windows temasını uygular.
        /// </summary>
        public static void ApplyWindowsThemeToDictionary(ResourceDictionary targetDict)
        {
            if (targetDict == null) return;
            bool winDark = DetectWindowsDark();
            PopulateBrushes(targetDict, winDark);
        }

        public static void Apply(ThemeChoice choice)
        {
            bool dark = choice switch
            {
                ThemeChoice.Dark => true,
                ThemeChoice.Light => false,
                _ => DetectWindowsDark()
            };
            IsDark = dark;

            var app = Application.Current;
            if (app == null) return;

            try
            {
                var uri = new Uri(dark
                    ? "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Dark.xaml"
                    : "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml",
                    UriKind.Absolute);
                var newFluent = new ResourceDictionary { Source = uri };
                // Önceki Fluent sözlüklerinin tamamını temizle ki Dark sözlüğü Light sözlüğünü ezmesin
                app.Resources.MergedDictionaries.Clear();
                app.Resources.MergedDictionaries.Add(newFluent);
            }
            catch (Exception ex)
            {
                Logger.Log("Fluent dictionary switch warning: " + ex.Message);
            }

            var r = app.Resources;
            PopulateBrushes(r, dark);

            // Fluent.xaml sözlüğü yenilendiğinde örtük (implicit) kontrol stillerini yeniden bağla
            RebindImplicitControlStyles(r);

            foreach (Window w in app.Windows)
            {
                ApplyWindowChrome(w);
            }

            ThemeChanged?.Invoke();
        }

        private static void RebindImplicitControlStyles(ResourceDictionary r)
        {
            try
            {
                if (r["AppButtonStyle"] is Style btnStyle) r[typeof(System.Windows.Controls.Button)] = btnStyle;
                if (r["AppTextBoxStyle"] is Style tbStyle) r[typeof(System.Windows.Controls.TextBox)] = tbStyle;
                if (r["AppCheckBoxStyle"] is Style cbStyle) r[typeof(System.Windows.Controls.CheckBox)] = cbStyle;
                if (r["AppComboBoxItemStyle"] is Style cbiStyle) r[typeof(System.Windows.Controls.ComboBoxItem)] = cbiStyle;
                if (r["AppComboBoxStyle"] is Style cmbStyle) r[typeof(System.Windows.Controls.ComboBox)] = cmbStyle;
            }
            catch { }
        }

        public static void ApplyWindowChrome(Window w)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h != IntPtr.Zero) NativeMethods.SetDarkTitleBar(h, IsDark);
            }
            catch { }
        }

        public static Color GetBorderOverlayColor(string colorKey)
        {
            return colorKey switch
            {
                "Cyan" => Color.FromRgb(6, 182, 212),
                "Gold" => Color.FromRgb(245, 158, 11),
                "Coral" => Color.FromRgb(244, 63, 94),
                "Emerald" => Color.FromRgb(16, 185, 129),
                _ => Color.FromRgb(59, 130, 246),
            };
        }

        private static void SetBrush(ResourceDictionary r, string key, string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            r[key] = b;
        }
    }
}
