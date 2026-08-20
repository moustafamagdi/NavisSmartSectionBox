using System;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using SmartSectionBox.Core;
using SmartSectionBox.Infrastructure;
using SmartSectionBox.Interaction;

namespace SmartSectionBox.Plugin
{
    internal static class SmartSectionBoxRuntime
    {
        private const string FacePullToolPluginId = "SmartSectionBox.SectionBoxTool.MSSB";
        private static readonly SectionBoxService service = new SectionBoxService();

        public static SectionBoxService Service => service;
        public static bool LiveUpdates { get; set; } = true;
        public static event EventHandler ToolStateChanged;

        public static bool IsFacePullActive
        {
            get
            {
                var document = Application.MainDocument;
                return document != null && document.Tool != null &&
                       document.Tool.Value == Tool.CustomToolPlugin &&
                       string.Equals(document.Tool.CustomToolPluginId, FacePullToolPluginId, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Custom ToolPlugin modes are exclusive in Navisworks. Invoke this after a user
        /// has chosen a native Move/Rotate/Scale mode to restore direct section-face pulling.
        /// </summary>
        public static bool TryActivateFacePull(out string message)
        {
            try
            {
                var document = Application.MainDocument;
                if (document == null || document.IsClear)
                {
                    message = "Open a model before starting Smart Section Box.";
                    return false;
                }

                var record = Application.Plugins.FindPlugin(FacePullToolPluginId) as ToolPluginRecord;
                if (record == null || !record.IsEnabled)
                {
                    message = "The Smart Section Box tool is unavailable.";
                    return false;
                }

                document.Tool.SetCustomToolPlugin(record.LoadPlugin());
                PublishToolStateChanged();
                message = "Tool started — drag a visible face.";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to activate the Smart Section Box Face Pull tool.", ex);
                message = "Unable to start the tool. See the Smart Section Box log.";
                return false;
            }
        }

        /// <summary>
        /// Cancels a pending face drag, then restores Navisworks' standard Select tool. Passing
        /// null to SetCustomToolPlugin is unsupported and throws, so a standard Tool value is
        /// deliberately assigned instead.
        /// </summary>
        public static bool TryDeactivateFacePull(out string message)
        {
            try
            {
                var document = Application.MainDocument;
                if (document == null || document.IsClear)
                {
                    PublishToolStateChanged();
                    message = "Smart Section Box is stopped.";
                    return true;
                }

                if (!IsFacePullActive)
                {
                    PublishToolStateChanged();
                    message = "Smart Section Box is already stopped.";
                    return true;
                }

                var record = Application.Plugins.FindPlugin(FacePullToolPluginId) as ToolPluginRecord;
                var facePullTool = record == null ? null : record.LoadPlugin() as SectionBoxToolPlugin;
                if (facePullTool != null) facePullTool.CancelActiveInteraction(document.ActiveView);

                document.Tool.Value = Tool.Select;
                if (document.ActiveView != null) document.ActiveView.RequestDelayedRedraw(ViewRedrawRequests.Render);
                PublishToolStateChanged();
                message = "Tool stopped — normal Navisworks selection restored.";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to deactivate the Smart Section Box Face Pull tool.", ex);
                message = "Unable to stop the tool. See the Smart Section Box log.";
                return false;
            }
        }

        private static void PublishToolStateChanged()
        {
            var handler = ToolStateChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static bool TryStartFromExistingBoxOrSelection(out string message)
        {
            if (!service.TryAdoptExistingOrFitToSelection(out message)) return false;

            string activationMessage;
            if (!TryActivateFacePull(out activationMessage))
            {
                message = activationMessage;
                return false;
            }

            var view = Application.MainDocument == null ? null : Application.MainDocument.ActiveView;
            if (view != null) view.RequestDelayedRedraw(ViewRedrawRequests.Render);
            message = activationMessage;
            return true;
        }
    }
}
