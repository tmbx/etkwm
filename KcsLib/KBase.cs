using System;
using System.Windows.Forms;

namespace kcslib
{
    /// <summary>
    /// Core definition class.
    /// </summary>
    public static class KBase
    {
        /// <summary>
        /// Empty delegate.
        /// </summary>
        public delegate void EmptyDelegate();

        /// <summary>
        /// Event handler delegate.
        /// </summary>
        public delegate void EventHandlerDelegate(Object sender, EventArgs args);

        /// <summary>
        /// Exception-reporting delegate.
        /// </summary>
        public delegate void ExceptionDelegate(Exception ex);

        /// <summary>
        /// General error handling delegate.
        /// </summary>
        public delegate void HandleErrorDelegate(String errorMessage, bool fatalFlag);

        /// <summary>
        /// UI control used for BeginInvoke() calls.
        /// </summary>
        public static Control InvokeUiControl = null;

        /// <summary>
        /// Fired when an error must be handled.
        /// </summary>
        public static HandleErrorDelegate HandleErrorCallback;

        /// <summary>
        /// Return true if the current thread is executing in UI context.
        /// </summary>
        public static bool IsInUi()
        {
            return InvokeUiControl == null || !InvokeUiControl.InvokeRequired;
        }

        /// <summary>
        /// Execute the specified delegate in the context of the UI 
        /// asynchronously.
        /// </summary>
        public static void ExecInUI(Delegate d)
        {
            ExecInUI(d, null);
        }

        /// <summary>
        /// Execute the specified delegate in the context of the UI
        /// asynchronously.
        /// </summary>
        public static void ExecInUI(EmptyDelegate d)
        {
            ExecInUI(d, null);
        }

        /// <summary>
        /// Execute the specified delegate in the context of the UI
        /// asynchronously.
        /// </summary>
        public static void ExecInUI(Delegate d, Object[] args)
        {
            if (InvokeUiControl == null) return;

            try
            {
                InvokeUiControl.BeginInvoke(d, args);
            }

            // Threads may continue after the control has died. Ignore the 
            // problem and screw .Net, Windows and Microsoft for not 
            // implementing proper thread mailboxes.
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>
        /// Display an error message to the user and exit the application if 
        /// required.
        /// </summary>
        public static void HandleError(String errorMessage, bool fatalFlag)
        {
            if (HandleErrorCallback != null) HandleErrorCallback(errorMessage, fatalFlag);
        }

        /// <summary>
        /// Display an error message to the user after a non-fatal exception
        /// has been caught. 
        /// </summary>
        public static void HandleException(Exception e)
        {
            HandleException(e, false);
        }

        /// <summary>
        /// Display an error message to the user after an exception has been
        /// caught. If fatalFlag is true, the application will exit.
        /// </summary>
        public static void HandleException(Exception e, bool fatalFlag)
        {
            String msg = e.Message;
#if DEBUG
            msg += Environment.NewLine + Environment.NewLine + e.StackTrace;
#endif
            HandleError(msg, fatalFlag);
        }
    }
}