using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Autodesk.Navisworks.Api.Plugins;

namespace SmartSectionBox.UI
{
    [Plugin("SmartSectionBox.DockPane", "MSSB", DisplayName = "Smart Section Box")]
    [DockPanePlugin(280, 620, FixedSize = false)]
    public sealed class SmartSectionBoxDockPanePlugin : DockPanePlugin
    {
        public override Control CreateControlPane()
        {
            var host = new ElementHost
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Child = new SectionBoxDockPane()
            };
            host.CreateControl();
            return host;
        }

        public override void DestroyControlPane(Control pane)
        {
            pane?.Dispose();
        }
    }
}
