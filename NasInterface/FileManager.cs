using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NasInterface
{
    public enum FileType
    {
        Directory,
        File
    }
    public class NasFile
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Type { get; set; }
        public List<NasFile> Directories { get; set; }
        public List<NasFile> Files { get; set; }
    }


    public class FileManager
    {
        static HttpClient client = new HttpClient();


        public FileManager()
        {

        }

        public async Task GetDiskSpace(string ipAddress, string port)
        {
            try
            {
                string url = $"http://{ipAddress}:{port}/disk-space";
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(jsonResponse); 
                }
                else
                {
                    Console.WriteLine("La requête a échoué avec le code : " + response.StatusCode);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request Error: {e.Message}");
            }
        }

        public async Task<NasFile> GetFiles(string ipAddress, string port)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri($"http://{ipAddress}:{port}/");

                HttpResponseMessage response = await client.GetAsync("list-files");

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    NasFile rootFile = JsonConvert.DeserializeObject<NasFile>(content);

                    return rootFile;
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Erreur : {errorMessage}");
                }
            }
            return null;
        }

        public async Task<bool> Upload(string ipAddress, string port, string filepath, string destPath)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{ipAddress}:{port}/upload");
            var content = new MultipartFormDataContent();

            string filename = Path.GetFileName(filepath);
            content.Add(new StreamContent(File.OpenRead(filepath)), "file", filename);
            content.Add(new StringContent(destPath), "folderDest");

            request.Content = content;

            try
            {
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                return false;
                //throw new Exception(ex.Message);
            }
        }
        public async Task<bool> Download(string ipAddress, string port, string path)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync($"http://{ipAddress}:{port}/download?path={WebUtility.UrlEncode(path)}");

                    if (response.IsSuccessStatusCode)
                    {
                        string downloadPath;
                        using (var folderDialog = new FolderBrowserDialog())
                        {
                            DialogResult result = folderDialog.ShowDialog();
                            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                            {
                                MessageBox.Show("No folder selected.");
                                return false;
                            }
                            downloadPath = folderDialog.SelectedPath;
                        }

                        string suggestedFileName = response.Content.Headers.ContentDisposition.FileNameStar;
                        if (string.IsNullOrEmpty(suggestedFileName))
                        {
                            suggestedFileName = response.Content.Headers.ContentDisposition.FileName;
                        }

                        if (string.IsNullOrEmpty(suggestedFileName))
                        {
                            MessageBox.Show("The server did not provide a file name.");
                            return false;
                        }

                        using (FileStream fileStream = File.Create(Path.Combine(downloadPath, suggestedFileName)))
                        {
                            await response.Content.CopyToAsync(fileStream);
                        }

                        MessageBox.Show("Download successful.");
                        return true;
                    }
                    else
                    {
                        MessageBox.Show($"Error downloading: {response.ReasonPhrase}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading: {ex.Message}");
                return false;
            }
        }






        public async Task<bool> Delete(string ipAddress, string port, string path)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Delete, $"http://{ipAddress}:{port}/delete?path={Uri.EscapeDataString(path)}");

                    var response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        MessageBox.Show($"Erreur lors de la suppression : {response.ReasonPhrase}");
                        return false;
                    }
                    else
                    {
                        MessageBox.Show($"Erreur lors de la suppression : {response.ReasonPhrase}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la suppression : {ex.Message}");
                return false;
            }
        }

    }
}
