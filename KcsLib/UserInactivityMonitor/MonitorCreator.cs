// MonitorCreator Class
// Revision 1 (2004-12-22)
// Copyright (C) 2004 Dennis Dietrich
//
// Released unter the BSD License
// http://www.opensource.org/licenses/bsd-license.php

using System.Windows.Forms;

namespace UserInactivityMonitoring
{
	/// <summary>
	/// Static class for creating <see cref="IInactivityMonitor"/> objects
	/// </summary>
	public class MonitorCreator
	{
		private MonitorCreator()
		{
		}

		/// <summary>
		/// Creates a new instance of the default monitor class (global)
		/// </summary>
		/// <returns>Reference to the new  <see cref="IInactivityMonitor"/> object</returns>
		public static IInactivityMonitor CreateInstance()
		{
			return CreateInstance(null, MonitorType.DefaultMonitor);
		}

		/// <summary>
		/// Creates a new instance of a monitor class
		/// </summary>
		/// <param name="type">
		/// Type of the monitor to create
		/// </param>
		/// <returns>Reference to the new  <see cref="IInactivityMonitor"/> object</returns>
		public static IInactivityMonitor CreateInstance(MonitorType type)
		{
			return CreateInstance(null, type);
		}

		/// <summary>
		/// Creates a new instance of a monitor class
		/// </summary>
		/// <param name="target">
		/// <see cref="Control"/> to monitor (may be null if not needed and
		/// is not checked by <see cref="CreateInstance"/> before calling the constructor)
		/// </param>
		/// <param name="type">
		/// Type of the monitor to create
		/// </param>
		/// <returns>Reference to the new  <see cref="IInactivityMonitor"/> object</returns>
		public static IInactivityMonitor CreateInstance(Control target, MonitorType type)
		{
			switch (type)
			{
				case MonitorType.ApplicationMonitor:
					return new ApplicationMonitor();
				case MonitorType.ControlMonitor:
					return new ControlMonitor(target);
				case MonitorType.LastInputMonitor:
					return new LastInputMonitor();
				case MonitorType.ThreadHookMonitor:
					return new HookMonitor(false);
				case MonitorType.GlobalHookMonitor:
				default:
					return new HookMonitor(true);
			}
		}
	}
}