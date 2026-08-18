using System;
using System.Windows;
using System.Windows.Controls;
using SmartSectionBox.Persistence;
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
            viewModel = new SectionBoxViewModel(SmartSectionBoxRuntime.Service, new PresetStore());
            DataContext = viewModel;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            viewModel.Dispose();
        }
    }
}
