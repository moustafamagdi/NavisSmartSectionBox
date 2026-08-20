using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Autodesk.Navisworks.Api.Plugins;
using SmartSectionBox.Plugin;

namespace SmartSectionBox.UI
{
    [Plugin("SmartSectionBox.DockPane", "MSSB", DisplayName = "Smart Section Box")]
    [DockPanePlugin(320, 145, FixedSize = false)]
    public sealed class SmartSectionBoxDockPanePlugin : DockPanePlugin
    {
        public override Control CreateControlPane()
        {
            var host = new ElementHost
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                MinimumSize = new System.Drawing.Size(0, 0),
                Child = new SectionBoxDockPane()
            };
            host.CreateControl();
            return host;
        }

        /// <summary>
        /// Navisworks calls this when the user hides or closes the pane. The custom tool is
        /// exclusive, so returning to a standard Navisworks tool is required even if the WPF
        /// control remains cached by the host.
        /// </summary>
        public override void OnVisibleChanged()
        {
            base.OnVisibleChanged();
            if (Visible) return;

            string ignored;
            SmartSectionBoxRuntime.TryDeactivateFacePull(out ignored);
        }

        public override void DestroyControlPane(Control pane)
        {
            // DestroyControlPane is a lifecycle backstop in case Navisworks disposes the host
            // control without first raising the visibility callback. Do not dispose the WPF view
            // model on a normal hide/show transition; Navisworks can retain the pane instance.
            string ignored;
            SmartSectionBoxRuntime.TryDeactivateFacePull(out ignored);
            var host = pane as ElementHost;
            var dockPane = host == null ? null : host.Child as SectionBoxDockPane;
            if (dockPane != null) dockPane.DisposeViewModel();
            pane?.Dispose();
        }
    }
}
