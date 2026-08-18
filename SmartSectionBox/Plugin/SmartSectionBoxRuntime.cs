using SmartSectionBox.Core;

namespace SmartSectionBox.Plugin
{
    internal static class SmartSectionBoxRuntime
    {
        private static readonly SectionBoxService service = new SectionBoxService();

        public static SectionBoxService Service => service;
        public static bool LiveUpdates { get; set; } = true;
    }
}
