﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace SharpGraphLib
{
    public partial class GraphViewer : ScaledViewerBase
    {
        const int kPointMarkerSize = 10;
        public GraphViewer()
        {
            InitializeComponent();
        }

        List<DisplayedGraph> _Graphs = new List<DisplayedGraph>();

        public IEnumerable<DisplayedGraph> DisplayedGraphs
        {
            get
            {
                return _Graphs;
            }
        }

        public bool HasNoGraphs { get { return _Graphs.Count == 0; } }

        public ContextMenuStrip EmbeddedLegendContextMenu
        {
            set
            {
                if (_EmbeddedLegend != null)
                    _EmbeddedLegend.ContextMenuStrip = value;
            }
        }

        public DisplayedGraph EmbeddedLegendSelectedGraph
        {
            get
            {
                if (_EmbeddedLegend == null)
                    return null;
                return (DisplayedGraph)_EmbeddedLegend.ActiveItem;
            }
        }

        LegendControl _Legend, _EmbeddedLegend;
        bool _EmbeddedLegendMoved = false;

        [Category("Appearance")]
        public bool EmbeddedLegend
        {
            get { return _EmbeddedLegend != null; }
            set 
            {
                if (value == (_EmbeddedLegend != null))
                    return;
                if (value)
                {
                    _EmbeddedLegend = new LegendControl();
                    _EmbeddedLegend.Cursor = Cursors.SizeAll;
                    _EmbeddedLegend.Visible = true;
                    _EmbeddedLegend.Autosize = true;
                    _EmbeddedLegend.Parent = this;
                    _EmbeddedLegend._GraphViewer = this;
                    _EmbeddedLegend.BorderStyle = BorderStyle.FixedSingle;
                    _EmbeddedLegend.UpdateLegend();
                    _EmbeddedLegend.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                    _EmbeddedLegend.OnLabelHilighted += new LegendControl.HandleLabelHilighted(_Legend_OnLabelHilighted);
                    _EmbeddedLegend.OnLabelGrayed += new LegendControl.HandleLabelGrayed(_Legend_OnLabelGrayed);
                    _EmbeddedLegend.MouseMove += new MouseEventHandler(_EmbeddedLegend_MouseMove);
                    _EmbeddedLegend.MouseDown += new MouseEventHandler(_EmbeddedLegend_MouseDown);
                    UpdateScaling();
                    UpdateLegend();
                }
                else
                {
                    _EmbeddedLegend.Dispose();
                    _EmbeddedLegend = null;
                }
            }
        }

        Point _EmbeddedLegendDragBase;

        void _EmbeddedLegend_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _EmbeddedLegendDragBase = e.Location;
        }

        void _EmbeddedLegend_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point newScreenPoint = _EmbeddedLegend.PointToScreen(e.Location);
                newScreenPoint.Offset(-_EmbeddedLegendDragBase.X, -_EmbeddedLegendDragBase.Y);
                Point newLoc = PointToClient(newScreenPoint);

                if (newLoc.X < DataRectangle.Left)
                    newLoc.X = DataRectangle.Left;
                if (newLoc.Y < DataRectangle.Top)
                    newLoc.Y = DataRectangle.Top;
                if (newLoc.X >= (DataRectangle.Right - _EmbeddedLegend.Width))
                    newLoc.X = DataRectangle.Right - _EmbeddedLegend.Width;
                if (newLoc.Y >= (DataRectangle.Bottom - _EmbeddedLegend.Height))
                    newLoc.Y = DataRectangle.Bottom - _EmbeddedLegend.Height;

                _EmbeddedLegend.Location = newLoc;
                _EmbeddedLegendMoved = true;
            }
        }

        [Category("Legend")]
        public SharpGraphLib.LegendControl Legend
        {
            get { return _Legend; }
            set 
            {
                if (_Legend != null)
                {
                    _Legend._GraphViewer = null;
                    _Legend.UpdateLegend();
                    _Legend.OnLabelHilighted -= _Legend_OnLabelHilighted;
                    _Legend.OnLabelGrayed -= _Legend_OnLabelGrayed;
                }

                _Legend = value;

                if (_Legend != null)
                {
                    _Legend._GraphViewer = this;
                    _Legend.UpdateLegend();
                    _Legend.OnLabelHilighted += new LegendControl.HandleLabelHilighted(_Legend_OnLabelHilighted);
                    _Legend.OnLabelGrayed += new LegendControl.HandleLabelGrayed(_Legend_OnLabelGrayed);
                }
            }
        }

        void _Legend_OnLabelGrayed(ILegendItem graph, bool grayed)
        {
            foreach(DisplayedGraph gr in _Graphs)
                if (gr.Hint == graph.Hint)
                    gr.Hidden = grayed;
        }

        GraphViewer.DisplayedGraph _HighlightedGraph;

        void _Legend_OnLabelHilighted(ILegendItem graph)
        {
            _HighlightedGraph = null;
            if (graph != null)
            foreach (DisplayedGraph gr in _Graphs)
                if (gr.Hint == graph.Hint)
                    _HighlightedGraph = gr;
            Invalidate();
        }

        public class DisplayedGraph : ILegendItem
        {
            public enum PointMarkingStyle
            {
                Undefined,
                None,
                Square,
                Circle,
            }

            public object Tag;
            GraphViewer _Viewer;
            Graph _Graph;
            Color _Color;
            string _Hint;
            int _LineWidth;
            bool _Hidden;
            PointMarkingStyle _DefaultPointMarkingStyle = PointMarkingStyle.None;

            PointMarkingStyle[] _PointMarkerOverride;

            public class DisplayedPoint
            {
                DisplayedGraph _Graph;
                int _PointIndex;

                internal DisplayedPoint(DisplayedGraph gr, int index)
                {
                    _Graph = gr;
                    _PointIndex = index;
                }

                public double X
                {
                    get { return _Graph.Graph.GetPointByIndex(_PointIndex).Key; }
                }

                public double Y
                {
                    get { return _Graph.Graph.GetPointByIndex(_PointIndex).Value; }
                }

                public DisplayedGraph Graph { get { return _Graph; } }

                public PointMarkingStyle MarkerStyle
                {
                    get
                    {
                        if ((_Graph._PointMarkerOverride == null) || (_Graph._PointMarkerOverride.Length >= _PointIndex))
                            return _Graph.DefaultPointMarkingStyle;
                        return _Graph._PointMarkerOverride[_PointIndex];
                    }
                    set
                    {
                        if ((_Graph._PointMarkerOverride == null) || (_Graph._PointMarkerOverride.Length != _Graph.Graph.SortedPoints.Count))
                            _Graph._PointMarkerOverride = new PointMarkingStyle[_Graph.Graph.SortedPoints.Count];
                        _Graph._PointMarkerOverride[_PointIndex] = value;
                        _Graph._Viewer.Invalidate();
                    }
                }
            }

            #region Properties
            public PointMarkingStyle DefaultPointMarkingStyle
            {
                get { return _DefaultPointMarkingStyle; }
                set { _DefaultPointMarkingStyle = value; _Viewer.Invalidate(); }
            }

            public int LineWidth
            {
                get { return _LineWidth; }
                set { _LineWidth = value; _Viewer.Invalidate(); }
            }

            public string Hint
            {
                get { return _Hint; }
                set { _Hint = value; _Viewer.UpdateScaling(); }
            }
            
            public Color Color
            {
                get { return _Color; }
                set { _Color = value; _Viewer.Invalidate(); }
            }

            public bool Hidden
            {
                get { return _Hidden; }
                set { _Hidden = value; _Viewer.Invalidate(); }
            }

            public Graph Graph { get { return _Graph; } }
            #endregion
            

            internal DisplayedGraph(GraphViewer viewer, Graph graph, Color color, int lineWidth, string hint)
            {
                _Viewer = viewer;
                _Graph = graph;
                _Color = color;
                _Hint = hint;
                _LineWidth = lineWidth;
            }

            internal GraphicsPath RebuildPath()
            {
                GraphicsPath path = new GraphicsPath();
                if (_Graph.SortedPoints.Count > 0)
                {
                    Point[] points = new Point[_Graph.PointCount];
                    int idx = 0;

                    foreach (KeyValuePair<double, double> kv in _Graph.SortedPoints)
                        points[idx++] = new Point(_Viewer.MapX(kv.Key, true), _Viewer.MapY(kv.Value, true));

                    path.AddLines(points);
                }
                return path;
            }

            public DisplayedPoint FindPoint(double X, double Y)
            {
                return FindPoint(X, Y, 10);
            }

            public DisplayedPoint FindPoint(double X, double Y, int maxRadius)
            {
                int bestPoint = 0;
                double bestDist = double.MaxValue;
                double maxDistX = _Viewer.UnmapWidth(maxRadius), maxDistY = _Viewer.UnmapHeight(maxRadius);
                
                for (int i = 0; i < _Graph.SortedPoints.Count; i++)
                {
                    KeyValuePair<double, double> kv = _Graph.GetPointByIndex(i);
                    double distX = (X - kv.Key) / maxDistX;
                    double distY = (Y - kv.Value) / maxDistY;
                    double dist = distX * distX + distY * distY;
                    
                    if (dist < bestDist)
                    {
                        bestPoint = i;
                        bestDist = dist;
                    }
                    if (kv.Key > X)
                        break;
                }

                if (bestDist < 1)
                    return new DisplayedPoint(this, bestPoint);

                return null;
            }

            public double GetLinearlyInterpolatedY(double X)
            {
                int unused;
                return GetLinearlyInterpolatedY(X, out unused);
            }

            public double GetLinearlyInterpolatedY(double X, out int nearestRefPoint)
            {
                nearestRefPoint = 0;
                for (int i = 0; i < _Graph.SortedPoints.Count; i++)
                {
                    KeyValuePair<double, double> kv = _Graph.GetPointByIndex(i);
                    if (kv.Key < X)
                        continue;
                    if (kv.Key == X)
                        return kv.Value;
                    if (i == 0)
                        return double.NaN;   //TODO: support extrapolation
                    
                    KeyValuePair<double, double> kv0 = _Graph.GetPointByIndex(i - 1);

                    double dx = kv.Key - kv0.Key;
                    double dy = kv.Value - kv0.Value;

                    if ((X - kv0.Key) < (kv.Key - X))
                        nearestRefPoint = i - 1;
                    else
                        nearestRefPoint = i;

                    return kv0.Value + ((X - kv0.Key) * dy / dx);
                }
                return double.NaN;
            }

            public void ResetPointMarkers()
            {
                _PointMarkerOverride = null;
                _Viewer.Invalidate();
            }

            public PointMarkingStyle GetStyleForPoint(int pointIdx)
            {
                if ((_PointMarkerOverride == null) || (_PointMarkerOverride.Length <= pointIdx))
                    return _DefaultPointMarkingStyle;
                return _PointMarkerOverride[pointIdx];
            }
        }

        public void ResetGraphs()
        {
            _Graphs.Clear();
            UpdateScaling();
            Invalidate();
            UpdateLegend();
        }

        public DisplayedGraph AddGraph(Graph graph, Color color, int lineWidth, string hint)
        {
            DisplayedGraph gr = new DisplayedGraph(this, graph, color, lineWidth, hint);
            _Graphs.Add(gr);
            UpdateScaling();
            Invalidate();
            UpdateLegend();
            return gr;
        }

        public void RemoveGraph(DisplayedGraph graph)
        {
            _Graphs.Remove(graph);
            UpdateScaling();
            Invalidate();
            UpdateLegend();
        }
        public class InterpolatedPoint
        {
            double _X, _Y;
            DisplayedGraph _Graph;
            int _NearestRefPoint;
            
            public double X
            {
                get { return _X; }
            }

            public double Y
            {
                get { return _Y; }
            }

            public SharpGraphLib.GraphViewer.DisplayedGraph Graph
            {
                get { return _Graph; }
            }

            public DisplayedGraph.DisplayedPoint NearestReferencePoint
            {
                get { return new DisplayedGraph.DisplayedPoint(_Graph, _NearestRefPoint); }
            }

            internal InterpolatedPoint(double x, double y, int nearestRefPoint, DisplayedGraph graph)
            {
                _X = x;
                _Y = y;
                _NearestRefPoint = nearestRefPoint;
                _Graph = graph;
            }
        }

        public int MapAndSquareDistance(double X1, double X2, double Y1, double Y2)
        {
            int dX = MapX(X1, true) - MapX(X2, true), dY = MapY(Y1, true) - MapY(Y2, true);
            return dX * dX + dY * dY;
        }

        public InterpolatedPoint FindNearestGraphPoint(double x, double y, bool ignoreHiddenGraphs, out int distanceSquare)
        {
            int bestDistanceSquare = int.MaxValue;
            InterpolatedPoint bestPoint = null;

            foreach (DisplayedGraph gr in _Graphs)
            {
                if (ignoreHiddenGraphs && gr.Hidden)
                    continue;

                int nearestRefPoint;
                double yFound = gr.GetLinearlyInterpolatedY(x, out nearestRefPoint);
                if (double.IsNaN(yFound))
                    continue;
                KeyValuePair<double, double> kv = gr.Graph.GetPointByIndex(nearestRefPoint);
                int thisDistanceSquare = MapAndSquareDistance(x, x, y, yFound);
                if (thisDistanceSquare < bestDistanceSquare)
                {
                    bestDistanceSquare = thisDistanceSquare;
                    bestPoint = new InterpolatedPoint(x, yFound, nearestRefPoint, gr);
                }
            }
            distanceSquare = bestDistanceSquare;
            return bestPoint;
        }

        public DisplayedGraph.DisplayedPoint FindNearestReferencePoint(double x, bool ignoreHiddenGraphs)
        {
            double bestDist = int.MaxValue;
            DisplayedGraph.DisplayedPoint bestPoint = null;

            foreach (DisplayedGraph gr in _Graphs)
            {
                if (ignoreHiddenGraphs && gr.Hidden)
                    continue;

                int nearestRefPoint;
                double yFound = gr.GetLinearlyInterpolatedY(x, out nearestRefPoint);
                if (double.IsNaN(yFound))
                    continue;
                KeyValuePair<double, double> kv = gr.Graph.GetPointByIndex(nearestRefPoint);
                double thisDist = Math.Abs(kv.Key - x);
                if (thisDist < bestDist)
                {
                    bestDist = thisDist;
                    bestPoint = new DisplayedGraph.DisplayedPoint(gr, nearestRefPoint);
                }
            }
            return bestPoint;
        }

        public DisplayedGraph.DisplayedPoint FindNearestReferencePoint(double x, double y, bool ignoreHiddenGraphs)
        {
            double bestDist = int.MaxValue;
            DisplayedGraph.DisplayedPoint bestPoint = null;

            foreach (DisplayedGraph gr in _Graphs)
            {
                if (ignoreHiddenGraphs && gr.Hidden)
                    continue;

                int nearestRefPoint;
                double yFound = gr.GetLinearlyInterpolatedY(x, out nearestRefPoint);
                if (double.IsNaN(yFound))
                    continue;
                KeyValuePair<double, double> kv = gr.Graph.GetPointByIndex(nearestRefPoint);
                double w = MapWidth(Math.Abs(kv.Key - x)), h = MapHeight(Math.Abs(kv.Value - y));
                double thisDist = w * w + h * h;
                if (thisDist < bestDist)
                {
                    bestDist = thisDist;
                    bestPoint = new DisplayedGraph.DisplayedPoint(gr, nearestRefPoint);
                }
            }
            return bestPoint;
        }

        protected override void GetRawDataBounds(out GraphBounds nonTransformedBounds, out GraphBounds transformedBounds)
        {
            if (_Graphs.Count == 0)
            {
                base.GetRawDataBounds(out nonTransformedBounds, out transformedBounds);
                return;
            }
            nonTransformedBounds = GraphBounds.CreateInitial();
            transformedBounds = GraphBounds.CreateInitial();
            foreach (DisplayedGraph gr in _Graphs)
            {
                foreach (KeyValuePair<double, double> kv in gr.Graph.SortedPoints)
                {
                    double x = kv.Key;
                    nonTransformedBounds.MinX = Math.Min(nonTransformedBounds.MinX, x);
                    nonTransformedBounds.MaxX = Math.Max(nonTransformedBounds.MaxX, x);
                    DoTransformX(ref x);
                    if (!double.IsInfinity(x) && !double.IsNaN(x))
                    {
                        transformedBounds.MinX = Math.Min(transformedBounds.MinX, x);
                        transformedBounds.MaxX = Math.Max(transformedBounds.MaxX, x);
                    }
                    double y = kv.Value;
                    nonTransformedBounds.MinY = Math.Min(nonTransformedBounds.MinY, y);
                    nonTransformedBounds.MaxY = Math.Max(nonTransformedBounds.MaxY, y);
                    DoTransformY(ref y);
                    if (!double.IsInfinity(y) && !double.IsNaN(y))
                    {
                        transformedBounds.MinY = Math.Min(transformedBounds.MinY, y);
                        transformedBounds.MaxY = Math.Max(transformedBounds.MaxY, y);
                    }
                }

            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(DataRectangle);
            foreach (DisplayedGraph gr in _Graphs)
            {
                if (gr.Hidden)
                    continue;
                int lineWidth = gr.LineWidth;
                if (_HighlightedGraph == gr)
                    lineWidth += 2;
                Pen pen = new Pen(gr.Color, lineWidth);
                e.Graphics.DrawPath(pen, gr.RebuildPath());

                int idx = 0;
                foreach (KeyValuePair<double, double> kv in gr.Graph.SortedPoints)
                {
                    DisplayedGraph.PointMarkingStyle style = gr.GetStyleForPoint(idx++);

                    if (style == DisplayedGraph.PointMarkingStyle.None)
                        continue;

                    Pen pointMarkerPen = Pens.Black;
                    int x = MapX(kv.Key, true), y = MapY(kv.Value, true);
                    switch (style)
                    {
                        case DisplayedGraph.PointMarkingStyle.Square:
                            e.Graphics.DrawRectangle(pointMarkerPen, x - kPointMarkerSize / 2, y - kPointMarkerSize / 2, kPointMarkerSize, kPointMarkerSize); 
                            break;
                        case DisplayedGraph.PointMarkingStyle.Circle:
                            e.Graphics.DrawEllipse(pointMarkerPen, x - kPointMarkerSize / 2, y - kPointMarkerSize / 2, kPointMarkerSize, kPointMarkerSize);
                            break;
                    }

                }
            }
            e.Graphics.ResetClip();
        }

        internal void UpdateLegend()
        {
            if (_Legend != null)
                _Legend.UpdateLegend();
            if (_EmbeddedLegend != null)
            {
                _EmbeddedLegend.UpdateLegend();
                if (!_EmbeddedLegendMoved)
                {
                    _EmbeddedLegend.Left = DataRectangle.Right - _EmbeddedLegend.Width - 10;
                    _EmbeddedLegend.Top = DataRectangle.Bottom - _EmbeddedLegend.Height - 10;
                }
            }
        }

    }
}
