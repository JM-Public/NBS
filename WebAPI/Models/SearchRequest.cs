namespace WebApi.Models
{
    public class SearchRequest
    {
        public double latitude { get; set; }

        public double longitude { get; set; }

        public string keyword { get; set; }
    }
}
