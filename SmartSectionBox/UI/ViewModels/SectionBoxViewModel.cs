using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SmartSectionBox.Core;
using SmartSectionBox.Interaction;
using SmartSectionBox.Plugin;

namespace SmartSectionBox.UI.ViewModels
{
    /// <summary>
    /// Minimal viewport-first launcher. The native Navisworks box remains the only viewport
    /// visualization; this view model supplies compact text feedback for the currently hovered face.
    /// </summary>
    public sealed class SectionBoxViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SectionBoxService service;
        private bool disposed;
        private bool interactionDiagnosticsEnabled;
        private string status = "Ready — activate, then drag a visible face.";
        private string hoverStatus;

        public SectionBoxViewModel(SectionBoxService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.service.StatusChanged += OnStatusChanged;
            SectionBoxToolPlugin.HoverChanged += OnHoverChanged;
            ActivateCommand = new DelegateCommand(_ => Activate());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ActivateCommand { get; }

        public string HoverStatus
        {
            get => hoverStatus;
            private set
            {
                if (SetField(ref hoverStatus, value)) RaisePropertyChanged(nameof(DisplayStatus));
            }
        }

        public string DisplayStatus => string.IsNullOrWhiteSpace(HoverStatus) ? Status : HoverStatus;

        public bool InteractionDiagnosticsEnabled
        {
            get => interactionDiagnosticsEnabled;
            set
            {
                if (!SetField(ref interactionDiagnosticsEnabled, value)) return;
                InteractionDiagnostics.Enabled = value;
                Status = value ? "Diagnostics on." : "Diagnostics off.";
            }
        }

        public string Status
        {
            get => status;
            private set
            {
                if (SetField(ref status, value)) RaisePropertyChanged(nameof(DisplayStatus));
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            service.StatusChanged -= OnStatusChanged;
            SectionBoxToolPlugin.HoverChanged -= OnHoverChanged;
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

        private void OnHoverChanged(object sender, FaceHoverState hover)
        {
            var next = Describe(hover);
            var dispatcher = Application.Current == null ? null : Application.Current.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!disposed) HoverStatus = next;
                }));
                return;
            }

            if (!disposed) HoverStatus = next;
        }

        private static string Describe(FaceHoverState hover)
        {
            if (hover == null || !hover.IsHovering) return null;
            var side = hover.PositiveSide ? "+" : "-";
            return "Face " + side + hover.Axis + " — drag to move.";
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            RaisePropertyChanged(name);
            return true;
        }

        private void RaisePropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
