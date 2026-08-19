using System;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using SmartSectionBox.Core;
using SmartSectionBox.Infrastructure;

namespace SmartSectionBox.Plugin
{
    internal static class SmartSectionBoxRuntime
    {
        private const string FacePullToolPluginId = "SmartSectionBox.SectionBoxTool.MSSB";
        private static readonly SectionBoxService service = new SectionBoxService();

        public static SectionBoxService Service => service;
        public static bool LiveUpdates { get; set; } = true;

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
                    message = "Open a model before enabling Face Pull.";
                    return false;
                }

                var record = Application.Plugins.FindPlugin(FacePullToolPluginId) as ToolPluginRecord;
                if (record == null || !record.IsEnabled)
                {
                    message = "The Smart Section Box Face Pull tool is unavailable.";
                    return false;
                }

                document.Tool.SetCustomToolPlugin(record.LoadPlugin());
                message = "Face Pull is active. Move over a blue section-box face, then left-drag to pull that face.";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to activate the Smart Section Box Face Pull tool.", ex);
                message = "Unable to activate Face Pull. See the Smart Section Box log.";
                return false;
            }
        }
    }
}
