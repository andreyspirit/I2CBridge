using System;

namespace I2CBridge.Framework.Contracts.Bridge;

/// <summary>
/// Exception thrown when an I2C bridge operation fails.
/// This exception is specific to I2C communication errors and protocol violations.
/// All I2C bridge implementations should throw this exception type for bridge-level errors.
/// </summary>
public class I2cBridgeException : Exception
{
    /// <summary>
    /// Gets the I2C slave address associated with the error, if applicable.
    /// </summary>
    public byte? SlaveAddress { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="I2cBridgeException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public I2cBridgeException(string message) : base(message)
    {
        SlaveAddress = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="I2cBridgeException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public I2cBridgeException(string message, Exception innerException) : base(message, innerException)
    {
        SlaveAddress = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="I2cBridgeException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="slaveAddress">The I2C slave address involved in the failed operation.</param>
    public I2cBridgeException(string message, byte slaveAddress) : base(message)
    {
        SlaveAddress = slaveAddress;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="I2cBridgeException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="slaveAddress">The I2C slave address involved in the failed operation.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public I2cBridgeException(string message, byte slaveAddress, Exception innerException) : base(message, innerException)
    {
        SlaveAddress = slaveAddress;
    }
}
