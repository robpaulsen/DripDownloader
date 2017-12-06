using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DripDownloader
{
    class Program
    {
        static void Main(string[] args)
        {


            if (args.Length != 3)
            {
                Console.WriteLine("Usage: dripget USERNAME PASSWORD FULLPATHTOSAVETO");
                return;
            }



            var userName = args[0];
            var password = args[1];

            var saveFolder = args[2]; //@"Y:\drip\";


            var sw = new Stopwatch();

            sw.Start();


            var alreadyFetched = GetAlreadyFetched(saveFolder);
            var alreadyFetchedLookup = alreadyFetched.ToLookup(r=>r.ToString());


            using (var f = new Fetcher())
            {

                var userid = f.Login(userName, password);
                if (userid == null) return;

                var availableReleases = f.LoadAvailableReleases(userid);
                var missingReleases = availableReleases.Where(r=>!alreadyFetchedLookup.Contains(r.ToString())).ToList();

                Console.WriteLine("Found Total Releases: {0} Missing: {1}", availableReleases.Count, missingReleases.Count);

                var counter = 0;

                var newlyAquired = new List<Release>();
                foreach (var release in missingReleases)
                {
                    if (f.GetRelease(saveFolder, release)) newlyAquired.Add(release);
                    AppendFetched(saveFolder,release);
                    counter++;
                    Console.WriteLine("Done {0} of {1} --- {2}%", counter, missingReleases.Count, counter * 100 / missingReleases.Count);
                }


                alreadyFetched.AddRange(newlyAquired);

                WriteAlreadyfetched(saveFolder, alreadyFetched);


                sw.Stop();

                Console.WriteLine("Done! Took {0} seconds, Saved {1} releases", sw.Elapsed.TotalSeconds.ToString("##.##"), f.Saved);

            }

        }

        private static void WriteAlreadyfetched(string saveFolder, List<Release> alreadyFetched)
        {

            var path = Path.Combine(saveFolder, "fetched.txt");
            File.WriteAllLines(path, alreadyFetched.Select(r => r.ToString()));

        }

        private static void AppendFetched(string saveFolder, Release newlyFetched)
        {

            var path = Path.Combine(saveFolder, "fetched.txt");
            File.AppendAllLines(path, new[] { newlyFetched.ToString() });

        }


        private static List<Release> GetAlreadyFetched(string saveFolder)
        {
            var path = Path.Combine(saveFolder, "fetched.txt");
            var fetched = new List<Release>();
            if (!File.Exists(path)) return fetched;
            
            using (var f = File.OpenText(path))
            {
                string line;
                while ((line = f.ReadLine()) != null)
                {
                    var split = line.Split(':');
                    fetched.Add(new Release
                    {
                        DripId = int.Parse(split[0]),
                        ReleaseId = int.Parse(split[1])
                    });
                }

            }
            
            return fetched;
        }
    }
}
