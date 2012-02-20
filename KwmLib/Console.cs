using kcslib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace kwmlib
{
    public partial class ConsoleWindow : Form, KwmLogHandler
    {
        /// <summary>
        /// Fired when the forms close.
        /// </summary>
        public event EventHandler<EventArgs> OnConsoleClosing;

        public ConsoleWindow()
        {
            InitializeComponent();

            ColumnHeader col = new ColumnHeader();
            col.Name = "Sev";
            col.Text = "Sev";
            col.Width = 20;
            lvMessages.Columns.Add(col);

            col = new ColumnHeader();
            col.Name = "Timestamp";
            col.Text = "Timestamp";
            col.Width = 125;
            lvMessages.Columns.Add(col);

            col = new ColumnHeader();
            col.Name = "Caller";
            col.Text = "Caller";
            col.Width = 176;
            lvMessages.Columns.Add(col);

            col = new ColumnHeader();
            col.Name = "Line";
            col.Text = "Line";
            col.Width = 60;
            lvMessages.Columns.Add(col);

            col = new ColumnHeader();
            col.Name = "Message";
            col.Text = "Message";
            col.Width = 384;
            lvMessages.Columns.Add(col);

            foreach (KwmLogEventArgs evt in KwmLogger.GetBufferedEventList())
                AddEvent(evt, false);
            KwmLogger.RegisterLogHandler(this);
        }

        public void HandleOnLogEvent(object _sender, KwmLogEventArgs args)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<KwmLogEventArgs>(HandleOnLogEvent), new object[] { _sender, args });
                return;
            }

            // We might be called after the UI has been disposed. Let the
            // event go.
            if (this.IsDisposed || this.Disposing) return;

            // Determine whether we want to scroll.
            bool scrollFlag = (this.WindowState != FormWindowState.Minimized &&
                               lvMessages.Items.Count != 0 &&
                               lvMessages.ClientRectangle.Contains(
                                 lvMessages.Items[lvMessages.Items.Count - 1].SubItems[1].Bounds));

            // Add the event.
            AddEvent(args, scrollFlag);
        }

        /// <summary>
        /// Adds a message to the listview. If scrollToBotton
        /// is set to true, calls EnsureVisible so that the last
        /// item is scrolled to.
        /// </summary>
        private void AddEvent(KwmLogEventArgs evt, bool scrollToBottom)
        {
            // Add this message to listview
            ListViewItem itm = new ListViewItem(evt.Severity.ToString());
            itm.Name = lvMessages.Columns[0].Name;
            itm.Tag = evt.CallStack;

            switch (evt.Severity)
            {
                case 1:
                    itm.BackColor = Color.Yellow;
                    break;
                case 2:
                    itm.BackColor = Color.Red;
                    break;
                default:
                    itm.BackColor = Color.White;
                    break;
            }

            ListViewItem.ListViewSubItem sub;

            sub = new ListViewItem.ListViewSubItem();
            sub.Name = lvMessages.Columns[1].Name;
            sub.Text = evt.Timestamp.ToString("hh:mm:ss.fff");
            itm.SubItems.Add(sub);

            sub = new ListViewItem.ListViewSubItem();
            sub.Name = lvMessages.Columns[2].Name;
            sub.Text = evt.Caller;
            itm.SubItems.Add(sub);

            sub = new ListViewItem.ListViewSubItem();
            sub.Name = lvMessages.Columns[3].Name;
            sub.Text = evt.Line;
            itm.SubItems.Add(sub);

            sub = new ListViewItem.ListViewSubItem();
            sub.Name = lvMessages.Columns[4].Name;
            sub.Text = evt.Message;
            itm.SubItems.Add(sub);            

            lvMessages.Items.Add(itm);

            if (scrollToBottom) CorrectUI(false);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                KBase.HandleException(ex);
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                KwmLogger.ClearBufferedEventList();
                lvMessages.Items.Clear();
            }
            catch (Exception ex)
            {
                KBase.HandleException(ex);
            }
        }

        private void ConsoleWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                KwmLogger.UnregisterLogHandler(this);
                if (OnConsoleClosing != null) OnConsoleClosing(sender, e);
            }
            catch (Exception ex)
            {
                KBase.HandleException(ex);
            }
        }

        private void ConsoleWindow_Shown(object sender, EventArgs e)
        {
            try
            {
                CorrectUI(true);
            }
            catch (Exception ex)
            {
                KBase.HandleException(ex);
            }
        }

        /// <summary>
        /// Adjusts column width, scroll to last item. If widthFlag is true, the
        /// width of the message box will also be adjusted.
        /// </summary>
        private void CorrectUI(bool widthFlag)
        {
            if (widthFlag) lvMessages.Columns["Message"].Width = -2;
            if(lvMessages.Items.Count > 1)
                lvMessages.EnsureVisible(lvMessages.Items.Count - 1);
        }

        private void lvMessages_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                ConsoleMessage msg = new ConsoleMessage(lvMessages.SelectedItems[0]);
                msg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                KBase.HandleException(ex);
            }
        }
    }
}