using System.ComponentModel.DataAnnotations;

namespace ASC.Web.Areas.Configuration.Models
{
    public class MasterDataKeyViewModel
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }

        public bool IsActive { get; set; }

        [Required]
        public string Name { get; set; }
    }
}