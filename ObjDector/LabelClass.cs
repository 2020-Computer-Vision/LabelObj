using System.ComponentModel;
using System.Windows.Controls;

namespace ObjDector
{
    public class LabelClass : INotifyPropertyChanged
    {
        /// <summary>
        /// Shortcut Id
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
        /// Class name
        /// </summary>
        private string classname;

        public string Classname
        {
            get { return classname; }
            set {
                classname = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Classname"));
            }
        }

        public LabelClass(string name)
        {
            Classname = name;
        }

        public LabelClass(int _id, string name)
        {
            Id = _id;
            Classname = name;
        }

        public static bool operator ==(LabelClass cls1, string str)
        {
            return cls1.Classname == str;
        }

        public static bool operator !=(LabelClass cls1, string str)
        {
            return cls1.Classname != str;
        }

        public static bool operator ==(LabelClass cls1, LabelClass cls2)
        {
            return cls1.Equals(cls2);
        }

        public static bool operator !=(LabelClass cls1, LabelClass cls2)
        {
            return !cls1.Equals(cls2);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            LabelClass other = obj as LabelClass;
            return Classname == other.Classname;
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
