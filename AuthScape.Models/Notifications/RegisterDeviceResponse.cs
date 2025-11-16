namespace AuthScape.Models.Notifications
{
    /// <summary>
    /// Response model for device registration
    /// </summary>
    public class RegisterDeviceResponse
    {
        /// <summary>
        /// Device registration ID
        /// </summary>
        public long DeviceId { get; set; }

        /// <summary>
        /// Whether the registration was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Whether this was a new registration or an update
        /// </summary>
        public bool IsNewDevice { get; set; }
    }
}
