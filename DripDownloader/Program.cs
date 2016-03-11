using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            Console.WriteLine("Starting Scan...");


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


                var r = c.GetAsync("/").Result;
                if (!r.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed initial get.");
                    return;
                }

                var body = string.Format(@"{{""email"":""{0}"",""password"":""{1}""}}", userName, password);
                
                 
                var login = c.PostAsync("/api/users/login", new StringContent(body)).Result;
                if (!login.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed login.");
                    return;
                }

                var loginResponse = login.Content.ReadAsStringAsync().Result;
                var userid = Regex.Match(loginResponse, @"{""id"":(?<id>[^,]+),""email""").Groups["id"].Value.Trim();



                var releases = new List<Tuple<int, int>>();
                for (var i = 1; i < 100; i++)
                {
                    Console.WriteLine("Looking for Releases: " + i);
                    var page = c.GetStringAsync(string.Format("/api/users/{1}/releases?page={0}", i, userid)).Result;
                    bool found=false;
                    foreach (Match match in Regex.Matches(page, @"""id"":(?<release>[^,]+),""creative_id"":(?<dripid>[^,]+),"""))
                    {
                        var releaseId = match.Groups["release"].Value.Trim();
                        var dripId = match.Groups["dripid"].Value.Trim();
                        releases.Add(new Tuple<int, int>(int.Parse(dripId),int.Parse(releaseId)));
                        found = true;
                    }
                    if (!found) break;
                }
                Console.WriteLine("Found Releases: " + releases.Count);
                foreach (var release in releases)
                {
                    TryGetRelease(saveFolder, release.Item1, release.Item2, c);
                }



                Console.WriteLine("done?");
                Console.ReadLine();

            }

        }
     

        private static void TryGetRelease(string saveFolder, int dripId, int releaseId, HttpClient c)
        {
            

            Console.WriteLine("Attempting to Download: Drip {0} Release {1}",dripId, releaseId);

            var requestUri = string.Format("/api/creatives/{0}/releases/{1}/download?release_format=flac", dripId,releaseId);

           
            var g =
                c.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);


           var postG = g.ContinueWith((resp) =>
            {
                using (var responseMessage = resp.Result)
                {
                    if (responseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("Drip {0} Release {1} No Good: {2}", dripId, releaseId,
                            responseMessage.StatusCode);
                        return;
                    }

                    var fname = Path.GetFileName(responseMessage.RequestMessage.RequestUri.LocalPath);


                    
                    var dir = Path.Combine(saveFolder, dripId.ToString()).ToString();
                    Directory.CreateDirectory(dir);
                    var newFile = Path.Combine(dir, fname);

                    if (File.Exists(newFile))
                    {
                        Console.WriteLine("Already Have: " + fname);
                        return;
                    }
               

                    Console.WriteLine("Attempting to save: " + fname);


                    using (
                        var f =
                            new FileStream(
                              newFile, FileMode.Create))
                    {
                        try
                        {
                            responseMessage.Content.CopyToAsync(f).Wait();
                            Console.WriteLine("Successfully saved: " + fname);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("UnSuccessfully saved: " + fname + " :: " + ex.Message);
                        }
                    }
                }
              
            });
            postG.Wait();
        }
    }
}
