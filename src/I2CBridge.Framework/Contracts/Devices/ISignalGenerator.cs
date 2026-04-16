using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts.Devices
{
    public interface ISignalGenerator
    {
        Task SetFrequencyAsync(double frequency);
        Task<double> GetFrequencyAsync();
        Task SetOutputAsync(bool state);
    }
}