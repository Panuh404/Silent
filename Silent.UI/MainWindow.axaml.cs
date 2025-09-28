using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Silent.Core.IO;
using Silent.Core.Text;

namespace Silent.UI
{
    public partial class MainWindow : Window
    {
        private Document _doc = new(null, "");
        private EventHandler? _bufferChangedHandler;

        public MainWindow()
        {
            InitializeComponent();

            AttachDocument(_doc);
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O)
            {
                _ = OpenAsync();
                e.Handled = true;
            }
            else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)
            {
                _ = SaveAsync();
                e.Handled = true;
            }
        }

        private void AttachDocument(Document doc)
        {
            // detach previous handlers
            if (_bufferChangedHandler is not null)
                _doc.Buffer.Changed -= _bufferChangedHandler;

            _doc = doc;
            Editor.Document = _doc;

            _bufferChangedHandler = (_, __) => UpdateTitle();
            _doc.Buffer.Changed += _bufferChangedHandler;
            _doc.DirtyChanged += (_, __) => UpdateTitle();

            UpdateTitle();
        }

        private void UpdateTitle()
        {
            var name = string.IsNullOrEmpty(_doc.FilePath) ? "Untitled" : Path.GetFileName(_doc.FilePath);
            Title = $"{name} - Silent Editor{(_doc.IsDirty ? " *" : "")}";
        }

        // Menu Handles
        private async void OnOpenClick(object? sender, RoutedEventArgs e) => await OpenAsync();
        private async void OnSaveClick(object? sender, RoutedEventArgs e) => await SaveAsync();
        private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await SaveAsAsync();
        private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

        // File Stuff
        private async System.Threading.Tasks.Task OpenAsync()
        {
            var dlg = new OpenFileDialog
            {
                AllowMultiple = false,
                Filters =
                {
                    new FileDialogFilter{ Name = "Text", Extensions = { "txt", "md", "json", "cs", "cpp", "h" } },
                    new FileDialogFilter{ Name = "All files", Extensions = { "*" } }
                }
            };
            var res = await dlg.ShowAsync(this);
            if (res is { Length: > 0 })
            {
                var opened = FileService.Open(res[0]);
                AttachDocument(opened);
            }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (string.IsNullOrEmpty(_doc.FilePath))
            {
                await SaveAsAsync();
                return;
            }
            FileService.Save(_doc);
            UpdateTitle();
        }

        private async System.Threading.Tasks.Task SaveAsAsync()
        {
            var dlg = new SaveFileDialog
            {
                InitialFileName = string.IsNullOrEmpty(_doc.FilePath) ? "Untitled.txt" : Path.GetFileName(_doc.FilePath)
            };
            var path = await dlg.ShowAsync(this);
            if (!string.IsNullOrWhiteSpace(path))
            {
                FileService.Save(_doc, path);
                UpdateTitle();
            }
        }
    }
}