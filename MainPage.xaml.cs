﻿using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MauiApp = Microsoft.Maui.Controls.Application;

#if WINDOWS
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Storage.Pickers;
using Windows.Storage;
#endif

namespace DownloadsFolderOrganizer
{
    public partial class MainPage : ContentPage
    {
        private string selectedFolderPath = string.Empty;
        private readonly List<(string Source, string Destination)> movedFiles = new();

        // Enhanced file categories with more extensions
        private readonly Dictionary<string, List<string>> FileCategories = new()
        {
            { "Images", new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".svg", ".ico", ".raw", ".heic", ".heif" } },
            { "Documents", new List<string> { ".pdf", ".docx", ".doc", ".txt", ".xlsx", ".xls", ".pptx", ".ppt", ".rtf", ".odt", ".ods", ".odp", ".csv" } },
            { "Videos", new List<string> { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp", ".mts", ".m2ts" } },
            { "Music", new List<string> { ".mp3", ".wav", ".aac", ".flac", ".ogg", ".wma", ".m4a", ".opus" } },
            { "Archives", new List<string> { ".zip", ".rar", ".tar", ".gz", ".7z", ".bz2", ".xz", ".tar.gz" } },
            { "Code", new List<string> { ".py", ".js", ".html", ".css", ".java", ".cpp", ".c", ".cs", ".php", ".rb", ".go", ".ts", ".json", ".xml" } },
            { "Executables", new List<string> { ".exe", ".msi", ".deb", ".rpm", ".dmg", ".pkg", ".apk", ".app" } },
            { "Others", new List<string>() }
        };

        public MainPage()
        {
            InitializeComponent();
            InitializeApp();
        }

        // CS1998: Changed from async Task to void as it has no awaitable operations.
        private void InitializeApp()
        {
            try
            {
                LogAction("Downloads Folder Organizer initialized successfully.");
                UpdateUIState();
            }
            catch (Exception ex)
            {
                LogAction($"Initialization error: {ex.Message}");
            }
        }

        private void UpdateUIState()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (selectedFolderLabel != null)
                {
                    if (string.IsNullOrEmpty(selectedFolderPath))
                    {
                        selectedFolderLabel.Text = "No folder selected";
                        selectedFolderLabel.TextColor = Color.FromArgb("#aaaaaa");
                    }
                    else
                    {
                        selectedFolderLabel.Text = $"Selected: {Path.GetFileName(selectedFolderPath)}";
                        selectedFolderLabel.TextColor = Color.FromArgb("#00ff00");
                    }
                }

                if (undoButton != null)
                {
                    undoButton.IsEnabled = movedFiles.Count > 0;
                    undoButton.Opacity = movedFiles.Count > 0 ? 1.0 : 0.5;
                }

                if (organizeButton != null)
                {
                    organizeButton.IsEnabled = !string.IsNullOrEmpty(selectedFolderPath);
                    organizeButton.Opacity = !string.IsNullOrEmpty(selectedFolderPath) ? 1.0 : 0.7;
                }
            });
        }

        // Removed RequestPermissionsAsync() entirely as it's not needed for Windows-only app.

        private async void OnBrowseClicked(object sender, EventArgs e)
        {
            try
            {
                LogAction("Browse button clicked...");

                // Permission check removed as it's implied or handled differently on Windows.
#if WINDOWS
                await SelectFolderWindows();
#else
                // This entire else block is effectively removed for Windows-only app.
                // await SelectFolderCrossPlatform(); 
#endif
                UpdateUIState();
            }
            catch (Exception ex)
            {
                LogAction($"Browse error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to browse files: {ex.Message}", "OK");
            }
        }

