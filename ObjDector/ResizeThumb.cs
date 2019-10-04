using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ObjDector
{
    public class ResizeThumb : Thumb
    {
        public ResizeThumb()
        {
            DragDelta += new DragDeltaEventHandler(this.ResizeThumb_DragDelta);
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            Control item = this.DataContext as Control;

            if (item != null)
            {
                Canvas parent = Helper.FindParent<Canvas>(item);

                double left = Canvas.GetLeft(item);
                double top = Canvas.GetTop(item);

                double deltaVertical, deltaHorizontal;

                switch (VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        deltaVertical = Math.Min(-e.VerticalChange, item.ActualHeight - item.MinHeight);
                        deltaVertical = item.Height - deltaVertical + top > parent.ActualHeight ? 0.0 : deltaVertical;
                        item.Height -= deltaVertical;
                        break;
                    case VerticalAlignment.Top:
                        deltaVertical = Math.Min(e.VerticalChange, item.ActualHeight - item.MinHeight);
                        deltaVertical = top + deltaVertical < 0.0 ? 0.0 : deltaVertical;
                        Canvas.SetTop(item, top + deltaVertical);
                        item.Height -= deltaVertical;
                        break;
                    default:
                        break;
                }

                switch (HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        deltaHorizontal = Math.Min(e.HorizontalChange, item.ActualWidth - item.MinWidth);
                        deltaHorizontal = left + deltaHorizontal < 0.0 ? 0.0 : deltaHorizontal;
                        Canvas.SetLeft(item, left + deltaHorizontal);
                        item.Width -= deltaHorizontal;
                        break;
                    case HorizontalAlignment.Right:
                        deltaHorizontal = Math.Min(-e.HorizontalChange, item.ActualWidth - item.MinWidth);
                        deltaHorizontal = item.Width - deltaHorizontal + left > parent.ActualWidth ? 0.0 : deltaHorizontal;
                        item.Width -= deltaHorizontal;
                        break;
                    default:
                        break;
                }
            }

            e.Handled = true;
        }
    }
}
