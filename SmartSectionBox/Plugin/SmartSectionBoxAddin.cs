using System;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using SmartSectionBox.Infrastructure;

namespace SmartSectionBox.Plugin
{
    [Plugin("SmartSectionBox.SmartSectionBoxAddin", "MSSB", DisplayName = "Smart Section Box", ToolTip = "Enable direct viewport face dragging for a section box.")]
    public sealed class SmartSectionBoxAddin : AddInPlugin
    {
        private const string DockPanePluginId = "SmartSectionBox.DockPane.MSSB";
        private const string ToolPluginId = "SmartSectionBox.SectionBoxTool.MSSB";

        public override int Execute(params string[] parameters)
        {
            try
            {
                if (Application.IsAutomated)
                {
                    throw new InvalidOperationException("Smart Section Box requires an interactive Navisworks session.");
                }

                ShowDockPane();
                string facePullMessage;
                if (!SmartSectionBoxRuntime.TryActivateFacePull(out facePullMessage))
                {
                    throw new InvalidOperationException(facePullMessage);
                }
                if (!SmartSectionBoxRuntime.Service.EnableSectioning(true))
                {
                    Logger.Warn("Smart Section Box was activated, but clipping could not be enabled from the current native payload.");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to activate Smart Section Box.", ex);
                return 1;
            }
        }

        public override CommandState CanExecute()
        {
            var state = new CommandState
            {
                IsVisible = true,
                IsEnabled = !Application.IsAutomated,
                IsChecked = Application.MainDocument != null && Application.MainDocument.Tool.CustomToolPluginId == ToolPluginId
            };
            return state;
        }

        private static void ShowDockPane()
        {
            var record = Application.Plugins.FindPlugin(DockPanePluginId) as DockPanePluginRecord;
            if (record == null || !record.IsEnabled) throw new InvalidOperationException("The Smart Section Box dock pane plug-in is unavailable.");
            if (record.LoadedPlugin == null) record.LoadPlugin();
            var pane = record.LoadedPlugin as DockPanePlugin;
            if (pane == null) throw new InvalidOperationException("The Smart Section Box dock pane could not be loaded.");
            pane.Visible = true;
            pane.ActivatePane();
        }

    }
}
