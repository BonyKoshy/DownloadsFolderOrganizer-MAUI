using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MauiApp = Microsoft.Maui.Controls.Application;


#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;
#endif

namespace DownloadsFolderOrganizer
{
    public partial class MainPage : ContentPage
    {
        private string selectedFolderPath = string.Empty;
        private readonly List<(string Source, string Destination)> movedFiles = new();

        // File categories
        private readonly Dictionary<string, List<string>> FileCategories = new()
        {
            ["Images"] = [".jpg", ".jpeg", ".png", ".gif", ".bmp"],
            ["Documents"] = [".pdf", ".docx", ".txt", ".xlsx", ".pptx"],
            ["Videos"] = [".mp4", ".mkv", ".avi", ".mov"],
            ["Music"] = [".mp3", ".wav", ".aac"],
            ["Archives"] = [".zip", ".rar", ".tar", ".gz"],
            ["Code"] = [".py", ".js", ".html", ".css", ".java"],
            ["Others"] = []
        };

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnBrowseClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a file to find its folder"
                });

                if (result != null)
                {
                    var filePath = result.FullPath;
                    var folder = Path.GetDirectoryName(filePath);
                    if (folder is not null)
                    {
                        selectedFolderPath = folder;
                        LogAction($">> Selected folder: {selectedFolderPath}");
                    }
                    else
                    {
                        LogAction(">> Error: Could not determine folder.");
                    }

                    LogAction($">> Selected folder: {selectedFolderPath}");
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Error: {ex.Message}");
            }
        }

        private async void OnOrganizeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                LogAction(">> Please select a folder first.");
                return;
            }

            var files = Directory.GetFiles(selectedFolderPath);
            int total = files.Length;
            if (total == 0)
            {
                LogAction(">> No files to organize.");
                return;
            }

            movedFiles.Clear();
            progressBar.Progress = 0;

            int current = 0;
            foreach (var file in files)
            {
                string extension = Path.GetExtension(file).ToLower();
                string category = FileCategories.FirstOrDefault(kv => kv.Value.Contains(extension)).Key ?? "Others";
                string destDir = Path.Combine(selectedFolderPath, category);

                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                string destPath = Path.Combine(destDir, Path.GetFileName(file));

                try
                {
                    File.Move(file, destPath);
                    movedFiles.Add((destPath, file));
                    LogAction($">> {Path.GetFileName(file)} -> {category}");
                }
                catch (Exception ex)
                {
                    LogAction($">> Failed: {ex.Message}");
                }

                current++;
                progressBar.Progress = (double)current / total;
                await Task.Delay(100); // Simulate progress
            }

            LogAction(">> Files organized successfully.");
        }

        private void OnUndoClicked(object sender, EventArgs e)
        {
            if (movedFiles.Count == 0)
            {
                LogAction(">> Nothing to undo.");
                return;
            }

            foreach (var (source, original) in movedFiles)
            {
                try
                {
                    File.Move(source, original);
                    LogAction($">> Undo: {Path.GetFileName(source)} restored.");
                }
                catch (Exception ex)
                {
                    LogAction($">> Undo Failed: {ex.Message}");
                }
            }

            movedFiles.Clear();
        }

        private void LogAction(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (logLabel != null)
                    logLabel.Text += $"\n{message}";
            });
        }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void OnMinimizeClicked(object sender, EventArgs e)
    {
        var window = (MauiApp.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window);
        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        ShowWindow(hWnd, 6); // 6 = SW_MINIMIZE
    }
#endif

        private void OnCloseClicked(object sender, EventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}
