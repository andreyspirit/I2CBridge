using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts.Devices
{
    /// <summary>
    /// Defines the contract for a memory device that supports asynchronous read and write operations.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface represent memory devices accessible via I2C protocol,
    /// such as EEPROM or RAM modules. All I/O operations are asynchronous to prevent blocking.
    /// </remarks>
    public interface IMemoryDevice
    {
        /// <summary>
        /// Writes data asynchronously to the memory device.
        /// </summary>
        /// <param name="data">The byte array containing data to be written to the device.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the write operation fails.</exception>
        Task WriteAsync(byte[] data);

        /// <summary>
        /// Reads data asynchronously from the memory device.
        /// </summary>
        /// <param name="length">The number of bytes to read from the device.</param>
        /// <returns>A task that represents the asynchronous read operation. The result contains the byte array read from the device.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is less than or equal to zero.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the read operation fails.</exception>
        Task<byte[]> ReadAsync(int length);

        /// <summary>
        /// Gets the total storage capacity of the memory device in bytes.
        /// </summary>
        /// <value>The capacity of the memory device in bytes.</value>
        int Capacity { get; }
    }
}