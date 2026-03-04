using System.Windows;
using System.Windows.Controls;

namespace BatchRecoder
{
    public partial class MainWindow : Window
    {
        private bool _autoScroll = true;

        public MainWindow()
        {
            InitializeComponent();
            
            LogTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, 
                new ScrollChangedEventHandler(OnLogScrollChanged));
            
            LogTextBox.TextChanged += OnLogTextChanged;
        }

        private void OnLogScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = e.OriginalSource as ScrollViewer;
            if (scrollViewer == null) return;

            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 10)
            {
                _autoScroll = true;
            }
            else if (e.VerticalChange < 0)
            {
                _autoScroll = false;
            }
        }

        private void OnLogTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_autoScroll)
            {
                LogTextBox.ScrollToEnd();
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}