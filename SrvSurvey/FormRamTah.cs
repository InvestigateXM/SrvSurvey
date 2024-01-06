﻿using SrvSurvey.game;
using System.ComponentModel;
using System.Windows.Forms.VisualStyles;

namespace SrvSurvey
{
    public partial class FormRamTah : Form
    {
        public static FormRamTah? activeForm;

        public static void show()
        {
            if (activeForm == null)
                FormRamTah.activeForm = new FormRamTah();

            Util.showForm(FormRamTah.activeForm);
        }

        private FormRamTah()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.setCurrentObelisk(null);

            // can we fit in our last location
            Util.useLastLocation(this, Game.settings.formRamTah);

            txtRuinsMissionActive.Text = this.cmdr?.decodeTheRuinsMissionActive.ToString() ?? "Unknown";
            if (this.cmdr?.decodeTheRuinsMissionActive == TahMissionStatus.Active) txtRuinsMissionActive.BackColor = Color.Lime;

            txtLogsMissionActive.Text = this.cmdr?.decodeTheLogsMissionActive.ToString() ?? "Unknown";
            if (this.cmdr?.decodeTheLogsMissionActive == TahMissionStatus.Active) txtLogsMissionActive.BackColor = Color.Lime;

            this.prepRuinsRows();
            this.prepLogCheckboxes();

            // auto select 2nd tab if only the 2nd mission is active
            if (this.cmdr?.decodeTheRuinsMissionActive != TahMissionStatus.Active && this.cmdr?.decodeTheLogsMissionActive == TahMissionStatus.Active)
                tabControl1.SelectedIndex = 1;
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);

            var rect = new Rectangle(this.Location, this.Size);
            if (Game.settings.formRamTah != rect)
            {
                Game.settings.formRamTah = rect;
                Game.settings.Save();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            FormRamTah.activeForm = null;
        }

        private CommanderSettings? cmdr { get => Game.activeGame?.cmdr; }

        private void prepLogCheckboxes()
        {

            foreach (Control ctrl in tabPage2.Controls)
            {
                var checkbox = ctrl as System.Windows.Forms.CheckBox;
                if (checkbox == null) continue;

                var idx = int.Parse(checkbox.Name.Substring(8));
                checkbox.Checked = cmdr?.decodeTheLogs.Contains($"#{idx}") == true;
                checkbox.BackColor = checkbox.Checked ? Color.Lime : Color.Transparent;
            }
        }

        private void checkLog_CheckedChanged(object sender, EventArgs e)
        {
            if (this.cmdr == null || Game.activeGame?.isRunning != true) return;

            var checkbox = sender as System.Windows.Forms.CheckBox;
            if (checkbox != null)
            {
                checkbox.BackColor = checkbox.Checked ? Color.Lime : Color.Transparent;

                var idx = int.Parse(checkbox.Name.Substring(8));
                if (checkbox.Checked)
                    this.cmdr.decodeTheLogs.Add($"#{idx}");
                else
                    this.cmdr.decodeTheLogs.Remove($"#{idx}");

                // update header labels to match
                lblThargoids.BackColor = checkLog1.Checked && checkLog2.Checked && checkLog3.Checked && checkLog4.Checked && checkLog5.Checked
                    ? Color.Lime : Color.Transparent;

                lblCivilWar.BackColor = checkLog6.Checked && checkLog7.Checked && checkLog8.Checked && checkLog9.Checked && checkLog10.Checked
                    ? Color.Lime : Color.Transparent;

                lblTechnology.BackColor = checkLog11.Checked && checkLog12.Checked && checkLog13.Checked && checkLog14.Checked && checkLog15.Checked
                    && checkLog16.Checked && checkLog17.Checked && checkLog18.Checked && checkLog19.Checked && checkLog20.Checked
                    && checkLog21.Checked && checkLog22.Checked && checkLog23.Checked
                    ? Color.Lime : Color.Transparent;

                lblLanguage.BackColor = checkLog24.Checked ? Color.Lime : Color.Transparent;

                lblBodyProtectorate.BackColor = checkLog25.Checked && checkLog26.Checked && checkLog27.Checked && checkLog28.Checked
                    ? Color.Lime : Color.Transparent;
            }
        }

        private void prepRuinsRows()
        {
            // inject 21 rows with cmdr's state
            listRuins.Items.Clear();
            for (int n = 1; n <= 21; n++)
            {
                var subItems = new ListViewItem.ListViewSubItem[]
                {
                    // ordering here needs to manually match columns
                    new ListViewItem.ListViewSubItem { Text = n.ToString() },
                    new ListViewItem.ListViewSubItem { Name = $"B{n}" },
                    new ListViewItem.ListViewSubItem { Name = $"C{n}" },
                    new ListViewItem.ListViewSubItem { Name = $"H{n}" },
                    new ListViewItem.ListViewSubItem { Name = $"L{n}" },
                    new ListViewItem.ListViewSubItem { Name = $"T{n}" },
                };

                if (n > 19) { subItems[1].Name = null; } // Biology ends at 19
                if (n > 20) { subItems[2].Name = null; subItems[5].Name = null; } // Biology + Technology end at 20

                var item = new ListViewItem(subItems, 0);
                listRuins.Items.Add(item);
            }
        }

        private void listRuins_MouseClick(object sender, MouseEventArgs e)
        {
            if (this.cmdr == null || Game.activeGame?.isRunning != true) return;

            var row = listRuins.GetItemAt(e.X, e.Y);
            if (row != null)
            {
                var subItem = row.GetSubItemAt(e.X, e.Y);
                if (!string.IsNullOrEmpty(subItem.Name))
                {
                    if (cmdr.decodeTheRuins.Contains(subItem.Name))
                        cmdr.decodeTheRuins.Remove(subItem.Name);
                    else
                        cmdr.decodeTheRuins.Add(subItem.Name);

                    listRuins.Invalidate(true);
                    //listRuins.Invalidate(subItem.Bounds);
                    //listRuins.Invalidate(new Rectangle(subItem.Bounds.X, 0, subItem.Bounds.Width, 24));
                }
            }

        }

