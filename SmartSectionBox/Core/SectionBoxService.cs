using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Infrastructure;

namespace SmartSectionBox.Core
{
    /// <summary>
    /// The single authoritative source of section-box state. It has no WPF dependency.
    /// All public writes are normalized and sent through View.TrySetClippingPlanes.
    /// </summary>
    public sealed class SectionBoxService
    {
        private readonly object gate = new object();
        private readonly SectionBoxJsonAdapter jsonAdapter = new SectionBoxJsonAdapter();
        private SectionBoxState current;
        private DateTime lastApplyUtc = DateTime.MinValue;

        public event EventHandler<SectionBoxState> StateChanged;
        public event EventHandler<string> StatusChanged;

        public double MinimumBoxThickness { get; set; } = 0.001;
        public double FitPadding { get; set; } = 0.05;
        public int LiveApplyIntervalMilliseconds { get; set; } = 75;

        public SectionBoxState GetCurrentBox()
        {
            lock (gate)
            {
                // Mouse movement reads this cache. Native parsing is intentionally limited to
                // initialization and explicit Refresh calls so large federated models remain responsive.
                if (current != null) return current.Clone();
                return RefreshFromNative();
            }
        }

        public SectionBoxState RefreshFromNative()
        {
            lock (gate)
            {
                try
                {
                    var view = RequireActiveView();
                    var nativeJson = view.GetClippingPlanes();
                    SectionBoxState decoded;
                    string diagnostic;
                    if (jsonAdapter.TryDecode(nativeJson, out decoded, out diagnostic))
                    {
                        current = decoded.Normalized(MinimumBoxThickness);
                        PublishState(current);
                        return current.Clone();
                    }

                    PublishStatus(diagnostic);
                    return current == null ? null : current.Clone();
                }
                catch (Exception ex)
                {
                    Logger.Error("Unable to read current clipping state.", ex);
                    PublishStatus("Unable to read the current section box. See the Smart Section Box log.");
                    return current == null ? null : current.Clone();
                }
            }
        }

        public bool EnableSectioning(bool enabled)
        {
            try
            {
                var state = GetCurrentBox();
                if (state == null)
                {
                    state = CreateFromBounds(GetModelBounds());
                }
                state.Enabled = enabled;
                return SetBox(state, true);
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to change sectioning enable state.", ex);
                PublishStatus("Unable to enable Smart Section Box. Create a native Box section once, then click Refresh.");
                return false;
            }
        }

