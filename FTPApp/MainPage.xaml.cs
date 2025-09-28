using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Renci.SshNet;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace FTPApp
{
    public partial class MainPage : ContentPage
    {
        // Hardcoded SFTP host here:
        private const string SftpHost = "swiftdeploy.blob.core.windows.net";
        string username = "swiftdeploy.swiftdeploy1";
        string password = "pSXfRnGicNKx3y3tHyGvRvFBmFL1+ndb";
        string EmailExistsApiUrl = "http://localhost:5280/api/User/exists"; // Replace with your actual API URL
        public MainPage()
        {
            InitializeComponent();
        }
        private async void OnUploadFolderClicked(object sender, EventArgs e)
        {
            await UploadFolderOrFiles(isFolder: true);
        }

        private async void OnUploadFilesClicked(object sender, EventArgs e)
        {
            await UploadFolderOrFiles(isFolder: false);
        }

        private async Task<bool> CheckEmailExistsAsync(string email)
        {
            try
            {
                var url = $"{EmailExistsApiUrl}?email={Uri.EscapeDataString(email)}";
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    StatusLabel.Text = "Email validation request failed.";
                    return false;
                }
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("exists", out var exists) && exists.GetBoolean();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Email check error: {ex.Message}";
                return false;
            }
        }

        private async Task UploadFolderOrFiles(bool isFolder)
        {
            var email = EmailEntry.Text?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                StatusLabel.Text = "Please enter your email.";
                return;
            }

            // Email validation step
            StatusLabel.Text = "Validating email...";
            if (!await CheckEmailExistsAsync(email))
            {
                StatusLabel.Text = "Email is not registered or not authorized!";
                return;
            }

            StatusLabel.Text = isFolder ? "Picking folder..." : "Picking file(s)...";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusLabel.Text = "Please enter your SFTP username and password.";
                return;
            }

            string zipPath;

            if (isFolder)
            {
                var folder = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync(null);
                if (folder.Folder == null)
                {
                    StatusLabel.Text = "No folder selected.";
                    return;
                }

                var selectedPath = folder.Folder.Path;
                StatusLabel.Text = "Zipping folder...";
                zipPath = FileHelper.CreateZipFromFolder(selectedPath, email);
            }
            else
            {
                var results = await FilePicker.Default.PickMultipleAsync();
                if (results == null || results.Count() == 0)
                {
                    StatusLabel.Text = "No files selected.";
                    return;
                }

                if (results.Count() == 1)
                {
                    var selectedFile = results.First().FullPath;

                    if (FileHelper.IsZipFile(selectedFile))
                    {
                        StatusLabel.Text = "File is already a .zip, skipping compression...";
                        zipPath = selectedFile; // use directly
                    }
                    else
                    {
                        StatusLabel.Text = "Zipping single file...";
                        zipPath = FileHelper.CreateZipFromFile(selectedFile, email);
                    }
                }
                else
                {
                    var tempFolder = Path.Combine(FileSystem.CacheDirectory, "TempUpload_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
                    Directory.CreateDirectory(tempFolder);

                    foreach (var file in results)
                    {
                        var dest = Path.Combine(tempFolder, Path.GetFileName(file.FullPath));
                        File.Copy(file.FullPath, dest, overwrite: true);
                    }

                    StatusLabel.Text = "Zipping multiple files...";
                    zipPath = FileHelper.CreateZipFromFolder(tempFolder, email);
                }
            }

            await UploadZipFile(zipPath);
        }

        private async Task UploadZipFile(string zipPath)
        {
            StatusLabel.Text = $"Connecting to {SftpHost}...";
            try
            {
                using var client = new SftpClient(SftpHost, username, password);
                client.Connect();

                var remoteFileName = "/" + Path.GetFileName(zipPath);
                using var fileStream = File.OpenRead(zipPath);

                StatusLabel.Text = $"Uploading '{Path.GetFileName(zipPath)}'...";
                client.UploadFile(fileStream, remoteFileName);

                StatusLabel.Text = $"Upload successful! File uploaded as '{remoteFileName}'";
                client.Disconnect();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Upload failed: {ex.Message}";
                Debug.WriteLine(ex);
            }
        }
    }
}
