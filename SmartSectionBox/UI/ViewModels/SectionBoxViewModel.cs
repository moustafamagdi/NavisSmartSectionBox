using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SmartSectionBox.Core;
using SmartSectionBox.Interaction;
using SmartSectionBox.Plugin;

namespace SmartSectionBox.UI.ViewModels
{
    /// <summary>
    /// Viewport-first launcher. Face movement belongs in the custom 3D overlay, not in a form.
    /// </summary>
    public sealed class SectionBoxViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SectionBoxService service;
        private bool interactionDiagnosticsEnabled;
        private string status = "Select model elements, or keep an existing Navisworks Box section active, then activate Smart Section Box.";

        public SectionBoxViewModel(SectionBoxService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.service.StatusChanged += OnStatusChanged;
            ActivateCommand = new DelegateCommand(_ => Activate());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ActivateCommand { get; }

        public bool InteractionDiagnosticsEnabled
        {
            get => interactionDiagnosticsEnabled;
            set
            {
                if (!SetField(ref interactionDiagnosticsEnabled, value)) return;
                InteractionDiagnostics.Enabled = value;
                Status = value
                    ? "Diagnostics enabled. The log will record face-selection decisions while you drag."
                    : "Diagnostics disabled.";
            }
        }

        public string Status
        {
            get => status;
            private set => SetField(ref status, value);
        }

        public void Dispose()
        {
            service.StatusChanged -= OnStatusChanged;
        }

        private void Activate()
        {
            string message;
            SmartSectionBoxRuntime.TryStartFromExistingBoxOrSelection(out message);
            Status = message;
        }

        private void OnStatusChanged(object sender, string message)
        {
            Status = message;
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
