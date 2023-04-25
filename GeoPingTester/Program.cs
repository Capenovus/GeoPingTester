using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace GeoPingTester
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 0) { Console.WriteLine("Please specify a file"); return; }
            string path = args[0];
            LoadFile(path, out string[]? content);
            if (content == null) { Console.WriteLine("File is empty"); return; }

            List<string> array = new();
            foreach (string line in content)
            {
                try { IPAddress.Parse(line); array.Add(line); }
                catch (FormatException)
                {
                    if (!line.Contains('/')) { Console.WriteLine("no subnet"); continue; }
                    string[] a = line.Split('/');
                    if (a.Length > 2) { Console.WriteLine("too much subnet"); continue;}
                    if (!int.TryParse(a[1], out int res)) { Console.WriteLine("invalid subnet"); continue; }
                    int b = SubnetMaskIpRange(res);
                    if (res < 24 || res > 32) { Console.WriteLine("Subnet too big or too small"); continue; }
                    string[] c = line.Split('.');
                    if (c.Length > 4) { Console.WriteLine("ip error"); continue; }

                    for (int i = 0; i < b; i++) { array.Add($"{c[0]}.{c[1]}.{c[2]}.{i}"); }
                }
                catch { Console.WriteLine("Some Error"); continue; }
            }

            if (array.Count == 0) { Console.WriteLine("arr empty"); return; }
            Console.Write($"Testing {array.Count} ips, continue? Y|N: ");
            var key = Console.ReadKey();
            if (key.Key != ConsoleKey.Y) return;
            Console.WriteLine("\nContinuing");
            var title = Console.Title;
            var results = RunTests(array);
            Console.Title = title;

            var f = File.OpenWrite("results.txt");
            f.Write(new UTF8Encoding(true).GetBytes("IP | COUNTRYCODE | CITY | ISP | REVERSE LOOKUP"));
            Console.WriteLine(results.Count);
            foreach (var result in results) 
            {
                f.Write(new UTF8Encoding(true).GetBytes($"\n{result[0]} | {result[1]} | {result[2]} | {result[3]} | {result[4]}"));
            }
            f.Close();
            Console.WriteLine("Finished");
            Console.ReadKey();
        }

        static List<List<string>> RunTests(List<string> array) // ip, countrycode, city, isp, reverse
        {
            // Better: Launch additional processes

            List<List<string>> datastuff = new();
            int numThreads = array.Count / Environment.ProcessorCount;
            int completed = 0;
            ServicePointManager.DefaultConnectionLimit = numThreads;
            Parallel.For(0, array.Count,
                new ParallelOptions { MaxDegreeOfParallelism = numThreads}, (i) => 
                {
                    // Remove items from copy and list remaining
                    JObject? data = GetData(array[i]);
                    string? dns = null;
                    try
                    {
                        dns = Dns.GetHostEntry(array[i]).HostName;
                    }
                    catch { }
                    if (data != null) datastuff.Add(new() { array[i], (string)data["country"]!["iso_code"]!, (string)data["city"]!["names"]!["en"]!, (string)data["traits"]!["isp"]!, dns! });
                    completed++;
                    Console.Title = $"{completed}/{array.Count} completed";
                }
            );
            return datastuff.OrderBy(x => x[1]).ToList();
        }

        static JObject? GetData(string ip)
        {
            const string API_URL = @"https://api.findip.net/";
            const string API_TOKEN = @"";
            try
            {
                Ping p = new();
                PingReply reply = p.Send(ip);
                if (reply.Status != IPStatus.Success) { return null; }

                HttpClient httpClient = new();
                return JObject.Parse(httpClient.GetAsync($"{API_URL}{ip}/?token={API_TOKEN}").Result.Content.ReadAsStringAsync().Result);
            }
            catch (Exception e){ Console.WriteLine(e); }
            return null;
        }
        
        static void LoadFile(string path, out string[]? content)
        {
            try { content = File.ReadAllLines(path); }
            catch { content = null; }
        }

        static int SubnetMaskIpRange(int SubnetMask) => (int)Math.Pow(2, 32 - SubnetMask);
    }
}