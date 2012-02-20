namespace UserInactivityMonitoring
{
	internal enum Win32Message : int
	{
		WM_KEYFIRST      = 0x0100,
		WM_KEYDOWN       = 0x0100,
		WM_KEYUP         = 0x0101,
		WM_CHAR          = 0x0102,
		WM_DEADCHAR      = 0x0103,
		WM_SYSKEYDOWN    = 0x0104,
		WM_SYSKEYUP      = 0x0105,
		WM_SYSCHAR       = 0x0106,
		WM_SYSDEADCHAR   = 0x0107,

		WM_MOUSEFIRST    = 0x0200,
		WM_MOUSEMOVE     = 0x0200,
		WM_LBUTTONDOWN   = 0x0201,
		WM_LBUTTONUP     = 0x0202,
		WM_LBUTTONDBLCLK = 0x0203,
		WM_RBUTTONDOWN   = 0x0204,
		WM_RBUTTONUP     = 0x0205,
		WM_RBUTTONDBLCLK = 0x0206,
		WM_MBUTTONDOWN   = 0x0207,
		WM_MBUTTONUP     = 0x0208,
		WM_MBUTTONDBLCLK = 0x0209,
		WM_MOUSEWHEEL    = 0x020A
	}
}
