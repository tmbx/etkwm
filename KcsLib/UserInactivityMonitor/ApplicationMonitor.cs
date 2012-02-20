// ApplicationMonitor Class
// Revision 1 (2004-12-22)
// Copyright (C) 2004 Dennis Dietrich
//
// Released unter the BSD License
// http://www.opensource.org/licenses/bsd-license.php

using System;
using System.Windows.Forms;

namespace UserInactivityMonitoring
{
	/// <summary>
	/// Class for monitoring user inactivity without plattform
	/// invokes by analyzing windows messages
	/// </summary>
	public class ApplicationMonitor : MonitorBase, IMessageFilter
	{
		#region Private Fields

		private bool disposed = false;

		#endregion Private Fields

		#region Constructors

		/// <summary>
		/// Creates a new instance of <see cref="ApplicationMonitor"/>
		/// </summary>
		public ApplicationMonitor() : base()
		{
			Application.AddMessageFilter(this);
		}

		#endregion Constructors

		#region Public Methods

		/// <summary>
		/// Processes windows messages to detect user activity
		/// </summary>
		/// <param name="m">
		/// <see cref="Message"/> object with the current message
		/// </param>
		/// <returns>
		/// Always returns false to allow further processing of all messages
		/// </returns>
		public bool PreFilterMessage(ref Message m)
		{
			if (MonitorKeyboardEvents)
				if (m.Msg == (int)Win32Message.WM_KEYDOWN ||
					m.Msg == (int)Win32Message.WM_KEYUP)
					ResetBase();
			if (MonitorMouseEvents)
				if (m.Msg == (int)Win32Message.WM_LBUTTONDOWN ||
					m.Msg == (int)Win32Message.WM_LBUTTONUP   ||
					m.Msg == (int)Win32Message.WM_MBUTTONDOWN ||
					m.Msg == (int)Win32Message.WM_MBUTTONUP   ||
					m.Msg == (int)Win32Message.WM_RBUTTONDOWN ||
					m.Msg == (int)Win32Message.WM_RBUTTONUP   ||
					m.Msg == (int)Win32Message.WM_MOUSEMOVE   ||
					m.Msg == (int)Win32Message.WM_MOUSEWHEEL)
					ResetBase();
			return false;
		}

		#endregion Public Methods

		#region Protected Methods

		/// <summary>
		/// Actual deconstructor in accordance with the dispose pattern
		/// </summary>
		/// <param name="disposing">
		/// True if managed and unmanaged resources will be freed
		/// (otherwise only unmanaged resources are handled)
		/// </param>
		protected override void Dispose(bool disposing)
		{
			if (!disposed)
			{
				disposed = true;
				if (disposing)
				{
					Application.RemoveMessageFilter(this);
				}
			}
			base.Dispose(disposing);
		}

		#endregion

		#region Private Methods

		private void ResetBase()
		{
			if (TimeElapsed && !ReactivatedRaised)
				OnReactivated(new EventArgs());
			base.Reset();
		}

		#endregion Private Methods
	}
}