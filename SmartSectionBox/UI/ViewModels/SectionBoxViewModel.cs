using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SmartSectionBox.Core;
using SmartSectionBox.Interaction;
using SmartSectionBox.Persistence;
using SmartSectionBox.Plugin;

namespace SmartSectionBox.UI.ViewModels
{
    public sealed class SectionBoxViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SectionBoxService service;
        private readonly PresetStore presets;
        private SectionBoxState state;
        private bool isSynchronizing;
        private bool liveUpdates = true;
        private string status = "Ready. Click Fit to Model, then drag a box face in the viewport.";
        private string presetName = "Default";

        public SectionBoxViewModel(SectionBoxService service, PresetStore presets)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.presets = presets ?? throw new ArgumentNullException(nameof(presets));
            service.StateChanged += OnStateChanged;
            service.StatusChanged += OnStatusChanged;
            SectionBoxToolPlugin.HoverChanged += OnHoverChanged;

            RefreshCommand = new DelegateCommand(_ => Refresh());
            FitSelectionCommand = new DelegateCommand(_ => service.FitToSelection());
            FitModelCommand = new DelegateCommand(_ => service.FitToModel());
            ResetCommand = new DelegateCommand(_ => service.ResetToNoClip());
            InvertCommand = new DelegateCommand(_ => service.InvertDirection());
            NudgeCommand = new DelegateCommand(Nudge);
            SavePresetCommand = new DelegateCommand(_ => SavePreset());
            LoadPresetCommand = new DelegateCommand(_ => LoadPreset());
            DeletePresetCommand = new DelegateCommand(_ => DeletePreset());
            Refresh();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand RefreshCommand { get; }
        public ICommand FitSelectionCommand { get; }
        public ICommand FitModelCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand InvertCommand { get; }
        public ICommand NudgeCommand { get; }
        public ICommand SavePresetCommand { get; }
        public ICommand LoadPresetCommand { get; }
        public ICommand DeletePresetCommand { get; }

        public string Status
        {
            get => status;
            private set => SetField(ref status, value);
        }

        public string PresetName
        {
            get => presetName;
            set => SetField(ref presetName, value);
        }

        public bool LiveUpdates
        {
            get => liveUpdates;
            set
            {
                if (SetField(ref liveUpdates, value)) SmartSectionBoxRuntime.LiveUpdates = value;
            }
        }

        public bool Enabled
        {
            get => state != null && state.Enabled;
            set
            {
                if (state == null || isSynchronizing || state.Enabled == value) return;
                var copy = state.Clone();
                copy.Enabled = value;
                service.SetBox(copy, true);
            }
        }

        public double MinX { get => Coordinate(SectionBoxFaceId.MinX); set => UpdateCoordinate(SectionBoxFaceId.MinX, value); }
        public double MaxX { get => Coordinate(SectionBoxFaceId.MaxX); set => UpdateCoordinate(SectionBoxFaceId.MaxX, value); }
        public double MinY { get => Coordinate(SectionBoxFaceId.MinY); set => UpdateCoordinate(SectionBoxFaceId.MinY, value); }
        public double MaxY { get => Coordinate(SectionBoxFaceId.MaxY); set => UpdateCoordinate(SectionBoxFaceId.MaxY, value); }
        public double MinZ { get => Coordinate(SectionBoxFaceId.MinZ); set => UpdateCoordinate(SectionBoxFaceId.MinZ, value); }
        public double MaxZ { get => Coordinate(SectionBoxFaceId.MaxZ); set => UpdateCoordinate(SectionBoxFaceId.MaxZ, value); }

        public double SliderMinimum => state == null ? -1 : Math.Min(Math.Min(MinX, MinY), MinZ) - SliderRange;
        public double SliderMaximum => state == null ? 1 : Math.Max(Math.Max(MaxX, MaxY), MaxZ) + SliderRange;
        public double SliderRange => state == null ? 1 : Math.Max(1, Math.Max(MaxX - MinX, Math.Max(MaxY - MinY, MaxZ - MinZ)) * 0.25);
        public double StepSize => Math.Max(SliderRange / 100.0, service.MinimumBoxThickness);

