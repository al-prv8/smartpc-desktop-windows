namespace SensePC.Desktop.WinUI.Services
{
    /// <summary>
    /// API configuration with all endpoint URLs for SensePC services
    /// Centralized configuration - change environment here only
    /// Using PRODUCTION environment endpoints
    /// </summary>
    public static class ApiConfig
    {
        // ==================== WEBSITE DOMAIN ====================
        public const string WebsiteDomain = "sensepc.com";
        public const string WebsiteBaseUrl = "https://sensepc.com";
        public const string DashboardUrl = "https://sensepc.com/dashboard/sense-pc";
        public const string AuthSignUpUrl = "https://sensepc.com/auth/sign-up";
        public const string AuthUrl = "https://sensepc.com/auth";
        public const string AuthCallbackUrl = "https://sensepc.com/auth/callback";

        // ==================== AUTHENTICATION (PROD) ====================
        public const string UserPoolId = "us-east-1_bsNNXwYpF";
        public const string UserPoolClientId = "5uij8k43rv2ri4ri9nl6479f49";
        public const string OAuthDomain = "sensepc-prod.auth.us-east-1.amazoncognito.com";
        public const string OAuthTokenEndpoint = "https://sensepc-prod.auth.us-east-1.amazoncognito.com/oauth2/token";
        public const string RedirectUri = "https://sensepc.com/auth/callback";

        // ==================== PC MANAGEMENT APIs (PROD) ====================
        public const string FetchPCUrl = "https://8qxk92ck0b.execute-api.us-east-1.amazonaws.com/prod/FetchPCdata";
        public const string VmManagementUrl = "https://vc0vnkvmdd.execute-api.us-east-1.amazonaws.com/prod/instance";
        public const string InstanceDetailsUrl = "https://4nmqohxrh3.execute-api.us-east-1.amazonaws.com/prod/instance-details-v2";

        // ==================== SESSION APIs (DCV Connection) - PROD ====================
        public const string SessionStartUrl = "https://jcidkynezk.execute-api.us-east-1.amazonaws.com/prod/start-session";
        public const string SessionValidateUrl = "https://jcidkynezk.execute-api.us-east-1.amazonaws.com/prod/validate-session";
        public const string SessionStopUrl = "https://jcidkynezk.execute-api.us-east-1.amazonaws.com/prod/stop-session";
        public const string SessionExtendUrl = "https://jcidkynezk.execute-api.us-east-1.amazonaws.com/prod/extend-session";

        // ==================== SCHEDULE & IDLE APIs (PROD) ====================
        public const string ScheduleUrl = "https://vh1pcqo5ef.execute-api.us-east-1.amazonaws.com/prod/";
        public const string IdleUrl = "https://1wdcrd49u6.execute-api.us-east-1.amazonaws.com/prod/";

        // ==================== RESIZE API (PROD) ====================
        public const string ResizeUrl = "https://y2yvok8mk6.execute-api.us-east-1.amazonaws.com/prod/resize";
        public const string IncreaseVolumeUrl = "https://y2yvok8mk6.execute-api.us-east-1.amazonaws.com/prod/increase-volume";

        // ==================== OTHER APIs (PROD) ====================
        public const string ProfileUrl = "https://62ygmq07jh.execute-api.us-east-1.amazonaws.com/prod/";
        public const string BillingUrl = "https://2mrd88o4fk.execute-api.us-east-1.amazonaws.com/prod/billing/";
        public const string SmartPCConfigUrl = "https://wuenp8sly5.execute-api.us-east-1.amazonaws.com/prod/config";
        public const string EstimationUrl = "https://cumya4fq52.execute-api.us-east-1.amazonaws.com/prod/calculate-cost";
        public const string ClientSessionUrl = "https://3m9ioa5mx7.execute-api.us-east-1.amazonaws.com/prod/";

        // ==================== ASSIGNMENT APIs (PROD) ====================
        public const string AssignUrl = "https://bgxw74z9zl.execute-api.us-east-1.amazonaws.com/prod/assign";
        public const string UsersUrl = "https://yvtd9w7187.execute-api.us-east-1.amazonaws.com/prod/users";

        // ==================== SUPPORT APIs (PROD) ====================
        public const string SupportUrl = "https://j0fkyoq6z5.execute-api.us-east-1.amazonaws.com/prod/";

        // ==================== STORAGE APIs (Sense Cloud) - PROD ====================
        public const string StorageBaseUrl = "https://32zy885t37.execute-api.us-east-1.amazonaws.com/prod";
        public const string StoragePingUrl = "https://vfbb98chmd.execute-api.us-east-1.amazonaws.com/prod/";

        // ==================== PROMO & CASHBACK API ====================
        public const string PromoUrl = "https://3kuf94ola1.execute-api.us-east-1.amazonaws.com/prod/promo";

        // ==================== NOTIFICATION APIs (PROD) ====================
        public const string NotificationUrl = "https://orv95gmpji.execute-api.us-east-1.amazonaws.com/prod/";
        public const string WebSocketUrl = "wss://zgnku1flfk.execute-api.us-east-1.amazonaws.com/prod/";

        // ==================== MFA API (PROD) ====================
        public const string MfaUrl = "https://blfuxeemig.execute-api.us-east-1.amazonaws.com/prod/mfa-recovery";

        // ==================== TUTORIAL ASSETS ====================
        public const string TutorialGettingStartedImage = "https://sensepc.com/assets/images/gettingStartedWithSensePc.png";
        public const string TutorialOptimizingImage = "https://sensepc.com/assets/images/optimizing.jpg";
        public const string TutorialStorageImage = "https://sensepc.com/assets/images/storageManagement.png";
    }
}
