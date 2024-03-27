using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

using Point = System.Windows.Point;

namespace SquareCropper {
    /// <summary>
    /// Full logic of the Multi-Square Cropper miniapplication.
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>
        /// User-loaded image to crop.
        /// </summary>
        Bitmap image;

        /// <summary>
        /// Path of the loaded <see cref="image"/> to overwrite it.
        /// </summary>
        string fileName;

        /// <summary>
        /// Beginning of the filename for each exported crop.
        /// </summary>
        string cropFileStart;

        /// <summary>
        /// When open, magnifies the selected area.
        /// </summary>
        Zoom zoom;

        /// <summary>
        /// Add the components to the window.
        /// </summary>
        public MainWindow() {
            InitializeComponent();
        }

        /// <summary>
        /// Handle keyboard input before any other control could.
        /// </summary>
        void OnKeyPress(object _, KeyEventArgs e) {
            if ((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) != 0) {
                return; // Shift bypasses this handler (e.g. shift + arrows move the image instead)
            }
            switch (e.Key) {
                case Key.Left:
                    selector.Margin = new Thickness(selector.Margin.Left - 1, selector.Margin.Top, 0, 0);
                    UpdateZoom();
                    break;
                case Key.Up:
                    selector.Margin = new Thickness(selector.Margin.Left, selector.Margin.Top - 1, 0, 0);
                    UpdateZoom();
                    break;
                case Key.Right:
                    selector.Margin = new Thickness(selector.Margin.Left + 1, selector.Margin.Top, 0, 0);
                    UpdateZoom();
                    break;
                case Key.Down:
                    selector.Margin = new Thickness(selector.Margin.Left, selector.Margin.Top + 1, 0, 0);
                    UpdateZoom();
                    break;
                case Key.Add:
                    cropSize.Value++;
                    break;
                case Key.Subtract:
                    cropSize.Value--;
                    break;
                case Key.Enter:
                    SaveCrop(null, null);
                    break;
                default:
                    return; // Don't mark the key as handled when it's not
            }
            e.Handled = true;
        }

        /// <summary>
        /// (Re)open an image for editing. When the cropped images are next to it, the crop counter will be restored.
        /// </summary>
        void OpenFile(object _, RoutedEventArgs e) {
            OpenFileDialog opener = new();
            if (opener.ShowDialog().Value) {
                fileName = opener.FileName;
                using Image source = Image.FromFile(fileName);
                image = new Bitmap(source);
                scrollGrid.Width = image.Width;
                scrollGrid.Height = image.Height;

                int extensionFrom = fileName.LastIndexOf('.');
                cropFileStart = Path.GetFileName(fileName[0..extensionFrom]);
                string dir = Path.GetDirectoryName(fileName);
                string[] crops = Directory.GetFiles(dir, cropFileStart + "*");
                cropFileStart = Path.Combine(dir, cropFileStart) + '_';
                if (crops.Length != 0) {
                    int idxFrom = crops[^1].LastIndexOf('_'),
                        idxTo = crops[^1].LastIndexOf('.');
                    if (idxFrom < extensionFrom) {
                        cropCount.Value = 1;
                    } else if (int.TryParse(crops[^1].AsSpan(idxFrom + 1, idxTo - idxFrom - 1), out int idx)) {
                        cropCount.Value = idx + 1;
                    }
                } else {
                    cropCount.Value = 1;
                }
                UpdateImage();
            }
        }

        /// <summary>
        /// Center the crop <see cref="selector"/> rectangle to where the user clicked on the image.
        /// </summary>
        void ImageClicked(object _, MouseButtonEventArgs e) {
            int selectorSize = (int)(selector.Width / 2 + .5);
            Point position = e.GetPosition(imageDisplay);
            selector.Margin = new Thickness(position.X - selectorSize, position.Y - selectorSize, 0, 0);
            UpdateZoom();
        }

        /// <summary>
        /// Update the crop <see cref="selector"/> when the user changed its size,
        /// while keeping it centered around the last click's position.
        /// </summary>
        void CropSizeChanged(object _, RoutedEventArgs e) {
            int oldSize = (int)(selector.Width + .5);
            selector.Width = cropSize.Value;
            selector.Height = cropSize.Value;
            if ((cropSize.Value & 1) != 0) {
                int offset = oldSize < cropSize.Value ? -1 : 1;
                selector.Margin = new Thickness(selector.Margin.Left + offset, selector.Margin.Top + offset, 0, 0);
            }
            UpdateZoom();
        }

        /// <summary>
        /// Write the selected crop rectangle's contents to a new file with an updated counter index,
        /// and remove that from the original image, overwriting it.
        /// </summary>
        void SaveCrop(object _, RoutedEventArgs e) {
            if (image == null) {
                return;
            }

            Bitmap crop = GetSelectedArea();
            if (crop == null) {
                MessageBox.Show("Selection overhangs the image.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            crop.Save($"{cropFileStart}{cropCount.Value:000}.png", ImageFormat.Png);

            // Remove it from the original image
            for (int x0 = (int)(selector.Margin.Left + .5), x = 0; x < crop.Width; x++) {
                for (int y0 = (int)(selector.Margin.Top + .5), y = 0; y < crop.Height; y++) {
                    image.SetPixel(x0 + x, y0 + y, Color.White);
                }
            }
            image.Save(fileName, ImageFormat.Png);
            UpdateImage();

            cropCount.Value++;
        }

        /// <summary>
        /// Opens a dialog showing the selected area, magnified.
        /// </summary>
        void OpenZoom(object _, RoutedEventArgs e) {
            zoom ??= new Zoom();
            zoom.Show();
        }

        /// <summary>
        /// Crops the part of the image which the user selected. Returns null when the selection overhangs the image.
        /// </summary>
        Bitmap GetSelectedArea() {
            Rectangle rect = new((int)(selector.Margin.Left + .5), (int)(selector.Margin.Top + .5),
                (int)(selector.Width + .5), (int)(selector.Height + .5));
            if (rect.Left < 0 || rect.Top < 0 || rect.Left + rect.Width >= image.Width || rect.Top + rect.Height >= image.Height) {
                return null;
            }
            return image.Clone(rect, image.PixelFormat);
        }

        /// <summary>
        /// Copy to the working <see cref="image"/> to the <see cref="imageDisplay"/> control for visual editing.
        /// </summary>
        void UpdateImage() {
            // Source: https://stackoverflow.com/questions/41998142
            using MemoryStream ms = new();
            image.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            imageDisplay.Source = bitmapImage;

            UpdateZoom();
        }

        /// <summary>
        /// Update the magnified image if there is a window for it.
        /// </summary>
        void UpdateZoom() {
            if (zoom == null) {
                return;
            }

            Bitmap selection = GetSelectedArea();
            if (selection == null) {
                return;
            }

            // Source: https://stackoverflow.com/questions/26260654
            nint handle = selection.GetHbitmap();
            try {
                BitmapSource source =
                    Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                zoom.Update(source);
            } finally {
                DeleteObject(handle);
            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
    }
}