        public bool SetBox(SectionBoxState state, bool force = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            lock (gate)
            {
                try
                {
                    var normalized = state.Normalized(MinimumBoxThickness);
                    var shouldPublishStatus = force || current == null || current.Enabled != normalized.Enabled;
                    var now = DateTime.UtcNow;
                    if (!force && (now - lastApplyUtc).TotalMilliseconds < LiveApplyIntervalMilliseconds)
                    {
                        current = normalized;
                        PublishState(current);
                        return true;
                    }

                    var view = RequireActiveView();
                    var json = jsonAdapter.Encode(normalized);
                    if (!view.TrySetClippingPlanes(json))
                    {
                        Logger.Warn("Navisworks rejected a ClipPlaneSet update. The native JSON payload was retained in the log.");
                        Logger.Warn(json);
                        PublishStatus("Navisworks rejected the clipping update. Enable a native Box section once, click Refresh, and retry.");
                        return false;
                    }

                    lastApplyUtc = now;
                    normalized.NativeJsonTemplate = view.GetClippingPlanes();
                    current = normalized;
                    PublishState(current);
                    if (shouldPublishStatus)
                    {
                        PublishStatus(normalized.Enabled ? "Smart Section Box active." : "Smart Section Box disabled.");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error("Unable to apply section-box state.", ex);
                    PublishStatus("Unable to apply the section box. See the Smart Section Box log.");
                    return false;
                }
            }
        }

        public bool ApplyPending(SectionBoxState state)
        {
            return SetBox(state, true);
        }

        public bool TryAdoptExistingOrFitToSelection(out string message)
        {
            try
            {
                var existing = RefreshFromNative();
                if (existing != null)
                {
                    if (!existing.Enabled)
                    {
                        existing.Enabled = true;
                        if (!SetBox(existing, true))
                        {
                            message = "The existing Navisworks section box could not be activated.";
                            return false;
                        }
                    }

                    message = "Adopted the existing Navisworks section box. Drag the custom red faces; hold Ctrl for yellow underlay faces.";
                    PublishStatus(message);
                    return true;
                }

                if (!FitToSelection())
                {
                    message = "Select at least one model element, or create a native Navisworks Box section, then activate Smart Section Box.";
                    return false;
                }

                message = "Created a section box around the current selection. Drag the custom red faces; hold Ctrl for yellow underlay faces.";
                PublishStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to adopt the native section box or fit the current selection.", ex);
                message = "Unable to start Smart Section Box. See the Smart Section Box log.";
                PublishStatus(message);
                return false;
            }
        }

        public bool FitToSelection()
        {
            try
            {
                return SetBox(CreateFromBounds(GetSelectionBounds()), true);
            }
            catch (InvalidOperationException ex)
            {
                PublishStatus(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to fit section box to selection.", ex);
                PublishStatus("Unable to fit to selection. See the Smart Section Box log.");
                return false;
            }
        }

        public bool FitToModel()
        {
            try
            {
                return SetBox(CreateFromBounds(GetModelBounds()), true);
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to fit section box to model.", ex);
                PublishStatus("Unable to fit to model. See the Smart Section Box log.");
                return false;
            }
        }

        public bool ResetToNoClip()
        {
            var state = GetCurrentBox();
            if (state == null) return false;
            state.Enabled = false;
            return SetBox(state, true);
        }

        public bool ExpandFace(SectionBoxFaceId face, double distance)
        {
            var state = GetCurrentBox();
            if (state == null) return false;
            var sign = IsPositive(face) ? 1.0 : -1.0;
            state.SetFaceCoordinate(face, state.GetFaceCoordinate(face) + sign * Math.Abs(distance), MinimumBoxThickness);
            return SetBox(state);
        }

        public bool ContractFace(SectionBoxFaceId face, double distance)
        {
            return ExpandFace(face, -Math.Abs(distance));
        }

        public bool InvertDirection()
        {
            PublishStatus("Direction inversion is not applicable to a closed Navisworks Box section. Use native Plane sectioning for directional inversion.");
            return false;
        }

        private static bool IsPositive(SectionBoxFaceId face)
        {
            return face == SectionBoxFaceId.MaxX || face == SectionBoxFaceId.MaxY || face == SectionBoxFaceId.MaxZ;
        }

        private SectionBoxState CreateFromBounds(Bounds3D bounds)
        {
            if (!bounds.IsValid) throw new InvalidOperationException("No valid model bounds are available.");
            var padded = SectionBoxMath.Expand(bounds, FitPadding);
            return new SectionBoxState
            {
                Enabled = true,
                MinX = padded.Min.X,
                MinY = padded.Min.Y,
                MinZ = padded.Min.Z,
                MaxX = padded.Max.X,
                MaxY = padded.Max.Y,
                MaxZ = padded.Max.Z
            }.Normalized(MinimumBoxThickness);
        }

        private static Bounds3D GetSelectionBounds()
        {
            var document = RequireDocument();
            var selection = document.CurrentSelection.SelectedItems;
            if (selection == null || selection.Count == 0)
            {
                throw new InvalidOperationException("Select at least one model item before fitting Smart Section Box to selection.");
            }

            Bounds3D result = default(Bounds3D);
            var hasBounds = false;
            foreach (ModelItem item in selection)
            {
                var box = item.BoundingBox();
                var converted = ToBounds(box);
                result = hasBounds ? SectionBoxMath.Union(result, converted) : converted;
                hasBounds = true;
            }

            if (!hasBounds || !result.IsValid) throw new InvalidOperationException("The selection has no valid bounding box.");
            return result;
        }

        private static Bounds3D GetModelBounds()
        {
            var document = RequireDocument();
            Bounds3D result = default(Bounds3D);
            var hasBounds = false;
            foreach (Model model in document.Models)
            {
                if (model == null || model.RootItem == null) continue;
                var converted = ToBounds(model.RootItem.BoundingBox());
                result = hasBounds ? SectionBoxMath.Union(result, converted) : converted;
                hasBounds = true;
            }

            if (!hasBounds || !result.IsValid) throw new InvalidOperationException("The active document has no model bounds.");
            return result;
        }

        private static Bounds3D ToBounds(BoundingBox3D box)
        {
            return new Bounds3D(
                new Vector3(box.Min.X, box.Min.Y, box.Min.Z),
                new Vector3(box.Max.X, box.Max.Y, box.Max.Z));
        }

        private static Document RequireDocument()
        {
            var document = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (document == null || document.IsClear) throw new InvalidOperationException("No Navisworks document is open.");
            return document;
        }

        private static View RequireActiveView()
        {
            var document = RequireDocument();
            if (document.ActiveView == null) throw new InvalidOperationException("No active Navisworks view is available.");
            return document.ActiveView;
        }

        private void PublishState(SectionBoxState state)
        {
            var handler = StateChanged;
            if (handler != null) handler(this, state.Clone());
        }

        private void PublishStatus(string status)
        {
            Logger.Info(status ?? string.Empty);
            var handler = StatusChanged;
            if (handler != null) handler(this, status);
        }
    }
}
