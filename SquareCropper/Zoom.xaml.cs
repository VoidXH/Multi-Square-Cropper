using System.Windows;
using System.Windows.Media;

namespace SquareCropper {
    /// <summary>
    /// Displays the selected area, scalably magnified.
    /// </summary>
    public partial class Zoom : Window {
        /// <summary>
        /// Assemble the Zoom window.
        /// </summary>
        public Zoom() => InitializeComponent();

        /// <summary>
        /// When the selected area has changed, update the displayed image.
        /// </summary>
        public void Update(ImageSource source) => image.Source = source;
    }
}