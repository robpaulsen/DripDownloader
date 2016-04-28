namespace DripDownloader
{
    public class Release
    {
        public int DripId { get; set; }
        public int ReleaseId { get; set; }

        public override string ToString()
        {
         
            return DripId + ":" + ReleaseId;
        
        }
    }
}