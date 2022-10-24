using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace ClassOutline
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("c9bf64e8-8464-46c7-b254-b6ccb8b52914")]
    public class ClassOutline2022 : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassOutline2022"/> class.
        /// </summary>
        public ClassOutline2022() : base(null)
        {
            this.Caption = "ClassOutline2022";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new MainWindowControl();
        }
    }
}
