using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Plugin;
using SmartSectionBox.UI;

namespace SmartSectionBox.Tests
{
    internal static class ToolLifecycleHarness
    {
        private const string FacePullToolPluginId = "SmartSectionBox.SectionBoxTool.MSSB";

        private static int Main()
        {
            try
            {
                VerifyStopReleasesCustomTool();
                VerifyPaneHideStopsTool();
                VerifyRepeatedStopIsSafe();
                Console.WriteLine("All Smart Section Box tool lifecycle tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void VerifyStopReleasesCustomTool()
        {
            var document = new Document
            {
                IsClear = false,
                ActiveView = new View(),
                Tool = new DocumentTool
                {
                    Value = Tool.CustomToolPlugin,
                    CustomToolPluginId = FacePullToolPluginId
                }
            };
            Application.MainDocument = document;

            Assert(SmartSectionBoxRuntime.IsFacePullActive, "Fixture must begin with the Smart Section Box custom tool active.");

            string message;
            Assert(SmartSectionBoxRuntime.TryDeactivateFacePull(out message), "Stop should succeed for the active custom tool.");
            Assert(document.Tool.Value == Tool.Select, "Stop must restore Navisworks' standard Select tool.");
            Assert(!SmartSectionBoxRuntime.IsFacePullActive, "Stop must clear Smart Section Box active state even when the old identifier remains cached.");
            Assert(message.IndexOf("stopped", StringComparison.OrdinalIgnoreCase) >= 0, "Stop should report a concise stopped status.");
        }

        private static void VerifyPaneHideStopsTool()
        {
            Application.MainDocument.Tool.Value = Tool.CustomToolPlugin;
            Application.MainDocument.Tool.CustomToolPluginId = FacePullToolPluginId;
            var pane = new SmartSectionBoxDockPanePlugin { Visible = false };

            pane.OnVisibleChanged();

            Assert(Application.MainDocument.Tool.Value == Tool.Select, "Hiding the dock pane must restore Navisworks' standard Select tool.");
            Assert(!SmartSectionBoxRuntime.IsFacePullActive, "Hiding the dock pane must clear Smart Section Box active state.");
        }

        private static void VerifyRepeatedStopIsSafe()
        {
            string message;
            Assert(SmartSectionBoxRuntime.TryDeactivateFacePull(out message), "Repeated Stop must be safe.");
            Assert(Application.MainDocument.Tool.Value == Tool.Select, "Repeated Stop must not change the standard tool.");
            Assert(message.IndexOf("already stopped", StringComparison.OrdinalIgnoreCase) >= 0, "Repeated Stop should report the inactive state.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
