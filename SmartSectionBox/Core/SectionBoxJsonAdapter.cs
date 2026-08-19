using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace SmartSectionBox.Core
{
    /// <summary>
    /// Preserves Navisworks' native ClipPlaneSet JSON envelope. The public SDK documents
    /// the payload only as JSON; this adapter therefore discovers the native box member
    /// from GetClippingPlanes rather than binding application state to undocumented DTOs.
    /// </summary>
    internal sealed class SectionBoxJsonAdapter
    {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public bool TryDecode(string json, out SectionBoxState state, out string diagnostic)
        {
            state = null;
            diagnostic = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                diagnostic = "Navisworks returned an empty clipping payload.";
                return false;
            }

            try
            {
                var root = serializer.DeserializeObject(json) as IDictionary<string, object>;
                if (root == null)
                {
                    diagnostic = "The clipping payload root is not an object.";
                    return false;
                }

                object boxValue;
                if (!TryFindBox(root, out boxValue))
                {
                    diagnostic = "The clipping payload does not expose a supported box member. Enable a native Box section once, then refresh Smart Section Box.";
                    return false;
                }

                Vector3 min;
                Vector3 max;
                if (!TryReadBounds(boxValue, out min, out max))
                {
                    diagnostic = "The clipping box member was found but its coordinate format is not recognized.";
                    return false;
                }

                state = new SectionBoxState
                {
                    Enabled = ReadBoolean(root, "Enabled", ReadBoolean(root, "Enable", true)),
                    MinX = min.X,
                    MinY = min.Y,
                    MinZ = min.Z,
                    MaxX = max.X,
                    MaxY = max.Y,
                    MaxZ = max.Z,
                    NativeJsonTemplate = json
                };

                ReadRotation(root, state);
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = "Unable to parse clipping JSON: " + ex.Message;
                return false;
            }
        }

        public string Encode(SectionBoxState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            IDictionary<string, object> root = null;
            if (!string.IsNullOrWhiteSpace(state.NativeJsonTemplate))
            {
                try { root = serializer.DeserializeObject(state.NativeJsonTemplate) as IDictionary<string, object>; }
                catch (ArgumentException) { root = null; }
            }

            if (root == null)
            {
                root = CreateFallbackEnvelope(state);
            }

            object boxValue;
            if (!TryFindBox(root, out boxValue) || !WriteBounds(boxValue, state))
            {
                root = CreateFallbackEnvelope(state);
            }

            WriteBoolean(root, "Enabled", state.Enabled);
            WriteRotation(root, state);
            return serializer.Serialize(root);
        }

        private static IDictionary<string, object> CreateFallbackEnvelope(SectionBoxState state)
        {
            // Native Navisworks box mode uses OrientedBox3D. This exact envelope was
            // observed from GetClippingPlanes() in Navisworks box mode; every write remains
            // guarded by View.TrySetClippingPlanes and a native template is still preferred.
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Type"] = "ClipPlaneSet",
                ["Version"] = 1,
                ["OrientedBox"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Type"] = "OrientedBox3D",
                    ["Version"] = 1,
                    ["Box"] = Coordinates(state),
                    ["Rotation"] = new ArrayList { state.RotationX, state.RotationY, state.RotationZ }
                },
                ["Enable"] = state.Enabled
            };
        }

        private static ArrayList Coordinates(SectionBoxState state)
        {
            return new ArrayList
            {
                new ArrayList { state.MinX, state.MinY, state.MinZ },
                new ArrayList { state.MaxX, state.MaxY, state.MaxZ }
            };
        }

        private static bool TryFindBox(IDictionary<string, object> current, out object box)
        {
            // Native Navisworks box payloads use Min and Max points directly on the
            // box object. The object may be the root or a nested named member.
            if (IsRecognizedBoxValue(current))
            {
                box = current;
                return true;
            }

            foreach (var pair in current)
            {
                if (string.Equals(pair.Key, "Box", StringComparison.OrdinalIgnoreCase) && IsRecognizedBoxValue(pair.Value))
                {
                    box = pair.Value;
                    return true;
                }
            }

            foreach (var child in current.Values)
            {
                var map = child as IDictionary<string, object>;
                if (map != null && TryFindBox(map, out box)) return true;
            }

            box = null;
            return false;
        }

        private static bool IsRecognizedBoxValue(object value)
        {
            Vector3 min;
            Vector3 max;
            return TryReadBounds(value, out min, out max);
        }

        private static bool TryReadBounds(object value, out Vector3 min, out Vector3 max)
        {
            min = new Vector3();
            max = new Vector3();
            var array = value as IList;
            if (array != null && array.Count == 2)
            {
                return TryReadVector(array[0], out min) && TryReadVector(array[1], out max);
            }

            var map = value as IDictionary<string, object>;
            if (map != null)
            {
                object minValue;
                object maxValue;
                if (TryGet(map, "Min", out minValue) && TryGet(map, "Max", out maxValue))
                {
                    return TryReadVector(minValue, out min) && TryReadVector(maxValue, out max);
                }
            }

            return false;
        }

        private static bool WriteBounds(object value, SectionBoxState state)
        {
            var array = value as IList;
            if (array != null && array.Count == 2)
            {
                array[0] = new ArrayList { state.MinX, state.MinY, state.MinZ };
                array[1] = new ArrayList { state.MaxX, state.MaxY, state.MaxZ };
                return true;
            }

            var map = value as IDictionary<string, object>;
            if (map != null)
            {
                string minKey;
                string maxKey;
                if (TryGetKey(map, "Min", out minKey) && TryGetKey(map, "Max", out maxKey))
                {
                    map[minKey] = new ArrayList { state.MinX, state.MinY, state.MinZ };
                    map[maxKey] = new ArrayList { state.MaxX, state.MaxY, state.MaxZ };
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadVector(object value, out Vector3 vector)
        {
            vector = new Vector3();
            var array = value as IList;
            if (array == null || array.Count < 3) return false;
            double x;
            double y;
            double z;
            if (!TryToDouble(array[0], out x) || !TryToDouble(array[1], out y) || !TryToDouble(array[2], out z)) return false;
            vector = new Vector3(x, y, z);
            return true;
        }

        private static void ReadRotation(IDictionary<string, object> root, SectionBoxState state)
        {
            IDictionary<string, object> container;
            object rotation;
            if (TryFindMapContaining(root, "Rotation", out container) && TryGet(container, "Rotation", out rotation))
            {
                var values = rotation as IList;
                double x;
                double y;
                double z;
                if (values != null && values.Count >= 3 && TryToDouble(values[0], out x) && TryToDouble(values[1], out y) && TryToDouble(values[2], out z))
                {
                    state.RotationX = x;
                    state.RotationY = y;
                    state.RotationZ = z;
                }
            }
        }

        private static void WriteRotation(IDictionary<string, object> root, SectionBoxState state)
        {
            IDictionary<string, object> container;
            if (TryFindMapContaining(root, "Rotation", out container))
            {
                string key;
                if (TryGetKey(container, "Rotation", out key))
                {
                    container[key] = new ArrayList { state.RotationX, state.RotationY, state.RotationZ };
                }
            }
        }

        private static bool TryFindMapContaining(IDictionary<string, object> current, string property, out IDictionary<string, object> result)
        {
            object ignored;
            if (TryGet(current, property, out ignored))
            {
                result = current;
                return true;
            }

            foreach (var child in current.Values)
            {
                var map = child as IDictionary<string, object>;
                if (map != null && TryFindMapContaining(map, property, out result)) return true;
            }

            result = null;
            return false;
        }

        private static bool ReadBoolean(IDictionary<string, object> dictionary, string name, bool fallback)
        {
            object value;
            bool parsed;
            return TryGet(dictionary, name, out value) && bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : fallback;
        }

        private static void WriteBoolean(IDictionary<string, object> dictionary, string name, bool value)
        {
            string key;
            if (!TryGetKey(dictionary, name, out key))
            {
                // Some documented examples use Enable while the current .NET payload
                // normally uses Enabled. Preserve whichever spelling the host supplied.
                if (!TryGetKey(dictionary, "Enable", out key)) key = name;
            }
            dictionary[key] = value;
        }

        private static bool TryGet(IDictionary<string, object> dictionary, string key, out object value)
        {
            string actualKey;
            if (TryGetKey(dictionary, key, out actualKey))
            {
                value = dictionary[actualKey];
                return true;
            }
            value = null;
            return false;
        }

        private static bool TryGetKey(IDictionary<string, object> dictionary, string key, out string actualKey)
        {
            actualKey = dictionary.Keys.FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
            return actualKey != null;
        }

        private static bool TryToDouble(object value, out double number)
        {
            if (value is double) { number = (double)value; return true; }
            if (value is decimal) { number = (double)(decimal)value; return true; }
            if (value is int) { number = (int)value; return true; }
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }
    }
}
