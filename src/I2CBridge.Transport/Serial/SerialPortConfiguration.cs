using System;
using System.IO.Ports;

namespace I2CBridge.Transport.Serial
{
    /// <summary>
    /// Encapsulates serial port configuration parameters.
    /// Allows flexible configuration of baud rate, parity, data bits, stop bits, and timeouts.
    /// </summary>
    public class SerialPortConfiguration
    {
        /// <summary>
        /// Gets or sets the baud rate (bits per second).
        /// Common values: 9600, 19200, 38400, 57600, 115200.
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// Gets or sets the number of data bits (typically 8).
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// Gets or sets the stop bits configuration.
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>
        /// Gets or sets the parity check mode.
        /// </summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>
        /// Gets or sets the read timeout in milliseconds.
        /// Use Timeout.Infinite (-1) for no timeout.
        /// </summary>
        public int ReadTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the write timeout in milliseconds.
        /// Use Timeout.Infinite (-1) for no timeout.
        /// </summary>
        public int WriteTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the newline sequence used for SerialPort.ReadLine() and SerialPort.WriteLine(String) operations.
        /// Default is line feed ("\n").
        /// </summary>
        /// <remarks>
        /// This property determines what character sequence marks the end of a line when using
        /// SerialPort.ReadLine() and SerialPort.WriteLine(String) methods.
        /// Common values:
        /// - "\n" (LF) for Unix-style newlines (default)
        /// - "\r\n" (CRLF) for Windows-style newlines
        /// - "\r" (CR) for legacy Mac-style newlines
        /// </remarks>
        public string NewLine { get; set; } = "\n";

        /// <summary>
        /// Creates a default configuration suitable for most I2C bridge devices (SC18IM700, etc.).
        /// </summary>
        /// <returns>A SerialPortConfiguration with standard defaults.</returns>
        public static SerialPortConfiguration CreateDefault()
        {
            return new SerialPortConfiguration();
        }

        /// <summary>
        /// Creates a configuration for high-speed communication.
        /// </summary>
        /// <returns>A SerialPortConfiguration optimized for 115200 baud operation.</returns>
        public static SerialPortConfiguration CreateHighSpeed()
        {
            return new SerialPortConfiguration
            {
                BaudRate = 115200,
                ReadTimeoutMs = 2000,
                WriteTimeoutMs = 2000
            };
        }

        /// <summary>
        /// Creates a configuration for RS485 communication with Even parity.
        /// </summary>
        /// <returns>A SerialPortConfiguration optimized for RS485.</returns>
        public static SerialPortConfiguration CreateRs485()
        {
            return new SerialPortConfiguration
            {
                BaudRate = 19200,
                Parity = Parity.Even,
                ReadTimeoutMs = 3000,
                WriteTimeoutMs = 3000
            };
        }
    }
}