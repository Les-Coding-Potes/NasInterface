using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NasInterface
{
    public class DiskManager
    {
        private readonly HttpClient client;
        public string UsedSpace { get; set; }
        public string TotalSpace { get; set; }

        public DiskManager()
        {
            client = new HttpClient();
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

                    dynamic result = JsonConvert.DeserializeObject(jsonResponse);
                    UsedSpace = result.used_space;
                    TotalSpace = result.total_space;
                }
                else
                {
                    Console.WriteLine("Request error : " + response.StatusCode);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Error : {e.Message}");
            }
        }
    }
}
