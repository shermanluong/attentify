namespace GoogleLogin.Services
{
    public class Global
    {
        public static List<ulong> lstHistoryIds = new List<ulong>();
        public static string access_token = string.Empty;
        public static List<string> lstLabelIds = new List<string>();

        public static bool isStartPubSub = false;
    }
}
