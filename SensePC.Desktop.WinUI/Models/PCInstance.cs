using System.Text.Json.Serialization;

namespace SensePC.Desktop.WinUI.Models
{
    /// <summary>
    /// Represents a user's cloud PC instance
    /// </summary>
    public class PCInstance
    {
        [JsonPropertyName("instanceId")]
        public string InstanceId { get; set; } = "";

        [JsonPropertyName("systemName")]
        public string SystemName { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("configId")]
        public string? ConfigId { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("instanceType")]
        public string? InstanceType { get; set; }

        [JsonPropertyName("dcvUrl")]
        public string? DcvUrl { get; set; }

        [JsonPropertyName("billingPlan")]
        public string? BillingPlan { get; set; }

        [JsonPropertyName("billingPlanDescription")]
        public string? BillingPlanDescription { get; set; }

        [JsonPropertyName("assignedUser")]
        public string? AssignedUser { get; set; }

        [JsonPropertyName("autoRenew")]
        public bool? AutoRenew { get; set; }

        [JsonPropertyName("publicIpAddress")]
        public string? PublicIpAddress { get; set; }

        [JsonPropertyName("privateIpAddress")]
        public string? PrivateIpAddress { get; set; }

        // Helper properties for UI
        public bool IsRunning => State?.ToLower() == "running";
        public bool IsStopped => State?.ToLower() == "stopped";
        public bool IsBusy => State?.ToLower() is "pending" or "starting" or "stopping" or "rebooting";
    }


    /// <summary>
    /// PC specifications
    /// </summary>
    public class PCSpecs
    {
        [JsonPropertyName("cpu")]
        public string? Cpu { get; set; }

        [JsonPropertyName("ram")]
        public string? Ram { get; set; }

        [JsonPropertyName("storage")]
        public string? Storage { get; set; }

        [JsonPropertyName("gpu")]
        public string? Gpu { get; set; }

        [JsonPropertyName("os")]
        public string? Os { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Cpu)) parts.Add(Cpu);
            if (!string.IsNullOrEmpty(Ram)) parts.Add(Ram);
            if (!string.IsNullOrEmpty(Gpu)) parts.Add(Gpu);
            return string.Join(" • ", parts);
        }
    }

    /// <summary>
    /// API response for FetchPCdata
    /// </summary>
    public class FetchPCResponse
    {
        [JsonPropertyName("instances")]
        public List<PCInstance>? Instances { get; set; }

        // Sometimes the response is a direct array
        public List<PCInstance> GetInstances()
        {
            return Instances ?? new List<PCInstance>();
        }
    }
}
