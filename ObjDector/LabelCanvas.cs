using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using System.Collections.Generic;

namespace ObjDector
{
    public partial class LabelCanvas : Canvas
    {
        public delegate (string name, Brush brush) OnCrateLabelBox(Point startPos);

        public delegate void OnLabelBoxFinished(LabelBox labelBox);

        public MainWindow ParentWindow;

        private bool isNewBox = false;

        public bool IsNewBox { get { return isNewBox; } }

        private LabelBox newBox = null;

        private Point newBoxPos;

        private OnCrateLabelBox _createcb;

        private OnLabelBoxFinished _finishcb;

        public void InitNewLabelBox(OnCrateLabelBox createCb, OnLabelBoxFinished finishCb)
        {
            isNewBox = true;

            _createcb = createCb;
            _finishcb = finishCb;

            Debug.WriteLine("InitNewLabelBox");
        }

        public void CancelNewLabelBox()
        {
            isNewBox = false;
            newBox = null;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Source == this)
            {
                foreach(var item in this.Children)
                {
                    LabelBox box = item as LabelBox;
                    if (box != null)
                    {
                        box.IsShowThumb = false;
                    }
                }
            }

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (isNewBox && newBox == null)
            {
                Debug.WriteLine("OnMouseDown");

                newBoxPos = e.GetPosition(this);
                var (name, brush) = _createcb == null ? ("", Brushes.White) : _createcb(newBoxPos);
                newBox = AddLabelBox(newBoxPos, 0, 0, name, brush);
                newBox.IsHitTestVisible = false;
                return;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (isNewBox && newBox != null)
            {
                Debug.WriteLine("OnMouseUp");

                newBox.IsHitTestVisible = true;
                _finishcb?.Invoke(newBox);
            }

            isNewBox = false;
            newBox = null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!isNewBox || newBox == null)
            {
                return;
            }

            Debug.WriteLine("OnMouseMove");

            var pos = e.GetPosition(this);

            double width = pos.X - newBoxPos.X;
            double height = pos.Y - newBoxPos.Y;

            width = width < 0 ? newBox.Width : width;
            height = height < 0 ? newBox.Height : height;

            newBox.Width = width;
            newBox.Height = height;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (isNewBox && newBox != null)
            {
                Debug.WriteLine("OnMouseLeave");

                newBox.IsHitTestVisible = true;
                _finishcb?.Invoke(newBox);
            }

            isNewBox = false;
            newBox = null;
        }

        /// <summary>
        /// Add label to canvas via normalized rectangle
        /// </summary>
        /// <param name="rect">Label rectangle</param>
        /// <param name="label">The name of the label</param>
        /// <param name="brush">The brushes for the label background</param>
        /// <returns></returns>
        public LabelBox AddUniLabelBox(Rect rect, string label, Brush brush)
        {
            Point pos = rect.Location;
            pos.X *= ActualWidth;
            pos.Y *= ActualHeight;

            double w = rect.Width * ActualWidth;
            double h = rect.Height * ActualHeight;

            return AddLabelBox(pos, w, h, label, brush);
        }

        /// <summary>
        /// Add a label to the canvas
        /// </summary>
        /// <param name="pos">coordinate of Top Left</param>
        /// <param name="width">Width of the box</param>
        /// <param name="height">Height of the box</param>
        /// <param name="label">The name of the label</param>
        /// <param name="brushes">The brushes for the label background</param>
        /// <returns>The label box</returns>
        public LabelBox AddLabelBox(Point pos, double width, double height, string label, Brush brushes)
        {
            var rect = new System.Windows.Shapes.Rectangle();
            rect.Fill = brushes;
            rect.Opacity = 0.5;
            rect.IsHitTestVisible = false;

            var text = new System.Windows.Controls.Label();
            text.Content = label;
            text.Foreground = new SolidColorBrush(Colors.White);
            text.FontSize = 14;
            text.FontWeight = FontWeights.Bold;
            text.IsHitTestVisible = false;

            var grid = new Grid();
            grid.Children.Add(rect);
            grid.Children.Add(text);
            grid.IsHitTestVisible = false;

            var item = new LabelBox();

            item.Template = ParentWindow.Resources["DesignerItemTemplate"] as ControlTemplate;
            item.Width = width;
            item.Height = height;

            item.Content = grid;
            this.Children.Add(item);

            LabelCanvas.SetLeft(item, pos.X);
            LabelCanvas.SetTop(item, pos.Y);

            Canvas.SetZIndex(item, this.Children.Count);
            return item;
        }

        public List<LabelBox> GetSelectedBox()
        {
            var selected = new List<LabelBox>();
            foreach (var item in this.Children)
            {
                LabelBox box = item as LabelBox;
                if (box != null && box.IsShowThumb)
                {
                    selected.Add(box);
                }
            }
            return selected;
        }

        public void RemoveAllLabel()
        {
            this.Children.Clear();
        }

        public void RemoveLabelBox(LabelBox label)
        {
            if (this.Children.Contains(label))
            {
                this.Children.Remove(label);
            }
        }
    }
}
