using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Brushes = System.Windows.Media.Brushes;

namespace ObjDector
{
    public partial class MainWindow : Window
    {
        private delegate bool HotkeyEventEventHandler(string name);

        private MTObservableCollection<Label> labelsList = new MTObservableCollection<Label>();

        private List<Label> persistentLabels = new List<Label>();

        private List<Label> clipboardLabels = new List<Label>();

        private MTObservableCollection<LabelClass> classesList = new MTObservableCollection<LabelClass>();

        private static int CACHE_SIZE = 30;

        private MediaFile mediaFile = null;

        private static List<SolidColorBrush> ColorSchem = new List<SolidColorBrush> {
            Brushes.DodgerBlue,
            Brushes.LimeGreen,
            Brushes.Red,
            Brushes.Cyan,
            Brushes.MediumPurple,
            Brushes.Yellow,
        };

        private Dictionary<string, SolidColorBrush> colors = new Dictionary<string, SolidColorBrush>();

        private int _currentFrame = -1;

        private int currentFrame
        {
            get {
                return _currentFrame;
            }

            set {
                _currentFrame = value;
                frameLable.Text = $"{value}/{(mediaFile == null ? 0 : mediaFile.totalFrames - 1)}";
                frameSlider.Value = value;
                gotoTarget.Text = $"{value}";
            }
        }

        private string _currentLabel;
        private string currentLable
        {
            get { return _currentLabel; }
            set {
                _currentLabel = value;
                currentClass.Text = value;
            }
        }

        private string status
        {
            set {
                statusBox.Text = value;
            }
        }

        private Dictionary<int, Bitmap> decodedFrame = new Dictionary<int, Bitmap>();

        private Dictionary<Key, Tuple<string, HotkeyEventEventHandler, bool>> _hotkeymap = 
            new Dictionary<Key, Tuple<string, HotkeyEventEventHandler, bool>>();

        private System.Windows.Threading.DispatcherTimer autoSaveTimer = null;

