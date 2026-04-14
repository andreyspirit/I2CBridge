# I2CBridge - Modular I2C Hardware Abstraction Framework

A professional-grade, extensible framework for I2C device automation and testing built on .NET 8.

## 🎯 Features

- **Hardware Bridge Abstraction**: Swap SC18IM700 ↔ CH340 without code changes
- **Configuration-Driven**: JSON-based device setup
- **100% Async/Await**: Non-blocking I/O with ConfigureAwait(false)
- **Cross-Platform**: Windows, Linux, macOS (.NET 8)
- **SOLID Principles**: Dependency injection, interface-based design
- **Thread-Safe**: Transaction patterns for atomic I2C operations
- **Fully Testable**: Mockable interfaces for unit testing


## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK or later

### Build & Test
```bash
git clone https://github.com/andreyspirit/I2CBridge.git
cd I2CBridge
dotnet build
dotnet test
dotnet run --project src/I2CBridge.Examples.Console/I2CBridge.Examples.Console.csproj

