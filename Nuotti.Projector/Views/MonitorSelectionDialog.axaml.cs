using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;

namespace Nuotti.Projector.Views;

public partial class MonitorSelectionDialog : Window
{
    private readonly ListBox _monitorListBox;
    
    public MonitorInfo? SelectedMonitor { get; private set; }
    public bool DialogResult { get; private set; }
    
    public MonitorSelectionDialog()
    {
        InitializeComponent();
        _monitorListBox = this.FindControl<ListBox>("MonitorListBox")!;
    }
    
    public void SetMonitors(List<MonitorInfo> monitors, string? selectedMonitorId = null)
    {
        _monitorListBox.ItemsSource = monitors;
        
        // Select the previously selected monitor or primary
        if (!string.IsNullOrEmpty(selectedMonitorId))
        {
            var selectedMonitor = monitors.Find(m => m.Id == selectedMonitorId);
            if (selectedMonitor != null)
            {
                _monitorListBox.SelectedItem = selectedMonitor;
            }
        }
        
        // If nothing selected, select primary
        if (_monitorListBox.SelectedItem == null)
        {
            var primaryMonitor = monitors.Find(m => m.IsPrimary);
            if (primaryMonitor != null)
            {
                _monitorListBox.SelectedItem = primaryMonitor;
            }
        }
    }
    
    private void OnSelectClick(object? sender, RoutedEventArgs e)
    {
        SelectedMonitor = _monitorListBox.SelectedItem as MonitorInfo;
        DialogResult = true;
        Close();
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
