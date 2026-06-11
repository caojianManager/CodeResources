using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EEGTool.ViewModels.Collection;

namespace EEGTool.Views.Collection
{
    /// <summary>
    /// EEGMonitorView.xaml 的交互逻辑
    /// </summary>
    public partial class EEGMonitorView : UserControl
    {
        private bool _isDraggingPlot;
        private Point _lastDragPoint;

        public EEGMonitorView()
        {
            InitializeComponent();
        }

        private void EegPlot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is EEGMonitorViewModel viewModel)
            {
                viewModel.ZoomYAxisByWheel(e.Delta);
                e.Handled = true;
            }
        }

        private void EegPlot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            _isDraggingPlot = true;
            _lastDragPoint = e.GetPosition(element);
            element.CaptureMouse();
            e.Handled = true;
        }

        private void EegPlot_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingPlot || e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement element)
            {
                return;
            }

            Point currentPoint = e.GetPosition(element);
            double deltaY = currentPoint.Y - _lastDragPoint.Y;
            _lastDragPoint = currentPoint;

            if (DataContext is EEGMonitorViewModel viewModel)
            {
                viewModel.PanYAxisByPixels(deltaY, element.ActualHeight);
            }

            e.Handled = true;
        }

        private void EegPlot_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndPlotDrag(sender as FrameworkElement);
            e.Handled = true;
        }

        private void EegPlot_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _isDraggingPlot = false;
        }

        private void EegPlot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (DataContext is EEGMonitorViewModel viewModel)
                {
                    viewModel.UpdateWaveHeaderItemPositions();
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void EndPlotDrag(FrameworkElement? element)
        {
            _isDraggingPlot = false;
            if (element?.IsMouseCaptured == true)
            {
                element.ReleaseMouseCapture();
            }
        }
    }
}
