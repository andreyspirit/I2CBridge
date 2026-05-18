using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using I2CBridge.Devices;
using I2CBridge.Framework.Core;
using I2CBridge.Framework.Contracts;
using I2CBridge.Transport.Serial;
using I2CBridge.Bridges;

namespace I2CBridge.Examples.Console;

/// <summary>
/// Example demonstrating how to use the 24XX08 EEPROM device with I2CBridge.
/// Shows common operations: reading, writing, verification, and erasing.
/// </summary>
public class Eeprom24xx08Example
{
    private readonly ILogger<Eeprom24xx08Example> _logger;
    private readonly DeviceRegistry _deviceRegistry;
    private readonly I2cBridgeFactory _bridgeFactory;

    public Eeprom24xx08Example(ILogger<Eeprom24xx08Example> logger)
    {
        _logger = logger;
        _deviceRegistry = new DeviceRegistry();
        _bridgeFactory = new I2cBridgeFactory();
    }

    /// <summary>
    /// Runs the complete EEPROM example demonstrating all key operations.
    /// </summary>
    public async Task RunAsync()
    {
        try
        {
            _logger.LogInformation("=== 24XX08 EEPROM Example ===");
            _logger.LogInformation("Demonstrating read, write, verify, and erase operations");

            // 1. Setup: Create transport, bridge, and EEPROM device
            await SetupDevicesAsync();

            // 2. Initialize the EEPROM
            var eeprom = _deviceRegistry.GetDevice<Eeprom24xx08>("EEPROM_MAIN");
            await eeprom.InitializeAsync();

            // 3. Write single byte
            await WriteSingleByteExampleAsync(eeprom);

            // 4. Read single byte
            await ReadSingleByteExampleAsync(eeprom);

            // 5. Write multiple bytes (page write)
            await WriteMultipleByteExampleAsync(eeprom);

            // 6. Read multiple bytes (sequential read)
            await ReadMultipleBytesExampleAsync(eeprom);

            // 7. Verify written data
            await VerifyDataExampleAsync(eeprom);

            // 8. Erase all memory
            await EraseAllExampleAsync(eeprom);

            _logger.LogInformation("=== Example Completed Successfully ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Example failed with error");
            throw;
        }
    }

    private async Task SetupDevicesAsync()
    {
        _logger.LogInformation("Setting up I2C communication...");

        // Create serial transport (adjust COM port as needed)
        var serialConfig = SerialPortConfiguration.CreateDefault();
        var transport = new SerialTransport("COM1", serialConfig);

        // Create SC18IM700 I2C bridge
        var bridge = new Sc18im700Bridge(transport);
        _bridgeFactory.RegisterBridge("UART_I2C", bridge);
        _bridgeFactory.SetActiveBridge("UART_I2C");

        // Initialize the bridge
        await bridge.InitializeAsync();

        // Create EEPROM device
        var eeprom = new Eeprom24xx08(
            "EEPROM_MAIN",
            bridge,
            0x50);  // Base I2C address

        _deviceRegistry.Register(eeprom);

        _logger.LogInformation("Setup complete: I2C bridge and EEPROM ready");
    }

    private async Task WriteSingleByteExampleAsync(Eeprom24xx08 eeprom)
    {
        _logger.LogInformation("--- Single Byte Write Example ---");
        _logger.LogInformation("Writing 0xAB to address 0x000");

        await eeprom.WriteSingleByteAsync(0x000, 0xAB);

        _logger.LogInformation("SUCCESS: Single byte written");
    }

    private async Task ReadSingleByteExampleAsync(Eeprom24xx08 eeprom)
    {
        _logger.LogInformation("--- Single Byte Read Example ---");
        _logger.LogInformation("Reading from address 0x000");

        byte value = await eeprom.ReadSingleByteAsync(0x000);

        _logger.LogInformation("SUCCESS: Read value: 0x{value:X2}", value);
    }

    private async Task WriteMultipleByteExampleAsync(Eeprom24xx08 eeprom)
    {
        _logger.LogInformation("--- Multiple Byte Write Example (Page Write) ---");

        var testData = new byte[]
        {
            0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20,  // "Hello "
            0x57, 0x6F, 0x72, 0x6C, 0x64, 0x21   // "World!"
        };

        _logger.LogInformation("Writing 12 bytes to address 0x010: '{message}'", 
            System.Text.Encoding.ASCII.GetString(testData));

        await eeprom.WriteAsync(0x010, testData);

        _logger.LogInformation("SUCCESS: Multiple bytes written");
    }

    private async Task ReadMultipleBytesExampleAsync(Eeprom24xx08 eeprom)
    {
        _logger.LogInformation("--- Multiple Byte Read Example (Sequential Read) ---");
        _logger.LogInformation("Reading 12 bytes from address 0x010");

        byte[] data = await eeprom.ReadAsync(0x010, 12);
        string message = System.Text.Encoding.ASCII.GetString(data);

        _logger.LogInformation("SUCCESS: Read {length} bytes: '{message}'", data.Length, message);
    }

    private async Task VerifyDataExampleAsync(Eeprom24xx08 eeprom)
    {
        _logger.LogInformation("--- Data Verification Example ---");

        var expectedData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };

        bool isValid = await eeprom.VerifyAsync(0x010, expectedData);

        if (isValid)
        {
            _logger.LogInformation("SUCCESS: Data verification passed");
        }
        else
        {
            _logger.LogWarning("FAILED: Data verification failed");
        }
    }

    private async Task EraseAllExampleAsync(Eeprom24xx08 eeprom)
    {
        _logger.LogInformation("--- Erase All Example ---");
        _logger.LogWarning("WARNING: This will erase the entire EEPROM (1024 bytes)");
        _logger.LogInformation("Erasing EEPROM...");

        await eeprom.EraseAllAsync();

        _logger.LogInformation("SUCCESS: EEPROM erased completely");
    }
}
