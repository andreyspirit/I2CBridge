using System.Threading;
using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts.Devices
{
    /// <summary>
    /// Represents a GPIO expander device
    /// </summary>
    public interface IGpioExpander
    {
        /// <summary>
        /// Sets a GPIO pin to high or low
        /// </summary>
        Task SetPinAsync(int pin, bool value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the state of a GPIO pin
        /// </summary>
        Task<bool> GetPinAsync(int pin, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets pin as input or output
        /// </summary>
        Task SetPinModeAsync(int pin, bool isInput, CancellationToken cancellationToken = default);
    }
}