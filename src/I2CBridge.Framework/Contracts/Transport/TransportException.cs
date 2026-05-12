using System;

namespace I2CBridge.Framework.Contracts.Transport
{
    /// <summary>
    /// Base exception for transport layer failures.
    /// </summary>
    public class TransportException : Exception
    {
        /// <summary>
        /// Gets the error code identifying the type of transport failure.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets a value indicating whether this error is potentially recoverable.
        /// </summary>
        public bool IsRecoverable { get; }

        /// <summary>
        /// Initializes a new instance of the TransportException class.
        /// </summary>
        /// <param name="errorCode">A short error code identifying the error type.</param>
        /// <param name="message">A message that describes the error.</param>
        /// <param name="isRecoverable">Whether the error is potentially recoverable.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TransportException(
            string errorCode,
            string message,
            bool isRecoverable = true,
            Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            IsRecoverable = isRecoverable;
        }
    }

    /// <summary>
    /// Thrown when the transport is not connected.
    /// </summary>
    public class TransportNotConnectedException : TransportException
    {
        /// <summary>
        /// Initializes a new instance of the TransportNotConnectedException class.
        /// </summary>
        /// <param name="message">A message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TransportNotConnectedException(string message, Exception? innerException = null)
            : base("NOT_CONNECTED", message, isRecoverable: true, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when a transport operation times out.
    /// </summary>
    public class TransportTimeoutException : TransportException
    {
        /// <summary>
        /// Initializes a new instance of the TransportTimeoutException class.
        /// </summary>
        /// <param name="message">A message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TransportTimeoutException(string message, Exception? innerException = null)
            : base("TIMEOUT", message, isRecoverable: true, innerException)
        {
        }
    }
}