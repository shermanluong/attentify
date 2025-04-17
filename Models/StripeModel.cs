using System.ComponentModel.DataAnnotations;

namespace GoogleLogin.Models
{
    public class TbPlan
    {
        [Key]
        public long id { get; set; }
        public string planName { get; set; }
        public int planLevel { get; set; }
        public string priceId { get; set; }

    }

    public class TbUserPlan
    {
        [Key]
        public long id { get; set;}
        public string userEmail { get; set; }
        public long planId { get; set; }
        public long expire { get; set; }
    }

    public class UserPlanDetail
    {
        public long id { get; set; }
        public string userEmail { get; set; }
        public string planName { get; set; }
        public int planLevel { get; set; }
        public string priceId { get; set; }
        public long expire { get; set; }
    }
}