        private Point getBoundsCenter(Rectangle r, Size sz)
        {
            var pt = new Point(
                (r.Width / 2) - (sz.Width / 2),
                (r.Height / 2) - (sz.Height / 2)
            );

            return pt;
        }

        private void listView1_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            var name = e.Item?.SubItems[e.ColumnIndex].Name;
            // background + edges;
            if (!string.IsNullOrEmpty(name) && this.cmdr?.decodeTheRuins.Contains(name) == true)
            {
                e.Graphics.FillRectangle(Brushes.Lime, e.Bounds);
            }
            else if (e.Item?.Selected == true)
            {
                e.Graphics.FillRectangle(SystemBrushes.HotTrack, e.Bounds);
            }
            else if (e.ItemIndex % 2 == 1)
            {
                e.Graphics.FillRectangle(SystemBrushes.ControlLight, e.Bounds);
            }
            else
            {
                e.DrawBackground();
            }

            if (Game.activeGame?.systemSite?.currentObelisk?.msg == name)
            {
                // draw some obvious highlight when the current obelisk yields this log
                var r = Rectangle.Inflate(e.Bounds, -2, -2);
                e.Graphics.DrawRectangle(GameColors.penBlack3Dotted, r);
            }

            if (e.Item?.Selected == true) // .ItemState == ListViewItemStates.Selected)
                e.DrawFocusRectangle(e.Bounds);

            e.Graphics.DrawRectangle(SystemPens.ControlDark, e.Bounds);

            if (e.ColumnIndex == 0)
            {
                TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, this.Font, e.Bounds, SystemColors.WindowText, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
            else
            {
                if (!string.IsNullOrEmpty(name))
                {
                    var checkState = this.cmdr?.decodeTheRuins.Contains(name) == true ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
                    var pt = this.getBoundsCenter(e.Bounds, CheckBoxRenderer.GetGlyphSize(e.Graphics, checkState));
                    pt.Offset(e.Bounds.Location);
                    CheckBoxRenderer.DrawCheckBox(e.Graphics, pt, checkState);
                }
            }
        }

        private static Dictionary<char, int> completedCounts = new Dictionary<char, int>()
        {
            { 'B', 19 },
            { 'C', 20 },
            { 'L', 21 },
            { 'H', 21 },
            { 'T', 20 },
        };

        private void listView1_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            if (this.cmdr == null) return;

            var txt = listRuins.Columns[e.ColumnIndex].Text;
            var countCompleted = this.cmdr.decodeTheRuins.Count(_ => _[0] == txt[0]);
            if (completedCounts.ContainsKey(txt[0]) && countCompleted >= completedCounts[txt[0]])
                e.Graphics.FillRectangle(Brushes.Lime, e.Bounds);
            else
                e.DrawBackground();

            var flags = e.ColumnIndex == 0
                ? TextFormatFlags.Right | TextFormatFlags.VerticalCenter
                : TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;

            TextRenderer.DrawText(e.Graphics, txt, this.Font, e.Bounds, SystemColors.WindowText, flags);
        }

        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            this.cmdr?.Save();
            Game.activeGame?.systemSite?.ramTahRecalc();
        }

        private void btnQuit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Util.openLink("https://canonn.science/codex/ram-tahs-mission/");
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Util.openLink("https://canonn.science/codex/ram-tah-decrypting-the-guardian-logs/");
        }

        private void btnResetRuins_Click(object sender, EventArgs e)
        {
            if (this.cmdr == null) return;

            var rslt = MessageBox.Show(
                this,
                $"Are you sure you want to clear your progress of Decode the Ancient Ruins, {this.cmdr.decodeTheRuins.Count} logs?",
                "SrvSurvey",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (rslt == DialogResult.Yes)
            {
                this.cmdr.decodeTheRuins.Clear();
                this.prepRuinsRows();
            }
        }

        private void btnResetLogs_Click(object sender, EventArgs e)
        {
            if (this.cmdr == null) return;

            var rslt = MessageBox.Show(
                this,
                $"Are you sure you want to clear your progress of Decode the Ancient Logs, {this.cmdr.decodeTheLogs.Count} logs?",
                "SrvSurvey",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (rslt == DialogResult.Yes)
            {
                this.cmdr.decodeTheLogs.Clear();
                this.prepLogCheckboxes();
            }
        }

        internal void setCurrentObelisk(ActiveObelisk? obelisk)
        {
            if (obelisk == null)
            {
                txtObelisk.Enabled = lblObelisk.Enabled = btnToggleObelisk.Enabled = false;
                txtObelisk.Text = null;
            }
            else
            {
                txtObelisk.Enabled = lblObelisk.Enabled = btnToggleObelisk.Enabled = true;
                txtObelisk.Text = $"{obelisk.name}: {obelisk.items[0]}"
                    + (obelisk.items.Count > 1 ? $" + {obelisk.items[1]}" : null)
                    + $" for {obelisk.msgDisplay}";
            }

            listRuins.Invalidate();
        }

        private void btnToggleObelisk_Click(object sender, EventArgs e)
        {
            // toggle it!
            var siteData = Game.activeGame?.systemSite;
            if (siteData == null || siteData.currentObelisk == null) return;

            siteData.toggleObeliskScanned();
        }
    }
}