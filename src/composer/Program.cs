﻿//
//  WinCompose — a compose key for Windows — http://wincompose.info/
//
//  Copyright © 2013—2015 Sam Hocevar <sam@hocevar.net>
//              2014—2015 Benjamin Litzelmann
//
//  This program is free software. It comes without any warranty, to
//  the extent permitted by applicable law. You can redistribute it
//  and/or modify it under the terms of the Do What the Fuck You Want
//  to Public License, Version 2, as published by the WTFPL Task Force.
//  See http://www.wtfpl.net/ for more details.
//

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using System.Xml;
using System.Xml.XPath;
using WinForms = System.Windows.Forms;

namespace WinCompose
{
    static class Program
    {
        private static WinForms.NotifyIcon m_tray_icon;
        private static WinForms.MenuItem m_disable_item;
        private static RemoteControl m_control;
        private static SequenceWindow m_sequencewindow;
        private static SettingsWindow m_optionswindow;
        private static DebugWindow m_debug_window;

        [STAThread]
        static void Main()
        {
            Composer.Init();
            Settings.LoadConfig();
            Settings.LoadSequences();
            KeyboardHook.Init();

            Settings.StartWatchConfigFile();

            try
            {
                WinForms.Application.EnableVisualStyles();
                WinForms.Application.SetCompatibleTextRenderingDefault(false);

                m_control = new RemoteControl();
                m_control.DisableEvent += OnDisableEvent;
                m_control.ExitEvent += OnExitEvent;
                m_control.TriggerDisableEvent();

                m_tray_icon = new WinForms.NotifyIcon
                {
                    Visible = true,
                    Icon = Properties.Resources.IconNormal,
                    ContextMenu = new WinForms.ContextMenu(new[]
                    {
                        new WinForms.MenuItem("WinCompose")
                        {
                            Enabled = false
                        },
                        new WinForms.MenuItem("-"),
                        new WinForms.MenuItem(i18n.Text.ShowSequences, ShowSequencesClicked),
                        new WinForms.MenuItem(i18n.Text.ShowOptions, ShowOptionsClicked),
                        /* Keep a reference on this entry */ m_disable_item =
                        new WinForms.MenuItem(i18n.Text.Disable, DisableClicked),
                        new WinForms.MenuItem(i18n.Text.About, AboutClicked),
#if DEBUG
                        new WinForms.MenuItem("Debug Logs…", DebugClicked),
#endif
                        new WinForms.MenuItem("-"),
                        new WinForms.MenuItem(i18n.Text.Restart, RestartClicked),
                        new WinForms.MenuItem(i18n.Text.Exit, OnExitEvent),
                    })
                };
                m_tray_icon.DoubleClick += NotifyiconDoubleclicked;

                Composer.Changed += ComposerStateChanged;

                WinForms.Application.Run();
                m_tray_icon.Dispose();
            }
            finally
            {
                Composer.Changed -= ComposerStateChanged;
                m_control.DisableEvent -= OnDisableEvent;
                m_control.ExitEvent -= OnExitEvent;

                Settings.StopWatchConfigFile();
                KeyboardHook.Fini();
                Settings.SaveConfig();
                Composer.Fini();
            }
        }

        private static void NotifyiconDoubleclicked(object sender, EventArgs e)
        {
            if (m_sequencewindow == null)
            {
                m_sequencewindow = new SequenceWindow();
                ElementHost.EnableModelessKeyboardInterop(m_sequencewindow);
            }

            if (m_sequencewindow.IsVisible)
            {
                m_sequencewindow.Hide();
            }
            else
            {
                m_sequencewindow.Show();
            }
        }

        private static void ComposerStateChanged(object sender, EventArgs e)
        {
            m_tray_icon.Icon = Composer.IsDisabled()  ? Properties.Resources.IconDisabled
                             : Composer.IsComposing() ? Properties.Resources.IconActive
                                                      : Properties.Resources.IconNormal;
            m_tray_icon.Text = Composer.IsDisabled()
                              ? i18n.Text.DisabledToolTip
                              : String.Format(i18n.Text.TrayToolTip,
                                        Settings.ComposeKey.Value.FriendlyName,
                                        Settings.GetSequenceCount(),
                                        Program.Version);

            m_disable_item.Checked = Composer.IsDisabled();
        }

        private static void ShowSequencesClicked(object sender, EventArgs e)
        {
            if (m_sequencewindow == null)
            {
                m_sequencewindow = new SequenceWindow();
                ElementHost.EnableModelessKeyboardInterop(m_sequencewindow);
            }
            m_sequencewindow.Show();
        }

        private static void ShowOptionsClicked(object sender, EventArgs e)
        {
            if (m_optionswindow == null)
            {
                m_optionswindow = new SettingsWindow();
                ElementHost.EnableModelessKeyboardInterop(m_optionswindow);
            }
            m_optionswindow.Show();
        }

        private static void DisableClicked(object sender, EventArgs e)
        {
            if (Composer.IsDisabled())
                m_control.TriggerDisableEvent();

            Composer.ToggleDisabled();
        }

        private static void AboutClicked(object sender, EventArgs e)
        {
            var about_box = new AboutBox();
            about_box.ShowDialog();
        }

        private static void DebugClicked(object sender, EventArgs e)
        {
            if (m_debug_window == null)
                m_debug_window = new DebugWindow();
            m_debug_window.Show();
        }

        private static void RestartClicked(object sender, EventArgs e)
        {
            WinForms.Application.Restart();
            Environment.Exit(0);
        }

        private static void OnDisableEvent(object sender, EventArgs e)
        {
            if (!Composer.IsDisabled())
                Composer.ToggleDisabled();
        }

        private static void OnExitEvent(object sender, EventArgs e)
        {
            WinForms.Application.Exit();
        }

        public static string Version
        {
            get
            {
                if (m_version == null)
                {
                    XmlDocument doc = new XmlDocument();
                    Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinCompose.build.xml");
                    doc.Load(stream);
                    XmlNamespaceManager mgr = new XmlNamespaceManager(doc.NameTable);
                    mgr.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003");

                    m_version = doc.DocumentElement.SelectSingleNode("//ns:Project/ns:PropertyGroup/ns:ApplicationVersion", mgr).InnerText;
                }

                return m_version;
            }
        }

        private static string m_version;
    }
}

