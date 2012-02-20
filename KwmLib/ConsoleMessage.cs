using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace kwmlib
{
    public partial class ConsoleMessage : Form
    {
        public ConsoleMessage()
        {
            InitializeComponent();
        }

        public ConsoleMessage(ListViewItem itm)
        {
            InitializeComponent();

            String newLine = "";

            for (int i = 0; i < itm.SubItems.Count; i++)
            {
                txtMessage.Text += newLine + itm.SubItems[i].Name + " : " + itm.SubItems[i].Text;
                newLine = Environment.NewLine;
            }

            String callStack = (String)itm.Tag;

            if (callStack != "")
            {
                txtMessage.Text += Environment.NewLine + Environment.NewLine + "Stack trace:" + 
                                   Environment.NewLine + Environment.NewLine + callStack +
                                   Environment.NewLine;
            }
        }
    }
}