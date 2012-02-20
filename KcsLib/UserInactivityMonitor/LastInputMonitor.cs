// LastInputMonitor Class
// Revision 1 (2004-12-21)
// Copyright (C) 2004 Dennis Dietrich
//
// Released unter the BSD License
// http://www.opensource.org/licenses/bsd-license.php

using System;
using System.ComponentModel;
using System.Timers;

namespace UserInactivityMonitoring
{
	/// <summary>
	/// Class to detect user inactivity by polling GetLastInputInfo()
	/// </summary>
	public class LastInputMonitor : IInactivityMonitor
	{
		#region Private Fields

		private double interval      = 100.0;
		private uint   lastTickCount = 0;

		private Win32LastInputInfo lastInputBuffer = new Win32LastInputInfo();
		private DateTime           lastInputDate   = DateTime.Now;

		private bool disposed = false;
		private bool enabled  = false;

		private bool monitorMouse    = true;
		private bool monitorKeyboard = true;

		private bool timeElapsed = false;
		private bool reactivated = false;

		private Timer pollingTimer = null;

		#endregion Private Fields

		#region Constructors

		/// <summary>
		/// Creates a new instance of <see cref="LastInputMonitor"/>
		/// </summary>
		public LastInputMonitor()
		{
			lastInputBuffer.cbSize = (uint)Win32LastInputInfo.SizeOf;
			pollingTimer = new Timer();
			pollingTimer.AutoReset = true;
			pollingTimer.Elapsed += new ElapsedEventHandler(TimerElapsed);
		}

		#endregion Constructors

		#region Dispose Pattern

		/// <summary>
		/// Unregisters all event handlers and disposes internal objects
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Actual deconstructor in accordance with the dispose pattern
		/// </summary>
		/// <param name="disposing">
		/// True if managed and unmanaged resources will be freed
		/// (otherwise only unmanaged resources are handled)
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				disposed = true;
				if (disposing)
				{
					Delegate[] delegateBuffer = null;

					pollingTimer.Elapsed -= new ElapsedEventHandler(TimerElapsed);
					pollingTimer.Dispose();

					delegateBuffer = Elapsed.GetInvocationList();
					foreach (ElapsedEventHandler item in delegateBuffer)
						Elapsed -= item;
					Elapsed = null;

					delegateBuffer = Reactivated.GetInvocationList();
					foreach (EventHandler item in delegateBuffer)
						Reactivated -= item;
					Reactivated = null;
				}
			}
		}

		/// <summary>
		/// Deconstructor method for use by the garbage collector
		/// </summary>
		~LastInputMonitor()
		{
			Dispose(false);
		}

		#endregion Dispose Pattern

		#region Public Events

		/// <summary>
		/// Occurs when the period of time defined by <see cref="Interval"/>
		/// has passed without any user interaction
		/// </summary>
		public event ElapsedEventHandler Elapsed;

		/// <summary>
		/// Occurs when the user continues to interact with the system after
		/// <see cref="Interval"/> has elapsed
		/// </summary>
		public event EventHandler Reactivated;

		#endregion Public Events

		#region Public Properties

		/// <summary>
		/// Period of time without user interaction after which
		/// <see cref="Elapsed"/> is raised
		/// </summary>
		public virtual double Interval
		{
			get
			{
				return interval;
			}
			set
			{
				if (value < 0)
					throw new ArgumentException("Invalid interval (value less than zero)");
				interval = value;
			}
		}

		/// <summary>
		/// Specifies if the instances raises events
		/// </summary>
		public virtual bool Enabled
		{
			get
			{
				return enabled;
			}
			set
			{
				pollingTimer.Enabled = enabled = value;
				lastInputDate = DateTime.Now;
			}
		}

		/// <summary>
		/// Specifies if the instances monitors mouse events
		/// </summary>
		public virtual bool MonitorMouseEvents
		{
			get
			{
				return monitorMouse;
			}
			set
			{
				monitorMouse = value;
			}
		}

		/// <summary>
		/// Specifies if the instances monitors keyboard events
		/// </summary>
		public virtual bool MonitorKeyboardEvents
		{
			get
			{
				return monitorKeyboard;
			}
			set
			{
				monitorKeyboard = value;
			}
		}

		/// <summary>
		/// Object to use for synchronization (the execution of
		/// event handlers will be marshalled to the thread that
		/// owns the synchronization object)
		/// </summary>
		public virtual ISynchronizeInvoke SynchronizingObject
		{
			get
			{
				return pollingTimer.SynchronizingObject;
			}
			set
			{
				pollingTimer.SynchronizingObject = value;
			}
		}

		#endregion Properties

		#region Public Methods

		/// <summary>
		/// Resets the internal timer and status information
		/// </summary>
		public virtual void Reset()
		{
			if (disposed)
				throw new ObjectDisposedException("Object has already been disposed");

			if (enabled)
			{
				lastInputDate = DateTime.Now;
				timeElapsed = false;
				reactivated = false;
			}
		}

		#endregion Public Methods

		#region Proteced Methods

		/// <summary>
		/// Method to raise the <see cref="Elapsed"/> event
		/// (performs consistency checks before raising <see cref="Elapsed"/>)
		/// </summary>
		/// <param name="e">
		/// <see cref="ElapsedEventArgs"/> object provided by the internal timer object
		/// </param>
		protected void OnElapsed(ElapsedEventArgs e)
		{
			timeElapsed = true;
			if (Elapsed != null && enabled && (monitorKeyboard || monitorMouse))
				Elapsed(this, e);
		}

		/// <summary>
		/// Method to raise the <see cref="Reactivated"/> event (performs
		/// consistency checks before raising <see cref="Reactivated"/>)
		/// </summary>
		/// <param name="e">
		/// <see cref="EventArgs"/> object
		/// </param>
		protected void OnReactivated(EventArgs e)
		{
			reactivated = true;
			if (Reactivated != null && enabled && (monitorKeyboard || monitorMouse))
				Reactivated(this, e);
		}

		#endregion Proteced Methods

		#region Private Methods

		private void TimerElapsed(object sender, ElapsedEventArgs e)
		{
			if (pollingTimer.SynchronizingObject != null)
				if (pollingTimer.SynchronizingObject.InvokeRequired)
				{
					pollingTimer.SynchronizingObject.BeginInvoke(
						new ElapsedEventHandler(TimerElapsed),
						new object[] {sender, e});
					return;
				}
			if (User32.GetLastInputInfo(out lastInputBuffer))
			{
				if (lastInputBuffer.dwTime != lastTickCount)
				{
					if (timeElapsed && !reactivated)
					{
						OnReactivated(new EventArgs());
						Reset();
					}
					lastTickCount = lastInputBuffer.dwTime;
					lastInputDate = DateTime.Now;
				}
				else if (!timeElapsed && (monitorMouse || monitorKeyboard))
					if (DateTime.Now.Subtract(lastInputDate).TotalMilliseconds > interval)
						OnElapsed(e);
			}
		}

		#endregion Private Methods
	}
}