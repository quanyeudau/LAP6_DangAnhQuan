using ASC.Model.BaseTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASC.Model.Models
{
    public class MasterDataValue : BaseEntity, IAuditTracker
    {
        public MasterDataValue() { } // ✅ EF Core cần constructor này để tạo object

        public MasterDataValue(string masterDataPartitionKey, string value, string name)
        {
            this.PartitionKey = masterDataPartitionKey;
            this.RowKey = Guid.NewGuid().ToString();
            this.Name = name;
        }

        public bool IsActive { get; set; }
        public string Name { get; set; }
    }
}
