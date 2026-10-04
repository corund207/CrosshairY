using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.UI.Controls;

namespace CrosshairY.UI.Dialogs
{
    /// <summary>"Update available": release notes, then download → verify → swap → restart.</summary>
    public class UpdateDialog : DarkDialog
    {
        readonly UpdateInfo info;
        readonly WrapLabel status;
        readonly FlatButton updateBtn, laterBtn, skipBtn;

        public UpdateDialog(UpdateInfo u) : base(L.T("Update available"), 560)
        {
            info = u;
            Content.Controls.Add(new WrapLabel(string.Format(L.T("CrosshairY {0} is available — you have {1}. Your crosshairs, profiles and settings are kept."), u.Version, Program.Version), Theme.Body, Theme.TextDim));
            var notes = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface2, ForeColor = Theme.Text, Font = Theme.Small, Height = Theme.S(220),
                Text = Plain(u.Notes)
            };
            notes.TabStop = false;
            Content.Controls.Add(notes);
            Shown += (s, e) => { notes.SelectionLength = 0; updateBtn?.Focus(); };
            status = new WrapLabel(u.Sha256 != null ? L.T("The download is checked against the release's SHA-256 before anything is replaced.") : "", Theme.Small, Theme.TextMute);
            Content.Controls.Add(status);
            skipBtn = AddButton(L.T("Skip this version"), ButtonKind.Ghost, () =>
            {
                AppController.I.State.Settings.SkippedVersion = u.Version;
                AppController.I.State.MarkSettingsChanged();
                Close();
            });
            laterBtn = AddButton(L.T("Later"), ButtonKind.Secondary, Close);
            updateBtn = AddButton(L.T("Update now"), ButtonKind.Primary, DoUpdate);
        }

        async void DoUpdate()
        {
            updateBtn.Enabled = laterBtn.Enabled = skipBtn.Enabled = false;
            status.ForeColor = Theme.TextDim;
            try
            {
                string file = await Updater.DownloadAsync(info, p => BeginInvoke(new Action(() => { if (!IsDisposed) status.Text = string.Format(L.T("Downloading… {0}%"), (int)(p * 100)); })));
                status.Text = L.T("Installing…");
                Updater.Install(file);
                DialogResult = DialogResult.OK;
                Close();
                MainForm.Instance.ExitApp();
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                status.ForeColor = Theme.Danger;
                status.Text = L.T("Update failed: ") + ex.Message + "\n" + L.T("You can download it from the releases page instead.");
                updateBtn.Text = L.T("Open releases page");
                updateBtn.Enabled = laterBtn.Enabled = true;
                updateBtn.Click += (s, e) => { try { System.Diagnostics.Process.Start(info.PageUrl); } catch { } };
            }
        }

        /// <summary>Release notes are Markdown; show them as readable plain text.</summary>
        static string Plain(string md)
        {
            if (string.IsNullOrWhiteSpace(md)) return "";
            string s = Regex.Replace(md, @"<[^>]+>", "");                       // html (images)
            s = Regex.Replace(s, @"\*\*([^*]+)\*\*", "$1");
            s = Regex.Replace(s, @"`([^`]+)`", "$1");
            s = Regex.Replace(s, @"^#+\s*", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^\s*[-*]\s+", "•  ", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");
            return s.Replace("\r\n", "\n").Replace("\n", "\r\n").Trim();
        }
    }
}
