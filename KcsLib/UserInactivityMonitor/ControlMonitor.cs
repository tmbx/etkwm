// ControlMonitor Class
// Revision 2 (2004-12-22)
// Copyright (C) 2004 Dennis Dietrich
//
// Released unter the BSD License
// http://www.opensource.org/licenses/bsd-license.php

using System;
using System.Windows.Forms;

namespace UserInactivityMonitoring
{
	/// <summary>
	/// Class for monitoring user inactivity without any plattform invokes
	/// </summary>
	public class ControlMonitor : MonitorBase
	{
		#region Private Fields

		private bool disposed = false;

		private Control targetControl = null;

		#endregion Private Fields

		#region Public Properties

		/// <summary>
		/// Specifies if the instances monitors mouse events
		/// </summary>
		public override bool MonitorMouseEvents
		{
			get
			{
				return base.MonitorMouseEvents;
			}
			set
			{
				if (disposed)
					throw new ObjectDisposedException("Object has already been disposed");

				if (base.MonitorMouseEvents != value)
				{
					if (value)
						RegisterMouseEvents(targetControl);
					else
						UnRegisterMouseEvents(targetControl);
					base.MonitorMouseEvents = value;
				}
			}
		}

		/// <summary>
		/// Specifies if the instances monitors keyboard events
		/// </summary>
		public override bool MonitorKeyboardEvents
		{
			get
			{
				return base.MonitorKeyboardEvents;
			}
			set
			{
				if (disposed)
					throw new ObjectDisposedException("Object has already been disposed");
				
				if (base.MonitorKeyboardEvents != value)
				{
					if (value)
						RegisterKeyboardEvents(targetControl);
					else
						UnRegisterKeyboardEvents(targetControl);
					base.MonitorKeyboardEvents = value;
				}
			}
		}

		#endregion Public Properties

		#region Constructors

		/// <summary>
		/// Creates a new instance of <see cref="ControlMonitor"/>
		/// </summary>
		/// <param name="target">
		/// The control (including all child controls) to be monitored
		/// </param>
		public ControlMonitor(Control target) : base()
		{
			if (target == null)
				throw new ArgumentException("Parameter target must not be null");
			targetControl = target;
			ControlAdded(this, new ControlEventArgs(targetControl));
			if (MonitorKeyboardEvents)
				RegisterKeyboardEvents(targetControl);
			if (MonitorMouseEvents)
				RegisterMouseEvents(targetControl);
		}

		#endregion Constructors

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
					ControlRemoved(this, new ControlEventArgs(targetControl));
					if (MonitorKeyboardEvents)
						UnRegisterKeyboardEvents(targetControl);
					if (MonitorMouseEvents)
						UnRegisterMouseEvents(targetControl);
					targetControl = null;
					
				}
			}
			base.Dispose(disposing);
		}

		#endregion

		#region Private Methods

		private void RegisterKeyboardEvents(Control c)
		{
			c.KeyDown += new KeyEventHandler(KeyboardEventOccured);
			c.KeyUp   += new KeyEventHandler(KeyboardEventOccured);
			foreach (Control item in c.Controls)
				RegisterKeyboardEvents(item);
		}

		private void UnRegisterKeyboardEvents(Control c)
		{
			c.KeyDown -= new KeyEventHandler(KeyboardEventOccured);
			c.KeyUp   -= new KeyEventHandler(KeyboardEventOccured);
			foreach (Control item in c.Controls)
				UnRegisterKeyboardEvents(item);
		}

		private void RegisterMouseEvents(Control c)
		{
			c.MouseDown  += new MouseEventHandler(MouseEventOccured);
			c.MouseUp    += new MouseEventHandler(MouseEventOccured);
			c.MouseMove  += new MouseEventHandler(MouseEventOccured);
			c.MouseWheel += new MouseEventHandler(MouseEventOccured);
			foreach (Control item in c.Controls)
				RegisterMouseEvents(item);
		}

		private void UnRegisterMouseEvents(Control c)
		{
			c.MouseDown  -= new MouseEventHandler(MouseEventOccured);
			c.MouseUp    -= new MouseEventHandler(MouseEventOccured);
			c.MouseMove  -= new MouseEventHandler(MouseEventOccured);
			c.MouseWheel -= new MouseEventHandler(MouseEventOccured);
			foreach (Control item in c.Controls)
				UnRegisterMouseEvents(item);
		}

		private void MouseEventOccured(object sender, MouseEventArgs e)
		{
			ResetBase();
		}

		private void KeyboardEventOccured(object sender, KeyEventArgs e)
		{
			ResetBase();
		}

		private void ResetBase()
		{
			if (TimeElapsed && !ReactivatedRaised)
				OnReactivated(new EventArgs());
			base.Reset();
		}

		private void ControlAdded(object sender, ControlEventArgs e)
		{
			e.Control.ControlAdded   += new ControlEventHandler(ControlAdded);
			e.Control.ControlRemoved += new ControlEventHandler(ControlRemoved);
			foreach (Control item in e.Control.Controls)
				ControlAdded(this, new ControlEventArgs(item));
		}

		private void ControlRemoved(object sender, ControlEventArgs e)
		{
			e.Control.ControlAdded   -= new ControlEventHandler(ControlAdded);
			e.Control.ControlRemoved -= new ControlEventHandler(ControlRemoved);
			foreach (Control item in e.Control.Controls)
				ControlRemoved(this, new ControlEventArgs(item));
		}

		#endregion Private Methods
	}
}
