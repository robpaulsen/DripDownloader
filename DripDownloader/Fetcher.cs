using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace DripDownloader
{
    public class Fetcher : IDisposable
    {
        private HttpClient _c;
        public int Attempted { get; set; }
        public int Saved { get; set; }
        public int Skipped { get; set; }


        public bool GetRelease(string saveFolder, Release release)
        {

            Attempted++;

            return new[] {"flac", "wav", "mp3", "aiff"}.Any(
                f => TryGetRelease(saveFolder, release.DripId, release.ReleaseId, f)
                );
         

        }

        public string Login(string userName, string password)
        {
            Initialize();

            var r = _c.GetAsync("/").Result;
            if (!r.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed initial get.");
                return null;
            }

            Console.WriteLine("Logging in...");

            var body = string.Format(@"{{""email"":""{0}"",""password"":""{1}""}}", userName, password);


            var login = _c.PostAsync("/api/users/login", new StringContent(body)).Result;
            if (!login.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed login.");
                return null;
            }

            var loginResponse = login.Content.ReadAsStringAsync().Result;
            return Regex.Match(loginResponse, @"{""id"":(?<id>[^,]+),""email""").Groups["id"].Value.Trim();

        }

        private void Initialize()
        {
            var cc = new CookieContainer();
            var h = new HttpClientHandler { CookieContainer = cc };
            h.AllowAutoRedirect = true;
            h.UseCookies = true;

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            var c = new HttpClient(h);

            var baseAddy = new Uri("https://drip.com");
            c.BaseAddress = baseAddy;
            c.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            c.DefaultRequestHeaders.Host = "drip.com";
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));


            c.Timeout = TimeSpan.FromHours(10);
            _c = c;

        }

        public List<Release> LoadAvailableReleases(string userid)
        {
            var releases = new List<Release>();
            for (var i = 1; i < 100; i++)
            {
                Console.WriteLine("Fetching release list page: " + i);
                var page = _c.GetStringAsync(string.Format("/api/users/{1}/releases?page={0}", i, userid)).Result;
                bool found = false;
                foreach (Match match in Regex.Matches(page, @"""id"":(?<release>[^,]+),""creative_id"":(?<dripid>[^,]+),"""))
                {
                    var releaseId = match.Groups["release"].Value.Trim();
                    var dripId = match.Groups["dripid"].Value.Trim();
                    releases.Add(new Release { DripId = int.Parse(dripId), ReleaseId = int.Parse(releaseId) });
                    found = true;
                }
                if (!found) break;
            }
            return releases;
        }

        private bool TryGetRelease(string saveFolder, int dripId, int releaseId, string format)
        {


            Console.WriteLine("Attempting to Download: Drip {0} Release {1} in {2}", dripId, releaseId, format);

            var requestUri = string.Format("/api/creatives/{0}/releases/{1}/download?release_format={2}", dripId, releaseId, format);


            var g = _c.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);


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
                        Skipped++;
                        Console.WriteLine("Already Have: " + fname);
                        return true;
                    }


                    Console.WriteLine("Attempting to save: " + fname);

                    try
                    {
                        using (var f = new FileStream(tempName, FileMode.Create))
                        {
                            responseMessage.Content.CopyToAsync(f).Wait();
                        }
                        File.Move(tempName, newFile);
                        Saved++;
                        Console.WriteLine("Successfully saved: " + fname);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Errors++;
                        Console.WriteLine("UnSuccessfully saved: " + fname + " :: " + ex.Message);
                        throw;
                    }
                }

            });

            return postG.Result;
        }

        public int Errors { get; set; }
        public void Dispose()
        {
            if (_c == null) return;
            ((IDisposable) _c).Dispose();
            _c = null;
        }
    }
}