        #region Misc Functions
        private void InfoBox(string message)
        {
            status = $"Info: {message}";
            System.Windows.MessageBox.Show(message, this.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WarningBox(string message)
        {
            status = $"Warning: {message}";
            System.Windows.MessageBox.Show(message, this.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ErrorBox(string message)
        {
            status = $"Error: {message}";
            System.Windows.MessageBox.Show(message, this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void HelpBox()
        {
            InfoBox(@"Usage:
""W"": Enter ""New label mode"" with last selected class
""1-9"": Enter ""New label mode"" with class id (Lower right corner)
""ESC"": Cancel ""New label mode""
""D"": Next frame
""A"": Previous frame
""Delete"": Remove all selected labels
""Ctrl+C/V"": Copy and paste selected labels
");
        }

        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);

                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                return bitmapimage;
            }
        }

        private void AddHotKeys(String name, Key key, ModifierKeys mod, HotkeyEventEventHandler handler)
        {
            try
            {
                if (_hotkeymap.ContainsKey(key))
                {
                    throw new Exception($"Key:\"{key}\" was already in used.");
                }
                _hotkeymap[key] = new Tuple<string, HotkeyEventEventHandler, bool>(name, handler, true);
            }
            catch (Exception err)
            {
                ErrorBox("HOTKEY: " + err.Message);
            }
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            autoSaveTimer.Tick += new EventHandler(OnAutoSave);
            autoSaveTimer.Interval = new TimeSpan(0, 0, 5);
            autoSaveTimer.Start();

            labelListView.ItemsSource = labelsList;
            classListView.ItemsSource = classesList;

            AddHotKeys("next", Key.D, ModifierKeys.None, name =>
            {
                GotoFrame(currentFrame + 1);
                return true;
            });
            AddHotKeys("prev", Key.A, ModifierKeys.None, name =>
            {
                GotoFrame(currentFrame - 1);
                return true;
            });

            try
            {
                foreach (string line in File.ReadLines("config.ini"))
                {
                    var tokens = line.Trim().Split(',');
                    if (tokens.Length == 0 || tokens[0].StartsWith("//"))
                    {
                        continue;
                    }

                    AddLabelClass(tokens[0], tokens.Length > 1 && tokens[1] == "1");
                }
            }
            catch (Exception e)
            {
                status = $"Unable to load config file. Error: {e.Message}";
            }

            if (classesList.Count == 0)
            {
                ErrorBox("No class was loaded.");
                AddLabelClass("Label_1");
            }

            currentLable = classesList[0].Classname;

            labelCanvas.ParentWindow = this;
        }

        private void CloseMediaFile()
        {
            if (mediaFile != null)
            {
                mediaFile.Dispose();
                decodedFrame.Clear();
                mediaFile = null;

                frameSlider.Value = 0;
                frameSlider.Maximum = 0;
            }
        }

        private void AddFrameToCache(int number, Bitmap frame, bool removeFirst = false)
        {
            if (decodedFrame.Count > CACHE_SIZE)
            {
                var keys = decodedFrame.Keys.ToList();
                keys.Sort();
                decodedFrame.Remove(keys[removeFirst ? 0 : keys.Count - 1]);
            }

            decodedFrame.Add(number, frame);
        }

        private void GotoFrame(int target)
        {
            if (mediaFile == null || target == currentFrame)
            {
                return;
            }

            if (target < 0)
            {
                gotoTarget.Text = $"{currentFrame}";
                return;
            }

            var persisLabels = new List<Label>(persistentLabels);
            persistentLabels.Clear();

            Bitmap frame;
            if (!decodedFrame.TryGetValue(target, out frame))
            {
                bool forward = target > currentFrame;
                if (target == currentFrame + 1)
                {
                    int fn = mediaFile.GetNextFrame(out frame);
                    if ((fn < 0 || fn != target) && !mediaFile.GetFrame(target, out frame))
                    {
                        ErrorBox($"Cannot decoding next frame #{target}");
                        return;
                    }
                }
                else
                {
                    if (!mediaFile.GetFrame(target, out frame))
                    {
                        ErrorBox($"Cannot decoding frame #{target}");
                        return;
                    }
                }

                AddFrameToCache(target, frame, !forward);
            }

            SaveLabel(currentFrame);
            LoadLabel(target);

            // only auto add when we seek to next frame
            if (target == currentFrame + 1)
            {
                foreach (var label1 in persisLabels)
                {
                    Label label = new Label(label1);
                    Rect rect = label.GetCanvasRect();
                    label.Id = target;
                    label.Box = labelCanvas.AddUniLabelBox(rect, label.Name, colors[label.Name]);
                    label.Box.IsShowThumb = false;
                    labelsList.Add(label);

                    persistentLabels.Add(label);
                }
            }

            videoPlayer.Source = BitmapToImageSource(frame);
            currentFrame = target;
        }

        private void NextFrame_Click(object sender, RoutedEventArgs e)
        {
            GotoFrame(currentFrame + 1);
        }

        private void PrevFrame_Click(object sender, RoutedEventArgs e)
        {
            GotoFrame(currentFrame - 1);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonVideos);
                openFileDialog.Filter = "Video Files (*.mp4)|*.mp4|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                filePath = openFileDialog.FileName;
            }

            CloseMediaFile();

            try
            {
                var res = System.Windows.MessageBox.Show("Attempt to get total number of frames?",
                    "Open new media file", MessageBoxButton.YesNo, MessageBoxImage.Question);
                mediaFile = new MediaFile(filePath, res == MessageBoxResult.Yes);
            }
            catch (Exception ex)
            {
                ErrorBox($"Cannot open file {filePath}: \n" + ex.Message);
                return;
            }

            frameSlider.Maximum = mediaFile.totalFrames - 1;

            Bitmap frame;
            int fn = mediaFile.GetNextFrame(out frame);
            if (fn < 0)
            {
                ErrorBox("Cannot decode first frame");
                CloseMediaFile();
                return;
            }

            if (fn != 0)
            {
                WarningBox("First decoded frame is not the first frame in the sequence");
            }

            AddFrameToCache(fn, frame);

            videoPlayer.Source = BitmapToImageSource(frame);
            currentFrame = fn;

            HelpBox();
        }

        private void FrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            GotoFrame(Convert.ToInt32(frameSlider.Value));
        }

        private void GotoFrame_Click(object sender, RoutedEventArgs e)
        {
            GotoFrame(Convert.ToInt32(gotoTarget.Text));
        }

        private void LabelListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Label label = labelListView.SelectedItem as Label;
            if (label != null)
            {
                GotoFrame(label.Id);
            }
        }

