using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace DripDownloader
{
    class Program
    {
        static void Main(string[] args)
        {

            
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: dripdownloader.exe USERNAME PASSWORD FULLPATHTOSAVETO");
                return;
            }



            var userName = args[0];
            var password = args[1];

            var saveFolder = args[2]; //@"Y:\drip\";

       

            var sw = new Stopwatch();
            var cc = new CookieContainer();
            var h = new HttpClientHandler{CookieContainer = cc};
            h.AllowAutoRedirect = true;
            h.UseCookies = true;

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;


            using (var c = new HttpClient(h))
            {
                var baseAddy = new Uri("https://drip.com");
                c.BaseAddress = baseAddy;
                c.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                c.DefaultRequestHeaders.Host = "drip.com";
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));


                c.Timeout = TimeSpan.FromHours(10);

                sw.Start();
                var r = c.GetAsync("/").Result;
                if (!r.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed initial get.");
                    return;
                }
                
                Console.WriteLine("Logging in...");
                
                var body = string.Format(@"{{""email"":""{0}"",""password"":""{1}""}}", userName, password);
                
                 
                var login = c.PostAsync("/api/users/login", new StringContent(body)).Result;
                if (!login.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed login.");
                    return;
                }

                var loginResponse = login.Content.ReadAsStringAsync().Result;
                var userid = Regex.Match(loginResponse, @"{""id"":(?<id>[^,]+),""email""").Groups["id"].Value.Trim();



                var releases = LoadAvailableReleases(c, userid);

                var totalCount = releases.Count;
                Console.WriteLine("Found Releases: " + totalCount);
                var counter = 0;
                var f = new Fetcher(c);
                foreach (var release in releases)
                {
                    f.GetRelease(saveFolder, release);

                    counter ++;
                    Console.WriteLine("Done {0} of {1} --- {2}%", counter, totalCount, counter*100/totalCount);
                }

                sw.Stop();

                Console.WriteLine("Done! Took {0} seconds, Saved {1} releases", sw.Elapsed.TotalSeconds.ToString("##.##"),f.Saved);
               

            }

        }

        private static List<Release> LoadAvailableReleases(HttpClient c, string userid)
        {
            var releases = new List<Release>();
            for (var i = 1; i < 100; i++)
            {
                Console.WriteLine("Fetching release list page: " + i);
                var page = c.GetStringAsync(string.Format("/api/users/{1}/releases?page={0}", i, userid)).Result;
                bool found = false;
                foreach (Match match in Regex.Matches(page, @"""id"":(?<release>[^,]+),""creative_id"":(?<dripid>[^,]+),"""))
                {
                    var releaseId = match.Groups["release"].Value.Trim();
                    var dripId = match.Groups["dripid"].Value.Trim();
                    releases.Add(new Release{DripId = int.Parse(dripId), ReleaseId = int.Parse(releaseId)});
                    found = true;
                }
                if (!found) break;
            }
            return releases;
        }
    }
}
