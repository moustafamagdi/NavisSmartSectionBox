using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;
using SmartSectionBox.Plugin;
using SmartSectionBox.UI.ViewModels;

namespace SmartSectionBox.Tests
{
    internal static class ToggleViewModelHarness
    {
        private const string FacePullToolPluginId = "SmartSectionBox.SectionBoxTool.MSSB";

        private static int Main()
        {
            try
            {
                Application.MainDocument = new Document
                {
                    IsClear = false,
                    ActiveView = new View(),
                    Tool = new DocumentTool
                    {
                        Value = Tool.CustomToolPlugin,
                        CustomToolPluginId = FacePullToolPluginId
                    }
                };

                using (var viewModel = new SectionBoxViewModel(new SectionBoxService()))
                {
                    Assert(viewModel.ToggleButtonText == "Stop", "Toggle must display Stop while Smart Section Box owns the custom tool.");
                    viewModel.ToggleCommand.Execute(null);
                    Assert(Application.MainDocument.Tool.Value == Tool.Select, "Toggle Stop must restore the Navisworks Select tool.");
                    Assert(viewModel.ToggleButtonText == "Start", "Toggle must display Start after the custom tool is released.");
                }

                Console.WriteLine("All Smart Section Box toggle view-model tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
