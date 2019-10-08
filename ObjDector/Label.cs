using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ObjDector
{
    public class Label : INotifyPropertyChanged
    {
        /// <summary>
        /// Source Id
        /// </summary>
        private int id;

        public int Id
        {
            get { return id; }
            set {
                id = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Id"));
            }
        }

        /// <summary>
        /// Object class name
        /// </summary>
        private string name;

        public string Name
        {
            get { return name; }
            set {
                name = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Name"));
            }
        }

        /// <summary>
        /// Coordinates of the of the object on the image, the ratio to the shape of the image
        /// </summary>
        public double X, Y;

        /// <summary>
        /// Width and height of bounding box, the ratio to the shape of the image
        /// </summary>
        public double Width, Height;

        public LabelBox Box;

        public Label()
        {
        }

        /// <summary>
        /// Deepcopy the Label
        /// </summary>
        /// <param name="label"></param>
        public Label(Label label)
        {
            Id = label.Id;
            Name = label.Name;
            X = label.X;
            Y = label.Y;
            Width = label.Width;
            Height = label.Height;
        }

        public Rect GetCanvasRect()
        {
            return new Rect(X - 0.5 * Width, Y - 0.5 * Height, Width, Height);
        }

        public override string ToString()
        {
            return $"{Id} {Name} {X} {Y} {Width} {Height}";
        }

        public static bool operator ==(Label label1, Label label2)
        {
            return label1.Equals(label2);
        }

        public static bool operator !=(Label label1, Label label2)
        {
            return !label1.Equals(label2);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Label other = obj as Label;
            return Id == other.Id && Name == other.Name
                && X == other.X && Y == other.Y
                && Width == other.Width && Height == other.Height;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
    }
}