        private void VideoPlayer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(e.Source as UIElement);
            var x = pos.X / videoPlayer.ActualWidth;
            var y = pos.Y / videoPlayer.ActualHeight;

            if (x > 1.0 || y > 1.0)
            {
                x = 0.0;
                y = 0.0;
            }

            mousePos.Text = $"Pos: {x:0.00},{y:0.00}";
        }

        private void OnLabelBoxFinished(LabelBox labelBox)
        {
            Label label = new Label();
            label.Box = labelBox;

            label.Id = currentFrame;
            label.Name = currentLable;

            label.Width = labelBox.ActualWidth / labelCanvas.ActualWidth;
            label.Height = labelBox.ActualHeight / labelCanvas.ActualHeight;

            label.X = Canvas.GetLeft(labelBox) / labelCanvas.ActualWidth + label.Width * 0.5;
            label.Y = Canvas.GetTop(labelBox) / labelCanvas.ActualHeight + label.Height * 0.5;

            labelsList.Add(label);

            if (classesList.Any(x => x.IsPersistent && x == currentLable))
            {
                persistentLabels.Add(label);
            }
        }

        private void CreateNewbox_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mediaFile != null;
        }

        private void CreateNewbox_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            labelCanvas.InitNewLabelBox((System.Windows.Point point) 
                => (currentLable, colors[currentLable]), OnLabelBoxFinished);
            status = $"New label: {currentLable}";
        }

        private void CancelNewbox_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = labelCanvas.IsNewBox;
        }

        private void CancelNewbox_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            labelCanvas.CancelNewLabelBox();
            status = $"New label Canceled";
        }

        private void DeleteBox_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mediaFile != null;
        }
        
        private void DeleteBox_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var box in labelCanvas.GetSelectedBox())
            {
                for (int i = 0; i < labelsList.Count; ++i)
                {
                    if (labelsList[i].Box == box)
                    {
                        labelsList.RemoveAt(i);
                        labelCanvas.RemoveLabelBox(box);
                        break;
                    }
                }
            }
        }

        private void CopyBox_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mediaFile != null;
        }

        private void CopyBox_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            clipboardLabels.Clear();
            foreach (var box in labelCanvas.GetSelectedBox())
            {
                for (int i = 0; i < labelsList.Count; ++i)
                {
                    if (labelsList[i].Box == box)
                    {
                        clipboardLabels.Add(new Label(labelsList[i]));
                    }
                }
            }
            status = $"Copied {clipboardLabels.Count} boxes.";
        }

        private void PasteBox_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = clipboardLabels.Count > 0;
        }

        private void PasteBox_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var label in clipboardLabels)
            {
                Rect rect = label.GetCanvasRect();
                label.Id = currentFrame;
                label.Box = labelCanvas.AddUniLabelBox(rect, label.Name, colors[label.Name]);
                label.Box.IsShowThumb = false;
                labelsList.Add(label);
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = false;
            if (!_hotkeymap.ContainsKey(e.Key))
            {
                return;
            }

            var (name, handler, enabled) = _hotkeymap[e.Key];
            if (handler != null && enabled)
            {
                e.Handled = handler(name);
            }
        }

        private bool OnNewLabelWithClass(string name)
        {
            if (gotoTarget.IsFocused)
            {
                return false;
            }

            if (mediaFile != null)
            {
                currentLable = name;
                labelCanvas.InitNewLabelBox((System.Windows.Point point) 
                    => (currentLable, colors[currentLable]), OnLabelBoxFinished);
                status = $"New label: {currentLable}";
            }
            return true;
        }

        private void ClassListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            currentLable = (classListView.SelectedItem as LabelClass)?.Classname;
        }

        /// <summary>
        /// Save current label box change
        /// </summary>
        /// <param name="frame"></param>
        private void SaveLabel(int frame)
        {
            foreach (var label in labelsList)
            {
                if (label.Id != frame)
                {
                    continue;
                }

                LabelBox labelBox = label.Box;
                if (!labelCanvas.Children.Contains(labelBox))
                {
                    ErrorBox($"Invalid control for label {label.Id}:{label.Name}");
                    continue;
                }

                label.Width = labelBox.ActualWidth / labelCanvas.ActualWidth;
                label.Height = labelBox.ActualHeight / labelCanvas.ActualHeight;

                label.X = Canvas.GetLeft(labelBox) / labelCanvas.ActualWidth + label.Width * 0.5;
                label.Y = Canvas.GetTop(labelBox) / labelCanvas.ActualHeight + label.Height * 0.5;
            }
        }

        /// <summary>
        /// Load label box 
        /// </summary>
        /// <param name="frame">The frame number</param>
        private void LoadLabel(int frame)
        {
            labelCanvas.RemoveAllLabel();

            foreach (var label in labelsList)
            {
                if (label.Id == frame)
                {
                    Rect rect = label.GetCanvasRect();
                    label.Box = labelCanvas.AddUniLabelBox(rect, label.Name, colors[label.Name]);
                    label.Box.IsShowThumb = false;
                }
            }
        }

        private void AddLabelClass(string classname, bool persistent = false)
        {
            int i = classesList.Count;
            // HotkeyManager.Current.AddOrReplace(classname, Key.D1 + i, ModifierKeys.None, OnNewLabelWithClass);
            AddHotKeys(classname, Key.D1 + i, ModifierKeys.None, OnNewLabelWithClass);
            colors[classname] = ColorSchem[i];
            classesList.Add(new LabelClass(++i, classname, persistent));
        }

        private void LoadLabelFile(string filename)
        {
            try
            {
                foreach (string line in File.ReadLines(filename))
                {
                    var tokens = line.Trim().Split(' ');
                    if (tokens.Length < 6 || tokens[0].StartsWith("//"))
                    {
                        continue;
                    }

                    Label label = new Label();
                    label.Id = int.Parse(tokens[0]);
                    label.Name = tokens[1].Trim();
                    label.X = double.Parse(tokens[2]);
                    label.Y = double.Parse(tokens[3]);
                    label.Width = double.Parse(tokens[4]);
                    label.Height = double.Parse(tokens[5]);

                    if (label.Width == 0.0 || label.Height == 0.0)
                    {
                        continue;
                    }
                    
                    if (labelsList.Contains(label))
                    {
                        continue;
                    }
                    labelsList.Add(label);

                    if (classesList.ToList().FindIndex(cls => cls == label.Name) == -1)
                    {
                        AddLabelClass(label.Name);
                    }
                }

                labelListView.ItemsSource = labelsList;
                LoadLabel(currentFrame);
            }
            catch (Exception e)
            {
                status = $"Unable to parse label file: {filename}. Error: {e.Message}";
            }
        }

        private void SaveLabelFile(string filename)
        {
            using (StreamWriter file = new StreamWriter(filename, false))
            {
                foreach (var label in labelsList.OrderBy(label => label.Id))
                {
                    file.WriteLine(label);
                }
            }
        }

        private void LoadLable_Click(object sender, RoutedEventArgs e)
        {
            if (mediaFile == null)
            {
                return;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonVideos);
                openFileDialog.Filter = "Label Files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                LoadLabelFile(openFileDialog.FileName);
            }
        }

        private void SaveLable_Click(object sender, RoutedEventArgs e)
        {
            if (mediaFile == null)
            {
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.InitialDirectory = Path.GetDirectoryName(mediaFile.filepath);
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(mediaFile.filepath) + ".txt";
                saveFileDialog.Filter = "Label Files (*.txt)|*.txt";

                if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                SaveLabelFile(saveFileDialog.FileName);
                InfoBox("Label file was saved.");
            }
        }

        private void OnAutoSave(object source, EventArgs e)
        {
            if (mediaFile == null)
            {
                return;
            }

            Path.GetDirectoryName(mediaFile.filepath);
            var filename = Path.GetFileNameWithoutExtension(mediaFile.filepath) + ".autosave";
            SaveLabelFile(filename);
            status = "Lable file was auto saved.";
        }
    }
}
