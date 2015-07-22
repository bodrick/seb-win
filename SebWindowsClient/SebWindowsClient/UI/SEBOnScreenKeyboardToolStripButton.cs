﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SebWindowsClient.ProcessUtils;
using SebWindowsClient.Properties;
using SebWindowsClient.XULRunnerCommunication;
using SebWindowsClient.ConfigurationUtils;


namespace SebWindowsClient.UI
{
    public class SEBOnScreenKeyboardToolStripButton : SEBToolStripButton
    {
        public SEBOnScreenKeyboardToolStripButton()
        {
            InitializeComponent();
            this.Alignment = ToolStripItemAlignment.Right;
        }

        protected override void OnClick(EventArgs e)
        {
            if (TapTipHandler.IsKeyboardVisible())
            {
                TapTipHandler.HideKeyboard();
            }
            else
            {
                TapTipHandler.ShowKeyboard();
            }
        }

        private void InitializeComponent()
        {
            // 
            // SEBOnScreenKeyboardToolStripButton
            // 
            this.ToolTipText = SEBUIStrings.toolTipOnScreenKeyboard;
            base.Image = (Bitmap)Resources.ResourceManager.GetObject("keyboard");
        }
    }

    public static class TapTipHandler
    {
        public delegate void KeyboardStateChangedEventHandler(bool shown);
        public static event KeyboardStateChangedEventHandler OnKeyboardStateChanged;

        public static void RegisterXulRunnerEvents()
        {
            SEBXULRunnerWebSocketServer.OnXulRunnerTextFocus += (x,y) => ShowKeyboard();
            SEBXULRunnerWebSocketServer.OnXulRunnerTextBlur += (x, y) => HideKeyboard();
        }

        public static void ShowKeyboard()
        {
            
                try
                {
                    if (
                        (bool)
                            SEBSettings.valueForDictionaryKey(SEBSettings.settingsCurrent, SEBSettings.KeyTouchOptimized))
                    {
                        if (!SEBWindowHandler.AllowedExecutables.Contains("tabtip.exe"))
                            SEBWindowHandler.AllowedExecutables.Add("tabtip.exe");

                        if (!IsKeyboardVisible())
                        {
                            //TODO: Use Environment Variable here, but with SEB running as 32bit it always takes X86
                            //string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                            string programFiles = @"C:\Program Files";
                            string inkDir = @"Common Files\Microsoft Shared\ink";
                            string onScreenKeyboardPath = Path.Combine(programFiles, inkDir, "TabTip.exe");
                            Process.Start(onScreenKeyboardPath);
                            if (OnKeyboardStateChanged != null)
                            {
                                var t = new System.Timers.Timer {Interval = 500};
                                t.Elapsed += (sender, args) =>
                                {
                                    if (!IsKeyboardVisible())
                                    {
                                        OnKeyboardStateChanged(false);
                                        t.Stop();
                                    }
                                };
                                t.Start();
                            }
                        }
                        OnKeyboardStateChanged(true);
                    }
                }
                catch
                { }
        }

        public static void HideKeyboard()
        {
            if (IsKeyboardVisible())
            {
                uint WM_SYSCOMMAND = 274;
                IntPtr SC_CLOSE = new IntPtr(61536);
                PostMessage(GetKeyboardWindowHandle(), WM_SYSCOMMAND, SC_CLOSE, (IntPtr)0);   
            }

            if (OnKeyboardStateChanged != null)
            {
                OnKeyboardStateChanged(false);
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// The window is disabled. See http://msdn.microsoft.com/en-gb/library/windows/desktop/ms632600(v=vs.85).aspx.
        /// </summary>
        public const UInt32 WS_DISABLED = 0x8000000;

        /// <summary>
        /// Specifies we wish to retrieve window styles.
        /// </summary>
        public const int GWL_STYLE = -16;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(String sClassName, String sAppName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern UInt32 GetWindowLong(IntPtr hWnd, int nIndex);


        /// <summary>
        /// Gets the window handler for the virtual keyboard.
        /// </summary>
        /// <returns>The handle.</returns>
        public static IntPtr GetKeyboardWindowHandle()
        {
            return FindWindow("IPTip_Main_Window", null);
        }

        /// <summary>
        /// Checks to see if the virtual keyboard is visible.
        /// </summary>
        /// <returns>True if visible.</returns>
        public static bool IsKeyboardVisible()
        {
            IntPtr keyboardHandle = GetKeyboardWindowHandle();

            bool visible = false;

            if (keyboardHandle != IntPtr.Zero)
            {
                keyboardHandle.MaximizeWindow();
                UInt32 style = GetWindowLong(keyboardHandle, GWL_STYLE);
                visible = ((style & WS_DISABLED) != WS_DISABLED);
            }

            return visible;
        }

        public static bool IsKeyboardDocked()
        {
            int docked = 1;

            try
            {
                //HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7\EdgeTargetDockedState -> 0 = floating, 1 = docked
                docked = (int)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7\", "EdgeTargetDockedState", 1);
            }
            catch { }

            return docked == 1;

        }
    }
}
