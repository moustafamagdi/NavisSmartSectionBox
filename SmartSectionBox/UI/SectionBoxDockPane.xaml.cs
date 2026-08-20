using System.Windows.Controls;
using SmartSectionBox.Plugin;
using SmartSectionBox.UI.ViewModels;

namespace SmartSectionBox.UI
{
    public partial class SectionBoxDockPane : UserControl
    {
        private readonly SectionBoxViewModel viewModel;

        public SectionBoxDockPane()
        {
            InitializeComponent();
            viewModel = new SectionBoxViewModel(SmartSectionBoxRuntime.Service);
            DataContext = viewModel;
        }

        /// <summary>
        /// Called only from DockPanePlugin.DestroyControlPane. A visibility transition must not
        /// dispose the view model because Navisworks can retain and later show the same pane.
        /// </summary>
        public void DisposeViewModel()
        {
            viewModel.Dispose();
        }
    }
}
