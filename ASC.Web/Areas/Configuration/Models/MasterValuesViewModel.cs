
namespace ASC.Web.Areas.Configuration.Models
{
    public class MasterValuesViewModel
    {
        internal List<MasterDataValueViewModel> MasterValues;

        public List<MasterDataKeyViewModel> MasterKeys { get; set; } = new();
        public MasterDataKeyViewModel MasterKeyInContext { get; set; } = new(); // Gán mặc định
        public bool IsEdit { get; set; }
    }
}
