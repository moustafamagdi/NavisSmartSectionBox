// Compiler-only stubs. This file is deliberately excluded from SmartSectionBox.csproj.
using System.Collections.Generic;

namespace Autodesk.Navisworks.Api
{
    [System.Flags]
    public enum KeyModifiers { None = 0, Shift = 1, Alt = 2, Ctrl = 4, DoubleClick = 8 }
    public enum Cursor { Unhandled, Handled, HyperHand }
    public enum ViewRedrawRequests { Render }
    public enum ViewpointProjection { Perspective, Orthographic }
    public enum Tool { None, Select, CustomToolPlugin }

    public class Point3D
    {
        public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class Rotation3D
    {
        // Navisworks documents quaternion components as A, B, C, and D.
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
        public double D { get; set; } = 1.0;
    }

    public sealed class Matrix3 : System.IDisposable
    {
        private readonly double[,] values = new double[3, 3];

        public Matrix3(Rotation3D rotation)
        {
            // The synthetic camera uses A/B/C/D as x/y/z/w. This mirrors the documented
            // Matrix3(Rotation3D) path without exposing a raw component convention to tests.
            var x = rotation.A;
            var y = rotation.B;
            var z = rotation.C;
            var w = rotation.D;
            var length = System.Math.Sqrt(x * x + y * y + z * z + w * w);
            if (length < 1e-12) { values[0, 0] = values[1, 1] = values[2, 2] = 1.0; return; }
            x /= length; y /= length; z /= length; w /= length;
            values[0, 0] = 1 - 2 * (y * y + z * z);
            values[0, 1] = 2 * (x * y - z * w);
            values[0, 2] = 2 * (x * z + y * w);
            values[1, 0] = 2 * (x * y + z * w);
            values[1, 1] = 1 - 2 * (x * x + z * z);
            values[1, 2] = 2 * (y * z - x * w);
            values[2, 0] = 2 * (x * z - y * w);
            values[2, 1] = 2 * (y * z + x * w);
            values[2, 2] = 1 - 2 * (x * x + y * y);
        }

        public double Get(int row, int column) { return values[row, column]; }
        public void Dispose() { }
    }

    public class ProjectionResult
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Depth { get; set; }
    }

    public class Viewpoint
    {
        public double VerticalExtentAtFocalDistance { get; set; }
        public double HorizontalExtentAtFocalDistance { get; set; }
        public double FocalDistance { get; set; }
        public bool HasFocalDistance { get; set; } = true;
        public Point3D Position { get; set; }
        public Rotation3D Rotation { get; set; } = new Rotation3D();
        public ViewpointProjection Projection { get; set; }
    }

    public class View
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public virtual ProjectionResult ProjectPoint(Point3D point, bool sectionClip, bool frustumClip) { return new ProjectionResult(); }
        public virtual Viewpoint CreateViewpointCopy() { return new Viewpoint(); }
        public virtual string GetClippingPlanes() { return string.Empty; }
        public virtual bool TrySetClippingPlanes(string json) { return true; }
        public virtual void RequestDelayedRedraw(ViewRedrawRequests requests) { }
    }

    public class Color
    {
        public static Color FromByteRGB(byte r, byte g, byte b) { return new Color(); }
    }

    public class Graphics
    {
        public void Color(Color color, double opacity) { }
        public void DepthTest(bool enabled) { }
        public void DepthMask(bool enabled) { }
        public void LineWidth(double width) { }
        public void Line(Point3D start, Point3D end) { }
        public void Triangle(Point3D point1, Point3D point2, Point3D point3, bool filled) { }
    }

    public class BoundingBox3D
    {
        public Point3D Min { get; set; } = new Point3D(0, 0, 0);
        public Point3D Max { get; set; } = new Point3D(0, 0, 0);
    }

    public class ModelItem { public virtual BoundingBox3D BoundingBox() { return new BoundingBox3D(); } }
    public class ModelItemCollection : List<ModelItem> { }
    public class DocumentCurrentSelection { public ModelItemCollection SelectedItems { get; set; } = new ModelItemCollection(); }
    public class Model { public ModelItem RootItem { get; set; } }
    public class DocumentTool
    {
        public string CustomToolPluginId { get; set; }
        public Tool Value { get; set; } = Tool.Select;
        public void SetCustomToolPlugin(Autodesk.Navisworks.Api.Plugins.ToolPlugin plugin)
        {
            Value = Tool.CustomToolPlugin;
        }
    }
    public class Document
    {
        public bool IsClear { get; set; }
        public View ActiveView { get; set; }
        public DocumentCurrentSelection CurrentSelection { get; set; } = new DocumentCurrentSelection();
        public List<Model> Models { get; set; } = new List<Model>();
        public string FileName { get; set; }
        public string Title { get; set; }
        public DocumentTool Tool { get; set; } = new DocumentTool();
    }

    public static class Application
    {
        public static Document ActiveDocument { get; set; }
        public static Document MainDocument { get; set; }
        public static bool IsAutomated { get; set; }
        public static PluginManager Plugins { get; set; } = new PluginManager();
    }
}

namespace Autodesk.Navisworks.Api
{
    public class PluginManager { public Autodesk.Navisworks.Api.Plugins.PluginRecord FindPlugin(string id) { return null; } }
    public static class StubApplicationExtensions { }
}

namespace Autodesk.Navisworks.Api.Plugins
{
    using System;
    using Autodesk.Navisworks.Api;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PluginAttribute : Attribute
    {
        public PluginAttribute(string name, string developerId) { }
        public string DisplayName { get; set; }
        public string ToolTip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DockPanePluginAttribute : Attribute
    {
        public DockPanePluginAttribute(int width, int height) { }
        public bool FixedSize { get; set; }
    }

    public class PluginRecord
    {
        public bool IsEnabled { get; set; }
        public object LoadedPlugin { get; set; }
        public object LoadPlugin() { return LoadedPlugin; }
    }

    public class ToolPluginRecord : PluginRecord
    {
        public new ToolPlugin LoadPlugin() { return LoadedPlugin as ToolPlugin; }
    }
    public class DockPanePluginRecord : PluginRecord { }

    public class DockPanePlugin
    {
        public bool Visible { get; set; }
        public virtual System.Windows.Forms.Control CreateControlPane() { return null; }
        public virtual void DestroyControlPane(System.Windows.Forms.Control pane) { }
        public virtual void OnVisibleChanged() { }
        public void ActivatePane() { }
    }

    public class ToolPlugin
    {
        public virtual bool MouseDown(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseMove(View view, KeyModifiers modifiers, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseDrag(View view, KeyModifiers modifiers, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseUp(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseLeave(View view, double timeOffset) { return false; }
        public virtual bool KeyDown(View view, KeyModifiers modifier, ushort key, double timeOffset) { return false; }
        public virtual Cursor GetCursor(View view, KeyModifiers modifier) { return Cursor.Unhandled; }
        public virtual void OverlayRender(View view, Autodesk.Navisworks.Api.Graphics graphics) { }
        public virtual void Render(View view, Autodesk.Navisworks.Api.Graphics graphics) { }
    }
}
