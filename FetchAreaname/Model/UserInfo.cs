using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FetchAreaname.Model
{
    public class UserInfo
    {
        private Guid _id;   

        public UserInfo()
        {
            _id = Guid.NewGuid();   
        }

        public string UserId { get; set; }
        public string TrueName { get; set; }
        public string MobilePhone { get; set; }
        public string HomePhone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }

        public string CityName { get; set; }

        public string RegionName { get; set; }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}
