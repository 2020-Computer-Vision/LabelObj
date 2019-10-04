using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ObjDector
{
    public class MoveThumb : Thumb
    {
        public MoveThumb()
        {
            DragDelta += new DragDeltaEventHandler(this.MoveThumb_DragDelta);
        }

        private void MoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            LabelBox item = this.DataContext as LabelBox;

            if (item != null)
            {
                item.IsShowThumb = true;

                double left = Canvas.GetLeft(item) + e.HorizontalChange;
                double top = Canvas.GetTop(item) + e.VerticalChange;

                Canvas parent = Helper.FindParent<Canvas>(item);

                left = left < 0 ? 0 : left + item.Width > parent.ActualWidth 
                    ? parent.ActualWidth - item.Width : left;

                top = top < 0 ? 0 : top + item.Height > parent.ActualHeight 
                    ? parent.ActualHeight - item.Height : top;

                Canvas.SetLeft(item, left);
                Canvas.SetTop(item, top);
            }
        }
    }
}
