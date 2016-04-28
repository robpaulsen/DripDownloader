using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace DripDownloader
{
    public class Fetcher
    {
        private readonly HttpClient _c;
        public int Attempted { get; set; }
        public int Saved { get; set; }
        public int Skipped { get; set; }


        public Fetcher(HttpClient c)
        {
            _c = c;
        }

        public void GetRelease(string saveFolder, Release release)
        {

            Attempted++;

            new[] {"flac", "wav", "mp3", "aiff"}.Any(
                f => TryGetRelease(saveFolder, release.DripId, release.ReleaseId, f)
                );
         



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
                        return true; //try again later
                    }
                }

            });

            return postG.Result;
        }

        public int Errors { get; set; }
    }
}