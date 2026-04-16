using System;
using System.Threading;
using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts
{
    /// <summary>
    /// Represents an I2C bridge that converts I2C commands to transport protocol
    /// </summary>
    public interface II2cBridge : IAsyncDisposable
    {
        /// <summary>
        /// Gets the bridge name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Initializes the bridge
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes data to an I2C slave device
        /// </summary>
        Task WriteToSlaveAsync(byte slaveAddress, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads data from an I2C slave device
        /// </summary>
        Task<byte[]> ReadFromSlaveAsync(byte slaveAddress, byte length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a combined write-then-read operation
        /// </summary>
        Task<byte[]> WriteReadAsync(byte slaveAddress, byte[] writeData, byte readLength, CancellationToken cancellationToken = default);
    }
}