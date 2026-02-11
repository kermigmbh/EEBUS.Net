using System;

namespace EEBUS.UseCases
{
    /// <summary>
    /// Indicates the direction of power flow for limit operations
    /// </summary>
    public enum PowerDirection
    {
        Consumption, // LPC - consume
        Production // LPP - produce
    }

    /// <summary>
    /// Result of a write approval request
    /// </summary>
    public class WriteApprovalResult
    {
        public bool Approved { get; private set; }
        public int ErrorCode { get; private set; } // 0 = OK, 7 = denied
        public string Description { get; private set; }

        private WriteApprovalResult(bool approved, int errorCode, string description)
        {
            Approved = approved;
            ErrorCode = errorCode;
            Description = description;
        }

        public static WriteApprovalResult Accept(string? description = null) => 
            new(true, 0, description ?? "Accepted");
        
        public static WriteApprovalResult Deny(string? description = null) => 
            new(false, 7, description ?? "Denied");
    }

    /// <summary>
    /// Base class for all write approval requests
    /// </summary>
    public abstract class WriteApprovalRequest
    {
        public PowerDirection Direction { get; protected set; }
        public string RemoteDeviceId { get; protected set; }
        public string RemoteSKI { get; protected set; }
    }

    /// <summary>
    /// Request for approval of active limit writes (LoadControlLimitListData)
    /// </summary>
    public class ActiveLimitWriteRequest : WriteApprovalRequest
    {
        public bool IsLimitActive { get; set; }
        public long Value { get; set; }
        public short Scale { get; set; }
        public TimeSpan? Duration { get; set; }

        public ActiveLimitWriteRequest(
            PowerDirection direction,
            bool isActive,
            long value,
            short scale,
            TimeSpan? duration,
            string remoteDeviceId,
            string remoteSKI)
        {
            Direction = direction;
            IsLimitActive = isActive;
            Value = value;
            Scale = scale;
            Duration = duration;
            RemoteDeviceId = remoteDeviceId;
            RemoteSKI = remoteSKI;
        }
    }

    /// <summary>
    /// Request for approval of failsafe limit writes (FailsafeConsumptionActivePowerLimit or FailsafeProductionActivePowerLimit)
    /// </summary>
    public class FailsafeLimitWriteRequest : WriteApprovalRequest
    {
        public long Value { get; set; }
        public short Scale { get; set; }

        public FailsafeLimitWriteRequest(
            PowerDirection direction,
            long value,
            short scale,
            string remoteDeviceId,
            string remoteSKI)
        {
            Direction = direction;
            Value = value;
            Scale = scale;
            RemoteDeviceId = remoteDeviceId;
            RemoteSKI = remoteSKI;
        }
    }

    /// <summary>
    /// Request for approval of failsafe duration writes
    /// </summary>
    public class FailsafeDurationWriteRequest : WriteApprovalRequest
    {
        public TimeSpan Duration { get; set; }

        public FailsafeDurationWriteRequest(
            TimeSpan duration,
            string remoteDeviceId,
            string remoteSKI)
        {
            // Failsafe duration is shared by LPC and LPP
            Duration = duration;
            RemoteDeviceId = remoteDeviceId;
            RemoteSKI = remoteSKI;
        }
    }
}