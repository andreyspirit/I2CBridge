using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts.Devices
{
    public interface IMemoryDevice
    {
        Task WriteAsync(byte[] data);
        Task<byte[]> ReadAsync(int length);
        int Capacity { get; }
    }
}