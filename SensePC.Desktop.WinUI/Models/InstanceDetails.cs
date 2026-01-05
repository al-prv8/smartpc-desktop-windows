using System.Text.Json.Serialization;

namespace SensePC.Desktop.WinUI.Models
{
    /// <summary>
    /// Real-time instance details including uptime, schedule, idle settings
    /// </summary>
    public class InstanceDetails
    {
        [JsonPropertyName("systemName")]
        public string SystemName { get; set; } = "";

        [JsonPropertyName("instanceId")]
        public string? InstanceId { get; set; }

        [JsonPropertyName("configId")]
        public string? ConfigId { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("cpuUsage")]
        public string? CpuUsage { get; set; }

        [JsonPropertyName("memoryUsage")]
        public string? MemoryUsage { get; set; }

        [JsonPropertyName("uptime")]
        public string? Uptime { get; set; }

        [JsonPropertyName("uptimeInfo")]
        public UptimeInfo? UptimeInfo { get; set; }

        [JsonPropertyName("idleTimeout")]
        public int? IdleTimeout { get; set; }

        [JsonPropertyName("schedule")]
        public ScheduleInfo? Schedule { get; set; }

        [JsonPropertyName("specs")]
        public PCSpecs? Specs { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("assignedUser")]
        public string? AssignedUser { get; set; }

        [JsonPropertyName("billingPlan")]
        public string? BillingPlan { get; set; }

        [JsonPropertyName("billingPlanDescription")]
        public string? BillingPlanDescription { get; set; }

        [JsonPropertyName("monthlyBillingTotal")]
        public decimal? MonthlyBillingTotal { get; set; }
    }

    /// <summary>
    /// Uptime information including billing plan limits
    /// </summary>
    public class UptimeInfo
    {
        [JsonPropertyName("maxUptimeHours")]
        public string? MaxUptimeHours { get; set; }

        [JsonPropertyName("currentUptimeHours")]
        public string? CurrentUptimeHours { get; set; }

        [JsonPropertyName("billingPlan")]
        public string? BillingPlan { get; set; }

        public string FormatUptime()
        {
            if (string.IsNullOrEmpty(CurrentUptimeHours) || string.IsNullOrEmpty(MaxUptimeHours))
                return "—";

            var current = FormatHours(CurrentUptimeHours);
            var max = FormatHours(MaxUptimeHours);
            return $"{current} / {max}";
        }

        private static string FormatHours(string hoursStr)
        {
            if (!double.TryParse(hoursStr, out var hours)) return hoursStr;
            if (hours < 1) return $"{(int)(hours * 60)}m";
            return $"{hours:F1}h";
        }
    }

    /// <summary>
    /// Auto start/stop schedule configuration
    /// </summary>
    public class ScheduleInfo
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("autoStartTime")]
        public string? AutoStartTime { get; set; }

        [JsonPropertyName("autoStopTime")]
        public string? AutoStopTime { get; set; }

        [JsonPropertyName("frequency")]
        public string? Frequency { get; set; } // everyday, weekdays, weekends, custom

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("timeZone")]
        public string? TimeZone { get; set; }

        public string FormatSchedule()
        {
            if (!Enabled) return "Disabled";
            
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(AutoStartTime)) parts.Add($"Start: {AutoStartTime}");
            if (!string.IsNullOrEmpty(AutoStopTime)) parts.Add($"Stop: {AutoStopTime}");
            
            return parts.Count > 0 ? string.Join(", ", parts) : "No schedule";
        }
    }

    /// <summary>
    /// DCV session launch response
    /// </summary>
    public class SessionLaunchResponse
    {
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }

        [JsonPropertyName("dcvUrl")]
        public string? DcvUrl { get; set; }

        [JsonPropertyName("gatewayUrl")]
        public string? GatewayUrl { get; set; }

        [JsonPropertyName("dnsName")]
        public string? DnsName { get; set; }

        [JsonPropertyName("authToken")]
        public string? AuthToken { get; set; }

        [JsonPropertyName("expiresAt")]
        public long? ExpiresAt { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; }
    }

    /// <summary>
    /// VM action response (start, stop, restart)
    /// </summary>
    public class VmActionResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("instanceId")]
        public string? InstanceId { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
    }
}
