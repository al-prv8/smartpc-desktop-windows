namespace SensePC.Desktop.WinUI.Services
{
    /// <summary>
    /// API configuration with all endpoint URLs for SensePC services
    /// </summary>
    public static class ApiConfig
    {
        // Authentication
        public const string UserPoolId = "us-east-1_vgBCKmL0c";
        public const string UserPoolClientId = "2lknj90rkjmtkcnph06q6r93ug";
        public const string OAuthDomain = "auth.smartpc.cloud";

        // PC Management APIs
        public const string FetchPCUrl = "https://vj9idlbwf7.execute-api.us-east-1.amazonaws.com/prod/FetchPCdata";
        public const string VmManagementUrl = "https://lul5oxdwic.execute-api.us-east-1.amazonaws.com/dev/instance";
        public const string InstanceDetailsUrl = "https://4oacxj1xyk.execute-api.us-east-1.amazonaws.com/instance-details-v2";

        // Session APIs (DCV Connection)
        public const string SessionStartUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/start-session";
        public const string SessionValidateUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/validate-session";
        public const string SessionStopUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/stop-session";
        public const string SessionExtendUrl = "https://hxmwrrakc6.execute-api.us-east-1.amazonaws.com/dev/extend-session";

        // Schedule & Idle APIs
        public const string ScheduleUrl = "https://nt0ajtyyca.execute-api.us-east-1.amazonaws.com/prod/";
        public const string IdleUrl = "https://mga45cl1h6.execute-api.us-east-1.amazonaws.com/prod/";

        // Resize API
        public const string ResizeUrl = "https://18x8a6i43a.execute-api.us-east-1.amazonaws.com/prod/resize";
        public const string IncreaseVolumeUrl = "https://18x8a6i43a.execute-api.us-east-1.amazonaws.com/prod/increase-volume";

        // Other APIs
        public const string ProfileUrl = "https://41xpzcumx4.execute-api.us-east-1.amazonaws.com/prod/";
        public const string BillingUrl = "https://558xjerom8.execute-api.us-east-1.amazonaws.com/prod/billing/";
        public const string SmartPCConfigUrl = "https://sx6x319uq1.execute-api.us-east-1.amazonaws.com/dev/config";
        public const string EstimationUrl = "https://zxxx3xjbb0.execute-api.us-east-1.amazonaws.com/calculate-cost";
        public const string ClientSessionUrl = "https://sytnm9j4t4.execute-api.us-east-1.amazonaws.com/prod/";

        // Assignment APIs
        public const string AssignUrl = "https://3d5lcfx6b6.execute-api.us-east-1.amazonaws.com/prod/assign";
        public const string UsersUrl = "https://9efrpftkq6.execute-api.us-east-1.amazonaws.com/prod/users";

        // Support APIs
        public const string SupportUrl = "https://lvir6hp7hb.execute-api.us-east-1.amazonaws.com/dev/";
    }
}
