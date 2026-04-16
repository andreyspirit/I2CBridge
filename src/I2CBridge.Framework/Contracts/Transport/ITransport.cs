using System;
using System.Threading;
using System.Threading.Tasks;

namespace I2CBridge.Framework.Contracts.Transport
{
    public interface ITransport : IAsyncDisposable
    {
        string Name { get; }
        bool IsConnected { get; }

        Task OpenAsync(CancellationToken ct = default);
        Task CloseAsync(CancellationToken ct = default);

        /// <summary>
        /// Sends data using ReadOnlyMemory to prevent accidental data modification.
        /// </summary>
        ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

        /// <summary>
        /// Receives data into a pre-allocated memory buffer to reduce GC pressure.
        /// </summary>
        ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default);

        /// <summary>
        /// Flushes any pending data in the buffers (essential for UART/RS232).
        /// </summary>
        Task FlushAsync(CancellationToken ct = default);
    }
}