namespace SensePC.Desktop.WinUI.Services
{
    /// <summary>
    /// API configuration with all endpoint URLs for SensePC services
    /// Using DEV environment endpoints
    /// </summary>
    public static class ApiConfig
    {
        // Authentication (DEV)
        public const string UserPoolId = "us-east-1_vgBCKmL0c";
        public const string UserPoolClientId = "2lknj90rkjmtkcnph06q6r93ug";
        public const string OAuthDomain = "auth.smartpc.cloud";

        // PC Management APIs (DEV)
        public const string FetchPCUrl = "https://vj9idlbwf7.execute-api.us-east-1.amazonaws.com/prod/FetchPCdata";
        public const string VmManagementUrl = "https://lul5oxdwic.execute-api.us-east-1.amazonaws.com/dev/instance";
        public const string InstanceDetailsUrl = "https://4oacxj1xyk.execute-api.us-east-1.amazonaws.com/instance-details-v2";

        // Session APIs (DCV Connection) - DEV
        public const string SessionStartUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/start-session";
        public const string SessionValidateUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/validate-session";
        public const string SessionStopUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/stop-session";
        public const string SessionExtendUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/extend-session";

        // Schedule & Idle APIs
        public const string ScheduleUrl = "https://nt0ajtyyca.execute-api.us-east-1.amazonaws.com/prod/";
        public const string IdleUrl = "https://mga45cl1h6.execute-api.us-east-1.amazonaws.com/prod/";

        // Resize API (DEV)
        public const string ResizeUrl = "https://y2yvok8mk6.execute-api.us-east-1.amazonaws.com/dev/resize";
        public const string IncreaseVolumeUrl = "https://y2yvok8mk6.execute-api.us-east-1.amazonaws.com/dev/increase-volume";

        // Other APIs
        public const string ProfileUrl = "https://41xpzcumx4.execute-api.us-east-1.amazonaws.com/prod/";
        public const string BillingUrl = "https://558xjerom8.execute-api.us-east-1.amazonaws.com/prod/billing/";
        public const string SmartPCConfigUrl = "https://sx6x319uq1.execute-api.us-east-1.amazonaws.com/dev/config";
        public const string EstimationUrl = "https://zxxx3xjbb0.execute-api.us-east-1.amazonaws.com/calculate-cost";
        public const string ClientSessionUrl = "https://sytnm9j4t4.execute-api.us-east-1.amazonaws.com/prod/";

        // Assignment APIs
        public const string AssignUrl = "https://5csw17pibi.execute-api.us-east-1.amazonaws.com/prod/assign";
        public const string UsersUrl = "https://v0605yjfrf.execute-api.us-east-1.amazonaws.com/prod/users";

        // Support APIs (DEV)
        public const string SupportUrl = "https://lvir6hp7hb.execute-api.us-east-1.amazonaws.com/dev/";

        // Storage APIs (Sense Cloud) - DEV
        public const string StorageBaseUrl = "https://bijv5mqt5l.execute-api.us-east-1.amazonaws.com/dev";

        // Promo & Cashback API (DEV)
        public const string PromoUrl = "https://3kuf94ola1.execute-api.us-east-1.amazonaws.com/dev/promo";
    }
}
