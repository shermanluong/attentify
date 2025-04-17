using System.ComponentModel.DataAnnotations;

namespace GoogleLogin.Models
{
    public class TbMailAccount
    {
        [Key]
        public long id { get; set; }
        public string mail { get; set; } 
        public string accessToken { get; set; } // google account access token
        public string refreshToken { get; set; } // google account refresh token
        public string userId { get; set; }
    }

    public class TbEmail
    {
        [Key]
        public long em_idx { get; set; }
        public string em_id { set; get; }
        public string? em_subject { set; get; }
        public string? em_body { set; get; }
        public string em_from { set; get; }
        public string em_to { set; get; }
        public string? em_replay { set; get; }
        public Nullable<int> em_state { set; get; }
        public string? em_threadId { set; get; }
        public Nullable<int> em_level { set; get; }
        public Nullable<DateTime> em_date { set; get; }
        public Nullable<int> em_read { set; get; }
    }
    public class TbTwilio
    {
        [Key]
        public long id { get; set; }
        public string userid { get; set; }
        public string accountsid { get; set; }
        public string authtoken { get; set; } // google account access token
        public string phonenumber { get; set; } // google account refresh token
        public TbTwilio() {
            userid       = string.Empty;
            accountsid  = string.Empty;
            authtoken   = string.Empty;
            phonenumber = string.Empty;
        }
    }

    public class TwilioSaveModel
    {
        public string? accountsid { get; set; }
        public string? authtoken { get; set; }
        public string? phonenumber { get; set; }

        public TwilioSaveModel()
        {
            accountsid  = string.Empty;
            authtoken   = string.Empty;
            phonenumber = string.Empty;
        }
    }

    public class TbSms
    {
        [Key]
        public long sm_idx { get; set; }
        public string sm_id { set; get; }
        public string sm_to { set; get; }
        public string sm_body { set; get; }
        public string sm_from { set; get; }
        public Nullable<DateTime> sm_date { set; get; }
        public Nullable<int> sm_read { set; get; }
        public Nullable<int> sm_state { set; get; }

        public TbSms()
        {
            sm_id = string.Empty;
            sm_to = string.Empty;
            sm_body = string.Empty;
            sm_from = string.Empty;
        }
    }
    public class TbOrder
    {
        [Key]
        public long od_idx { get; set; }
        public long or_id { set; get; }
        public string or_name { set; get; }
        public Nullable<DateTime> or_date { set; get; }
        public string? or_customer { set; get; }
        public string? or_channel { set; get; }
        public Nullable<double> or_total { set; get; }
        public Nullable<int> or_payment_status { set; get; }
        public Nullable<int> or_fulfill_status { set; get; }
        public Nullable<int> or_itemCnt { set; get; }
        public Nullable<int> or_delivery_status { set; get; }
        public Nullable<int> or_delivery_method { set; get; }
        public string? or_tags { set; get; }
        public Nullable<int> or_status { set; get; }
        public string or_owner { set; get; }
        public string? or_phone { set; get; }
        public string? or_customer_name { set; get; }
    }
    public class ResetPassword
    {
        [Required]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public string Email { get; set; }
        public string Token { get; set; }
    }
    public class TbShopifyLog
    {
        [Key]
        public long idx { get; set; }
        public string UserId { get; set; } // User who performed the action
        public string Action { get; set; } // Description of the action (e.g., "Order Canceled")
        public int OrderId { get; set; } // Associated order ID
        public Nullable<DateTime> Timestamp { get; set; } // Date and time of the action
    }
    public class TbShopifyToken
    {
        [Key]
        public long idx { get; set; }
        public int Id { get; set; }
        public string? UserId { get; set; } // Links to the authenticated user in your system
        public string AccessToken { get; set; } // Shopify API access token
        public string ShopDomain { get; set; } // The Shopify store domain (e.g., "example.myshopify.com")
        public Nullable<DateTime> DateCreated { get; set; }
        public Nullable<DateTime> DateUpdated { get; set; }
    }
    public class TbShopifyUser
    {
        [Key]
        public long idx { get; set; }
        public string UserId { get; set; } // user email
        public string UserName { get; set; }
        public string UserShopifyDomain { set; get; }
        public string User_Id { get; set; } //customer id
        public Nullable<DateTime> createdAt { set; get; }
        public Nullable<DateTime> updatedAt { set; get; }
        public string? phone { set; get; }
        public string? address1 { set; get; }
        public string? address2 { set; get; }
        public string? city { set; get; }
        public string? province { set; get; }
        public string? country { set; get; }
        public string? province_code { set; get; }
        public string? zip { set; get; }
    }
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
