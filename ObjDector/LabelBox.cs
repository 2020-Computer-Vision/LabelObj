using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ObjDector
{
    public class LabelBox : ContentControl
    {
        public bool IsShowThumb
        {
            get { return (Visibility)GetValue(IsShowThumbProperty) == Visibility.Visible; }
            set { SetValue(IsShowThumbProperty, value ? Visibility.Visible : Visibility.Hidden); }
        }

        public static readonly DependencyProperty IsShowThumbProperty =
          DependencyProperty.Register("IsShowThumb", typeof(Visibility), typeof(LabelBox));

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (e.OriginalSource.GetType() == typeof(Rectangle))
            {
                IsShowThumb = !IsShowThumb;
            }
        }
    }
}
