﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// UI Components
// BlurHelper.cs
// Released under the GNU GLP v3.0

using Mac_EFI_Toolkit.WIN32;
using System;
using System.Windows.Forms;

namespace Mac_EFI_Toolkit.UI
{
    public static class BlurHelper
    {
        internal static void ApplyBlur(Form form)
        {
            NativeMethods.DWM_BLURBEHIND dwmBlurBehind = new NativeMethods.DWM_BLURBEHIND
            {
                dwFlags = NativeMethods.DwmBlurBehindFlags.DWM_BB_ENABLE,
                fEnable = true,
                hRgnBlur = IntPtr.Zero
            };

            NativeMethods.DwmEnableBlurBehindWindow(form.Handle, ref dwmBlurBehind);

            form.AllowTransparency = true;
            form.BackColor = System.Drawing.Color.Green;
            form.TransparencyKey = System.Drawing.Color.Green;

            if (Settings.ReadBool(SettingsBoolType.UseAccentColor))
            {
                form.BackColor = AccentColorHelper.GetSystemAccentColor();
                return;
            }

            form.BackColor = Colours.ClrAppBorderDefault;
        }

        internal static void RemoveBlur(Form form)
        {
            NativeMethods.DWM_BLURBEHIND dwmBlurBehind = new NativeMethods.DWM_BLURBEHIND
            {
                dwFlags = NativeMethods.DwmBlurBehindFlags.DWM_BB_ENABLE,
                fEnable = false
            };

            NativeMethods.DwmEnableBlurBehindWindow(form.Handle, ref dwmBlurBehind);

            form.AllowTransparency = false;
        }
    }
}