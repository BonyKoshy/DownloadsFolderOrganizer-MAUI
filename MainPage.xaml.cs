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
            ["Images"] = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".svg", ".ico", ".raw"],
            ["Documents"] = [".pdf", ".docx", ".doc", ".txt", ".xlsx", ".xls", ".pptx", ".ppt", ".rtf", ".odt", ".ods", ".odp"],
            ["Videos"] = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp"],
            ["Music"] = [".mp3", ".wav", ".aac", ".flac", ".ogg", ".wma", ".m4a"],
            ["Archives"] = [".zip", ".rar", ".tar", ".gz", ".7z", ".bz2", ".xz"],
            ["Code"] = [".py", ".js", ".html", ".css", ".java", ".cpp", ".c", ".cs", ".php", ".rb", ".go", ".ts"],
            ["Executables"] = [".exe", ".msi", ".deb", ".rpm", ".dmg", ".pkg", ".apk"],
            ["Others"] = []
        };

        public MainPage()
        {
            InitializeComponent();
            InitializeApp();
        }

        private async void InitializeApp()
        {
            try
            {
                // Request permissions on startup for mobile platforms
                await RequestPermissionsAsync();
                LogAction(">> Downloads Folder Organizer initialized successfully.");
            }
            catch (Exception ex)
            {
                LogAction($">> Initialization error: {ex.Message}");
            }
        }

        private async Task<bool> RequestPermissionsAsync()
        {
            try
            {
                // Request storage permissions for mobile platforms
                var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                if (status != PermissionStatus.Granted)
                {
                    LogAction(">> Storage read permission denied.");
                    return false;
                }

                var writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                if (writeStatus != PermissionStatus.Granted)
                {
                    LogAction(">> Storage write permission denied.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogAction($">> Permission request error: {ex.Message}");
                return false;
            }
        }

        // Cross-platform file selection method
        private async void OnBrowseClicked(object sender, EventArgs e)
        {
            try
            {
                // Check permissions first
                if (!await RequestPermissionsAsync())
                {
                    await DisplayAlert("Permission Required", "Storage permission is required to organize files.", "OK");
                    return;
                }

#if WINDOWS
                await SelectFolderWindows();
#else
                await SelectFolderCrossPlatform();
#endif
            }
            catch (Exception ex)
            {
                LogAction($">> Browse error: {ex.Message}");
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

                // Get the current window handle
                var window = (MauiApp.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window);
                if (window != null)
                {
                    var hWnd = WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

                    var folder = await picker.PickSingleFolderAsync();
                    if (folder != null)
                    {
                        selectedFolderPath = folder.Path;
                        LogAction($">> Selected folder: {selectedFolderPath}");
                        await UpdateFileCount();
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Windows folder selection error: {ex.Message}");
            }
        }
#endif

        private async Task SelectFolderCrossPlatform()
        {
            try
            {
                // For mobile platforms, use file picker to let user select a file from the desired folder
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "*/*" } },
                        { DevicePlatform.iOS, new[] { "public.item" } },
                        { DevicePlatform.WinUI, new[] { "*" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.item" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Select any file from the folder you want to organize",
                    FileTypes = customFileType
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    var filePath = result.FullPath;
                    var folder = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        selectedFolderPath = folder;
                        LogAction($">> Selected folder: {selectedFolderPath}");
                        await UpdateFileCount();
                    }
                    else
                    {
                        LogAction(">> Error: Could not determine folder path.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Cross-platform folder selection error: {ex.Message}");
            }
        }

        private async Task UpdateFileCount()
        {
            try
            {
                if (!string.IsNullOrEmpty(selectedFolderPath) && Directory.Exists(selectedFolderPath))
                {
                    var files = Directory.GetFiles(selectedFolderPath);
                    LogAction($">> Found {files.Length} files to organize.");

                    // Show file type breakdown
                    var typeBreakdown = files
                        .GroupBy(f => GetFileCategory(Path.GetExtension(f).ToLower()))
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var type in typeBreakdown)
                    {
                        LogAction($"   • {type.Key}: {type.Value} files");
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Error updating file count: {ex.Message}");
            }
        }

        private string GetFileCategory(string extension)
        {
            return FileCategories.FirstOrDefault(kv => kv.Value.Contains(extension)).Key ?? "Others";
        }

        private async void OnOrganizeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                LogAction(">> Please select a folder first.");
                await DisplayAlert("No Folder Selected", "Please browse and select a folder to organize.", "OK");
                return;
            }

            if (!Directory.Exists(selectedFolderPath))
            {
                LogAction(">> Selected folder no longer exists.");
                await DisplayAlert("Folder Not Found", "The selected folder no longer exists.", "OK");
                return;
            }

            try
            {
                var files = Directory.GetFiles(selectedFolderPath);
                int total = files.Length;

                if (total == 0)
                {
                    LogAction(">> No files to organize.");
                    await DisplayAlert("No Files", "No files found in the selected folder.", "OK");
                    return;
                }

                // Confirm organization
                bool confirm = await DisplayAlert("Confirm Organization",
                    $"This will organize {total} files into category folders. Continue?",
                    "Yes", "No");

                if (!confirm) return;

                movedFiles.Clear();
                progressBar.Progress = 0;
                LogAction($">> Starting organization of {total} files...");

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
                            LogAction($">> Created folder: {category}");
                        }

                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(destDir, fileName);

                        // Handle duplicate files
                        if (File.Exists(destPath))
                        {
                            destPath = GetUniqueFilePath(destPath);
                        }

                        File.Move(file, destPath);
                        movedFiles.Add((destPath, file));
                        LogAction($">> {fileName} → {category}");
                        successful++;
                    }
                    catch (Exception ex)
                    {
                        LogAction($">> Failed to move {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }

                    current++;
                    progressBar.Progress = (double)current / total;

                    // Allow UI to update
                    await Task.Delay(50);
                }

                LogAction($">> Organization complete! {successful} files organized, {failed} failed.");

                if (successful > 0)
                {
                    await DisplayAlert("Success",
                        $"Successfully organized {successful} files into category folders!",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Organization error: {ex.Message}");
                await DisplayAlert("Error", $"An error occurred during organization: {ex.Message}", "OK");
            }
        }

        private string GetUniqueFilePath(string filePath)
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
                LogAction(">> Nothing to undo.");
                await DisplayAlert("Nothing to Undo", "No recent organization to undo.", "OK");
                return;
            }

            bool confirm = await DisplayAlert("Confirm Undo",
                $"This will restore {movedFiles.Count} files to their original locations. Continue?",
                "Yes", "No");

            if (!confirm) return;

            try
            {
                int successful = 0;
                int failed = 0;

                foreach (var (source, original) in movedFiles.ToList())
                {
                    try
                    {
                        if (File.Exists(source))
                        {
                            File.Move(source, original);
                            LogAction($">> Restored: {Path.GetFileName(source)}");
                            successful++;
                        }
                        else
                        {
                            LogAction($">> File not found for undo: {Path.GetFileName(source)}");
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogAction($">> Undo failed for {Path.GetFileName(source)}: {ex.Message}");
                        failed++;
                    }
                }

                movedFiles.Clear();
                LogAction($">> Undo complete! {successful} files restored, {failed} failed.");

                // Clean up empty category folders
                await CleanupEmptyFolders();
            }
            catch (Exception ex)
            {
                LogAction($">> Undo error: {ex.Message}");
                await DisplayAlert("Error", $"An error occurred during undo: {ex.Message}", "OK");
            }
        }

        private async Task CleanupEmptyFolders()
        {
            try
            {
                if (string.IsNullOrEmpty(selectedFolderPath) || !Directory.Exists(selectedFolderPath))
                    return;

                foreach (var category in FileCategories.Keys)
                {
                    string categoryPath = Path.Combine(selectedFolderPath, category);
                    if (Directory.Exists(categoryPath) && !Directory.EnumerateFileSystemEntries(categoryPath).Any())
                    {
                        Directory.Delete(categoryPath);
                        LogAction($">> Removed empty folder: {category}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Cleanup error: {ex.Message}");
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
                        // Add timestamp to log messages
                        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
                        logLabel.Text += $"\n{timestamped}";

                        // Limit log length to prevent memory issues
                        var lines = logLabel.Text.Split('\n');
                        if (lines.Length > 100)
                        {
                            logLabel.Text = string.Join("\n", lines.Skip(20));
                        }

                        if (logScrollView != null)
                        {
                            await Task.Delay(10); // Small delay to ensure text is updated
                            await logScrollView.ScrollToAsync(logLabel, ScrollToPosition.End, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Logging error: {ex.Message}");
                }
            });
        }

        private async void OnClearLogClicked(object sender, EventArgs e)
        {
            if (logLabel != null)
            {
                logLabel.Text = ">> Log cleared.";
                LogAction("Downloads Folder Organizer ready.");
            }
        }

#if WINDOWS
        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static partial bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

        private void OnMinimizeClicked(object sender, EventArgs e)
        {
            try
            {
                var window = (MauiApp.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window);
                if (window != null)
                {
                    System.IntPtr hWnd = WindowNative.GetWindowHandle(window);
                    ShowWindow(hWnd, 6); // 6 = SW_MINIMIZE
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Minimize error: {ex.Message}");
            }
        }
#else
        private async void OnMinimizeClicked(object sender, EventArgs e)
        {
            // For non-Windows platforms, show a message
            await DisplayAlert("Info", "Minimize function is only available on Windows.", "OK");
        }
#endif

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert("Confirm Exit",
                    "Are you sure you want to close the application?",
                    "Yes", "No");

                if (confirm)
                {
#if WINDOWS
                    MauiApp.Current?.Quit();
#else
                    // For other platforms, use the standard method
                    System.Environment.Exit(0);
#endif
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Close error: {ex.Message}");
                // Fallback exit method
                System.Environment.Exit(0);
            }
        }

        // Method to get default downloads folder path for each platform
        private string GetDefaultDownloadsPath()
        {
            try
            {
#if WINDOWS
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#elif ANDROID
                return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath ?? "";
#elif IOS
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Downloads");
#else
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#endif
            }
            catch
            {
                return "";
            }
        }

        // Quick access to downloads folder
        private async void OnQuickDownloadsClicked(object sender, EventArgs e)
        {
            try
            {
                string downloadsPath = GetDefaultDownloadsPath();
                if (!string.IsNullOrEmpty(downloadsPath) && Directory.Exists(downloadsPath))
                {
                    selectedFolderPath = downloadsPath;
                    LogAction($">> Quick access: Downloads folder selected");
                    await UpdateFileCount();
                }
                else
                {
                    LogAction(">> Downloads folder not found. Please use Browse instead.");
                    await DisplayAlert("Downloads Not Found",
                        "Default downloads folder not found. Please use the Browse button to select a folder.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                LogAction($">> Quick downloads error: {ex.Message}");
            }
        }
    }
}