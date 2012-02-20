// MonitorType Enumeration
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
	/// Defines constants to pass to <see cref="MonitorCreator.CreateInstance"/>
	/// </summary>
	public enum MonitorType : int
	{
		/// <summary>
		/// Monitor working with the events of the <see cref="Control"/> class
		/// </summary>
		ControlMonitor     = 0,

		/// <summary>
		/// Monitor which implements the <see cref="IMessageFilter.PreFilterMessage"/>
		/// method to intercept windows messages
		/// </summary>
		ApplicationMonitor = 1,

		/// <summary>
		/// Monitor which polls the last input time
		/// </summary>
		LastInputMonitor   = 2,

		/// <summary>
		/// Monitor which installs the windows hooks WH_KEYBOARD and WH_MOUSE
		/// </summary>
		ThreadHookMonitor  = 3,

		/// <summary>
		/// Monitor which installs the windows hooks WH_KEYBOARD_LL and WH_MOUSE_LL
		/// </summary>
		GlobalHookMonitor  = 4,

		/// <summary>
		/// Global default monitor
		/// </summary>
		DefaultMonitor     = GlobalHookMonitor
	}
}