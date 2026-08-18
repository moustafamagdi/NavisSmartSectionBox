// Compiler-only stubs. This file is deliberately excluded from SmartSectionBox.csproj.
using System.Collections.Generic;

namespace Autodesk.Navisworks.Api
{
    [System.Flags]
    public enum KeyModifiers { None = 0, Shift = 1, Alt = 2, Ctrl = 4, DoubleClick = 8 }
    public enum Cursor { Unhandled, Handled, HyperHand }
    public enum ViewRedrawRequests { Render }
    public enum ViewpointProjection { Perspective, Orthographic }

    public class Point3D
    {
        public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
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
        public double FocalDistance { get; set; }
        public Point3D Position { get; set; }
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

    public class BoundingBox3D
    {
        public Point3D Min { get; set; } = new Point3D(0, 0, 0);
        public Point3D Max { get; set; } = new Point3D(0, 0, 0);
    }

    public class ModelItem { public virtual BoundingBox3D BoundingBox() { return new BoundingBox3D(); } }
    public class ModelItemCollection : List<ModelItem> { }
    public class DocumentCurrentSelection { public ModelItemCollection SelectedItems { get; set; } = new ModelItemCollection(); }
    public class Model { public ModelItem RootItem { get; set; } }
    public class DocumentTool { }
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

    public class ToolPluginRecord : PluginRecord { }
    public class DockPanePluginRecord : PluginRecord { }

    public class ToolPlugin
    {
        public virtual bool MouseDown(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseMove(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseUp(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset) { return false; }
        public virtual bool MouseLeave(View view, double timeOffset) { return false; }
        public virtual bool KeyDown(View view, KeyModifiers modifier, ushort key, double timeOffset) { return false; }
        public virtual Cursor GetCursor(View view, KeyModifiers modifier) { return Cursor.Unhandled; }
        public virtual void OverlayRender(View view, Graphics graphics) { }
    }

    public class Graphics { }
}
