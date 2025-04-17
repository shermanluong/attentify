using System.ComponentModel.DataAnnotations;

namespace GoogleLogin.Models
{
    public class TbCompany
    {
        [Key]
        public long id { get; set; }
        public string name { get; set; }
        public string site { get; set; }
    }

    public class TbMember
    {
        [Key]
        public long id { get; set;}
        public string email { get; set; }
        public long companyIdx { get; set; }
        public int role { get; set; }
    }
}
