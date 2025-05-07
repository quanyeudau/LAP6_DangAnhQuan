using ASC.Model.BaseTypes;
using System.Collections.Generic;

namespace ASC.Web.Areas.Configuration.Models
{
    public class MasterKeysViewModel
    {
        public bool IsEdit { get; set; }

        public MasterDataKeyViewModel MasterKeyInContext { get; set; } 

        public List<MasterDataKeyViewModel> MasterKeys { get; set; }
    }


}

