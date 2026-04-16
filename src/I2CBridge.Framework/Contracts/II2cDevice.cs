using System;
using System.Threading;
using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts
{
    /// <summary>
    /// Represents an I2C device connected to the bus
    /// </summary>
    public interface II2cDevice : IAsyncDisposable
    {
        /// <summary>
        /// Gets the device ID
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// Gets the I2C slave address
        /// </summary>
        byte SlaveAddress { get; }

        /// <summary>
        /// Initializes the device
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the device type name
        /// </summary>
        string DeviceType { get; }
    }
}