        public void Dispose()
        {
            service.StateChanged -= OnStateChanged;
            service.StatusChanged -= OnStatusChanged;
            SectionBoxToolPlugin.HoverChanged -= OnHoverChanged;
        }

        private void Refresh()
        {
            var snapshot = service.RefreshFromNative();
            if (snapshot != null) ApplySnapshot(snapshot);
        }

        private void UpdateCoordinate(SectionBoxFaceId face, double value)
        {
            if (state == null || isSynchronizing || double.IsNaN(value) || double.IsInfinity(value)) return;
            var copy = state.Clone();
            copy.SetFaceCoordinate(face, value, service.MinimumBoxThickness);
            service.SetBox(copy, !LiveUpdates);
        }

        private double Coordinate(SectionBoxFaceId face) => state == null ? 0 : state.GetFaceCoordinate(face);

        private void Nudge(object parameter)
        {
            if (state == null || parameter == null) return;
            var tokens = Convert.ToString(parameter, CultureInfo.InvariantCulture).Split(':');
            if (tokens.Length != 2) return;
            SectionBoxFaceId face;
            double direction;
            if (!Enum.TryParse(tokens[0], out face) || !double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out direction)) return;
            UpdateCoordinate(face, state.GetFaceCoordinate(face) + direction * StepSize);
        }

        private void SavePreset()
        {
            try
            {
                if (state == null) return;
                presets.Save(PresetName, state);
                Status = "Saved preset '" + PresetName + "'.";
            }
            catch (Exception ex)
            {
                Status = "Unable to save preset: " + ex.Message;
            }
        }

        private void LoadPreset()
        {
            try
            {
                var preset = presets.Load(PresetName);
                if (preset == null)
                {
                    Status = "Preset '" + PresetName + "' was not found for this document.";
                    return;
                }
                service.SetBox(preset.State, true);
                Status = "Loaded preset '" + preset.Name + "'.";
            }
            catch (Exception ex)
            {
                Status = "Unable to load preset: " + ex.Message;
            }
        }

        private void DeletePreset()
        {
            try
            {
                Status = presets.Delete(PresetName) ? "Deleted preset '" + PresetName + "'." : "Preset '" + PresetName + "' was not found.";
            }
            catch (Exception ex)
            {
                Status = "Unable to delete preset: " + ex.Message;
            }
        }

        private void OnStateChanged(object sender, SectionBoxState snapshot) => ApplySnapshot(snapshot);
        private void OnStatusChanged(object sender, string message) => Status = message ?? string.Empty;

        private void OnHoverChanged(object sender, FaceHoverState hover)
        {
            if (hover == null || !hover.IsHovering) return;
            Status = hover.PositiveSide ? "Dragging target: MAX " + hover.Axis + " = " + hover.Coordinate.ToString("0.###", CultureInfo.CurrentCulture)
                                        : "Dragging target: MIN " + hover.Axis + " = " + hover.Coordinate.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private void ApplySnapshot(SectionBoxState snapshot)
        {
            if (snapshot == null) return;
            isSynchronizing = true;
            state = snapshot.Clone();
            RaiseAllStateProperties();
            isSynchronizing = false;
        }

        private void RaiseAllStateProperties()
        {
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(MinX));
            OnPropertyChanged(nameof(MaxX));
            OnPropertyChanged(nameof(MinY));
            OnPropertyChanged(nameof(MaxY));
            OnPropertyChanged(nameof(MinZ));
            OnPropertyChanged(nameof(MaxZ));
            OnPropertyChanged(nameof(SliderMinimum));
            OnPropertyChanged(nameof(SliderMaximum));
            OnPropertyChanged(nameof(SliderRange));
            OnPropertyChanged(nameof(StepSize));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
