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



                var releases = new List<Tuple<int, int>>();
                for (var i = 1; i < 100; i++)
                {
                    Console.WriteLine("Fetching release list page: " + i);
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
                var totalCount = releases.Count;
                Console.WriteLine("Found Releases: " + totalCount);
                var counter = 0;
                foreach (var release in releases)
                {
                    if (!TryGetRelease(saveFolder, release.Item1, release.Item2, c, "flac")) 
                     if (!TryGetRelease(saveFolder, release.Item1, release.Item2, c, "wav"))
                      if (!TryGetRelease(saveFolder, release.Item1, release.Item2, c, "mp3")) 
                       if (!TryGetRelease(saveFolder, release.Item1, release.Item2, c, "aiff")) continue;

                    counter ++;
                    Console.WriteLine("Done {0} of {1} --- {2}%", counter, totalCount, counter*100/totalCount);
                }



                Console.WriteLine("done?");
                Console.ReadLine();

            }

        }
     

        private static bool TryGetRelease(string saveFolder, int dripId, int releaseId, HttpClient c, string format)
        {
            

            Console.WriteLine("Attempting to Download: Drip {0} Release {1}",dripId, releaseId);

            var requestUri = string.Format("/api/creatives/{0}/releases/{1}/download?release_format={2}", dripId,releaseId,format);

           
            var g = c.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);


           var postG = g.ContinueWith((resp) =>
            {
                using (var responseMessage = resp.Result)
                {
                    if (responseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("Drip {0} Release {1} in {3} No Good: {2}", dripId, releaseId,
                            responseMessage.StatusCode, format);
                        return false;
                    }

                    var fname = Path.GetFileName(responseMessage.RequestMessage.RequestUri.LocalPath);

                   

                    var dir = Path.Combine(saveFolder, dripId.ToString()).ToString();
                    Directory.CreateDirectory(dir);
                    var newFile = Path.Combine(dir, fname);

                    var tempName = newFile + ".temp";
                    
                    if (File.Exists(newFile))
                    {
                        Console.WriteLine("Already Have: " + fname);
                        return true;
                    }
               

                    Console.WriteLine("Attempting to save: " + fname);

                    try
                    {
                        using (var f =new FileStream(tempName, FileMode.Create))
                        {
                            responseMessage.Content.CopyToAsync(f).Wait();
                        }
                        File.Move(tempName, newFile);
                        Console.WriteLine("Successfully saved: " + fname);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("UnSuccessfully saved: " + fname + " :: " + ex.Message);
                        return true; //try again later
                    }
                }
                
            });
          
            return postG.Result;
        }
    }
}
