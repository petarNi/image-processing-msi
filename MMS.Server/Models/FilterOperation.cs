namespace MMS.Server.Models
{
    public class FilterOperation
    {
        public string Name { get; set; } = string.Empty;
        public int? Amount { get; set; }
        public int? BlockSize { get; set; }
        public double? Factor { get; set; }
    }
}