#if WINDOWS
        private async Task SelectFolderWindows()
        {
            try
            {
                var picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.FileTypeFilter.Add("*");

                var window = (MauiApp.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window);
                if (window != null)
                {
                    var hWnd = WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

                    var folder = await picker.PickSingleFolderAsync();
                    if (folder != null)
                    {
                        selectedFolderPath = folder.Path;
                        LogAction($"Selected folder: {selectedFolderPath}");
                        await UpdateFileCount();
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($"Windows folder selection error: {ex.Message}");
            }
        }
#endif

        // Removed SelectFolderCrossPlatform() entirely for Windows-only app.

        private async Task UpdateFileCount()
        {
            try
            {
                if (!string.IsNullOrEmpty(selectedFolderPath) && Directory.Exists(selectedFolderPath))
                {
                    // Run file IO and grouping on a background thread to avoid blocking the UI
                    var result = await Task.Run(() =>
                    {
                        var files = Directory.GetFiles(selectedFolderPath);
                        var logLines = new List<string>
                        {
                            $"Found {files.Length} files to organize."
                        };

                        if (files.Length > 0)
                        {
                            var typeBreakdown = files
                                .GroupBy(f => GetFileCategory(Path.GetExtension(f).ToLower()))
                                .ToDictionary(g => g.Key, g => g.Count());

                            foreach (var type in typeBreakdown.OrderByDescending(x => x.Value))
                            {
                                logLines.Add($"   • {type.Key}: {type.Value} files");
                            }
                        }
                        return logLines;
                    });

                    foreach (var line in result)
                    {
                        LogAction(line);
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($"Error updating file count: {ex.Message}");
            }
        }

        // CA1826: Replaced LINQ with foreach loop for potential efficiency gain.
        private string GetFileCategory(string extension)
        {
            foreach (var category in FileCategories)
            {
                if (category.Value.Contains(extension))
                {
                    return category.Key;
                }
            }
            return "Others";
        }

        private async void OnOrganizeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                LogAction("Please select a folder first.");
                await DisplayAlert("No Folder Selected", "Please browse and select a folder to organize.", "OK");
                return;
            }

            if (!Directory.Exists(selectedFolderPath))
            {
                LogAction("Selected folder no longer exists.");
                selectedFolderPath = string.Empty;
                UpdateUIState();
                await DisplayAlert("Folder Not Found", "The selected folder no longer exists.", "OK");
                return;
            }

            try
            {
                organizeButton.IsEnabled = false;
                organizeButton.Text = "Organizing...";

                var files = Directory.GetFiles(selectedFolderPath);
                int total = files.Length;

                if (total == 0)
                {
                    LogAction("No files to organize.");
                    await DisplayAlert("No Files", "No files found in the selected folder.", "OK");
                    return;
                }

                bool confirm = await DisplayAlert("Confirm Organization",
                    $"This will organize {total} files into category folders. Continue?",
                    "Yes", "No");

                if (!confirm)
                {
                    organizeButton.IsEnabled = true;
                    organizeButton.Text = "Organize";
                    return;
                }

                movedFiles.Clear();
                progressBar.Progress = 0;
                LogAction($"Starting organization of {total} files...");

                int current = 0;
                int successful = 0;
                int failed = 0;

                foreach (var file in files)
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string category = GetFileCategory(extension);
                        string destDir = Path.Combine(selectedFolderPath, category);

                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                            LogAction($"Created folder: {category}");
                        }

                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(destDir, fileName);

                        if (File.Exists(destPath))
                        {
                            destPath = GetUniqueFilePath(destPath);
                        }

                        File.Move(file, destPath);
                        movedFiles.Add((destPath, file));
                        LogAction($"{fileName} → {category}");
                        successful++;
                    }
                    catch (Exception ex)
                    {
                        LogAction($"Failed to move {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }

                    current++;
                    progressBar.Progress = (double)current / total;
                    await Task.Delay(10);
                }

                LogAction($"Organization complete! {successful} files organized, {failed} failed.");

                if (successful > 0)
                {
                    await DisplayAlert("Success",
                        $"Successfully organized {successful} files into category folders!",
                        "OK");
                }

                UpdateUIState();
            }
            catch (Exception ex)
            {
                LogAction($"Organization error: {ex.Message}");
                await DisplayAlert("Error", $"An error occurred during organization: {ex.Message}", "OK");
            }
            finally
            {
                organizeButton.IsEnabled = true;
                organizeButton.Text = "Organize";
            }
        }

        // CA1822: Marked as static as it does not access instance data.
        private static string GetUniqueFilePath(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newPath = filePath;

            while (File.Exists(newPath))
            {
                newPath = Path.Combine(directory, $"{fileName} ({counter}){extension}");
                counter++;
            }

            return newPath;
        }

        private async void OnUndoClicked(object sender, EventArgs e)
        {
            if (movedFiles.Count == 0)
            {
                LogAction("Nothing to undo.");
                return;
            }

            bool confirm = await DisplayAlert("Confirm Undo",
                $"This will restore {movedFiles.Count} files to their original locations. Continue?",
                "Yes", "No");

            if (!confirm) return;

            try
            {
                undoButton.IsEnabled = false;
                LogAction("Undoing files...");

                int successful = 0;
                int failed = 0;

                foreach (var (source, original) in movedFiles.ToList())
                {
                    try
                    {
                        if (File.Exists(source))
                        {
                            var originalDir = Path.GetDirectoryName(original);
                            if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
                            {
                                Directory.CreateDirectory(originalDir);
                            }

                            File.Move(source, original);
                            LogAction($"Restored: {Path.GetFileName(source)}");
                            successful++;
                        }
                        else
                        {
                            LogAction($"File not found for undo: {Path.GetFileName(source)}");
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogAction($"Undo failed for {Path.GetFileName(source)}: {ex.Message}");
                        failed++;
                    }
                }

                movedFiles.Clear();
                LogAction($"Undo complete! {successful} files restored, {failed} failed.");

                await CleanupEmptyFolders();
                UpdateUIState();
            }
            catch (Exception ex)
            {
                LogAction($"Undo error: {ex.Message}");
                await DisplayAlert("Error", $"An error occurred during undo: {ex.Message}", "OK");
            }
            finally
            {
                undoButton.IsEnabled = movedFiles.Count > 0;
            }
        }

        private async Task CleanupEmptyFolders()
        {
            try
            {
                if (string.IsNullOrEmpty(selectedFolderPath) || !Directory.Exists(selectedFolderPath))
                    return;

                await Task.Run(() =>
                {
                    foreach (var category in FileCategories.Keys)
                    {
                        string categoryPath = Path.Combine(selectedFolderPath, category);
                        if (Directory.Exists(categoryPath) && !Directory.EnumerateFileSystemEntries(categoryPath).Any())
                        {
                            Directory.Delete(categoryPath);
                            LogAction($"Removed empty folder: {category}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogAction($"Cleanup error: {ex.Message}");
            }
        }

        private void LogAction(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    if (logLabel != null)
                    {
                        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

                        if (string.IsNullOrEmpty(logLabel.Text) || logLabel.Text == ">> Ready to organize...")
                        {
                            logLabel.Text = timestamped;
                        }
                        else
                        {
                            logLabel.Text += $"\n{timestamped}";
                        }

                        var lines = logLabel.Text.Split('\n');
                        if (lines.Length > 100)
                        {
                            logLabel.Text = string.Join("\n", lines.Skip(20));
                        }

                        if (logScrollView != null)
                        {
                            await Task.Delay(10);
                            await logScrollView.ScrollToAsync(logLabel, ScrollToPosition.End, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Logging error: {ex.Message}");
                }
            });
        }

        private void OnClearLogClicked(object sender, EventArgs e)
        {
            if (logLabel != null)
            {
                logLabel.Text = ">> Ready to organize...";
            }
        }

        // CS1998: Changed from async string to string as it has no awaitable operations.
        private string GetDefaultDownloadsPath()
        {
            try
            {
#if WINDOWS
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#endif
            }
            catch (Exception ex)
            {
                LogAction($"Error getting downloads path: {ex.Message}");
                return "";
            }
        }

        private async void OnQuickDownloadsClicked(object sender, EventArgs e)
        {
            try
            {
                // Permission check removed as it's implied or handled differently on Windows.

                string downloadsPath = GetDefaultDownloadsPath();

                if (!string.IsNullOrEmpty(downloadsPath) && Directory.Exists(downloadsPath))
                {
                    selectedFolderPath = downloadsPath;
                    LogAction($"Quick access: Downloads folder selected");
                    await UpdateFileCount();
                    UpdateUIState();
                }
                else
                {
                    LogAction("Downloads folder not found. Please use Browse instead.");
                    await DisplayAlert("Downloads Not Found",
                        "Default downloads folder not found. Please use the Browse button to select a folder.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                LogAction($"Quick downloads error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to access downloads folder: {ex.Message}", "OK");
            }
        }
    }
}