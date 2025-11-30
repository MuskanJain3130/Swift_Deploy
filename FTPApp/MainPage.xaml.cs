using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel; // for Clipboard
using Renci.SshNet;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FTPApp
{
    public partial class MainPage : ContentPage
    {
        // Hardcoded SFTP host here:
        string SftpHost = "";
        string username = "";
        string password = "";
        string CredentialsUrl = "http://localhost:5280/api/ftp/azure/creds"; // Replace with your actual API URL
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


        // Add this method to request SFTP/Azure creds from your backend
        private async Task<bool> RequestAzureCredsFromServerAsync(string appUsername, string appPassword, string email)
        {
            try
            {
                var payload = new
                {
                    Username = appUsername ?? string.Empty,
                    Password = appPassword ?? string.Empty,
                    Email = email ?? string.Empty
                };

                using var client = new HttpClient();
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await client.PostAsync(CredentialsUrl, content);
                var respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    try
                    {
                        var doc = JsonDocument.Parse(respText);
                        if (doc.RootElement.TryGetProperty("message", out var msg))
                            StatusLabel.Text = $"Auth failed: {msg.GetString()}";
                        else
                            StatusLabel.Text = $"Auth failed: {resp.StatusCode}";
                    }
                    catch
                    {
                        StatusLabel.Text = $"Auth failed: {resp.StatusCode}";
                    }
                    return false;
                }

                var docRoot = JsonDocument.Parse(respText).RootElement;
                if (docRoot.TryGetProperty("azure", out var azure))
                {
                    // Ensure SftpHost is not const so you can set it here
                    SftpHost = azure.GetProperty("host").GetString() ?? string.Empty;
                    username = azure.GetProperty("username").GetString() ?? string.Empty;
                    password = azure.GetProperty("password").GetString() ?? string.Empty;

                    StatusLabel.Text = "Authenticated — SFTP credentials acquired.";
                    return true;
                }

                StatusLabel.Text = "Unexpected server response.";
                return false;
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Request error: {ex.Message}";
                Debug.WriteLine(ex);
                return false;
            }
        }

        private async Task UploadFolderOrFiles(bool isFolder)
        {
            var email = EmailEntry.Text?.Trim();


            StatusLabel.Text = isFolder ? "Picking folder..." : "Picking file(s)...";

            // ensure these lines run inside the upload click handler(s) before using username/password
            username = UsernameEntry.Text ?? string.Empty;
            password = PasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusLabel.Text = "Please enter your SFTP username and password.";
                return;
            }

            var appUser = UsernameEntry?.Text?.Trim();    // replace with the UI Entry you use for app creds
            var appPass = PasswordEntry?.Text;            // replace with the UI Entry you use for app creds

            if (string.IsNullOrEmpty(appUser) || string.IsNullOrEmpty(appPass))
            {
                StatusLabel.Text = "Please enter your application username and password.";
                return;
            }

            var got = await RequestAzureCredsFromServerAsync(appUser, appPass, email);
            if (!got)
                return; // StatusLabel already set


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

                // show loading after folder picked
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                StatusLabel.Text = "Zipping folder...";

                zipPath = FileHelper.CreateZipFromFolder(selectedPath, email);

                // hide loading for zip preparation (upload will show again)
                LoadingIndicator.IsRunning = false;
            }
            else
            {
                var results = await FilePicker.Default.PickMultipleAsync();
                if (results == null || results.Count() == 0)
                {
                    StatusLabel.Text = "No files selected.";
                    return;
                }

                // show loading after files picked
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

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

                // stop preparation indicator (upload will set indicator again)
                LoadingIndicator.IsRunning = false;
            }

            // start upload indicator
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            await UploadZipFile(zipPath);

            // ensure loading cleared
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }

        private async Task UploadZipFile(string zipPath)
        {
            StatusLabel.Text = $"Connecting to {SftpHost}...";
            try
            {
                using var client = new SftpClient(SftpHost, username, password);
                client.Connect();

                var remoteFileName = Path.GetFileName(zipPath);
                using var fileStream = File.OpenRead(zipPath);

                StatusLabel.Text = $"Uploading '{Path.GetFileName(zipPath)}'...";
                client.UploadFile(fileStream, remoteFileName);

                StatusLabel.Text = $"Upload successful! File uploaded as '{remoteFileName}'";
                client.Disconnect();

                // show uploaded filename and enable copy
                UploadedFileLabel.Text = remoteFileName;
                UploadedFileLabel.IsVisible = true;
                CopyFilenameButton.IsVisible = true;
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Upload failed: {ex.Message}";
                Debug.WriteLine(ex);

                // hide filename/copy on failure
                UploadedFileLabel.IsVisible = false;
                CopyFilenameButton.IsVisible = false;
            }
            finally
            {
                // ensure loading cleared
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnCopyFilenameClicked(object sender, EventArgs e)
        {
            var text = UploadedFileLabel?.Text;
            if (string.IsNullOrEmpty(text))
            {
                StatusLabel.Text = "No filename to copy.";
                return;
            }

            try
            {
                await Clipboard.SetTextAsync(text);
                StatusLabel.Text = "Filename copied to clipboard.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Copy failed: {ex.Message}";
                Debug.WriteLine(ex);
            }
        }
    }
}
