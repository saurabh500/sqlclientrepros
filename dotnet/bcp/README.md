# .NET Bulk Copy Packet Capture

This is a test program to capture SQL Server bulk copy packets from .NET SqlBulkCopy.

## Prerequisites

1. .NET 8.0 SDK installed
2. SQL Server running on localhost:1433
3. Wireshark or Microsoft Message Analyzer installed

## Running the Test

1. Open PowerShell or Command Prompt as Administrator
2. Navigate to this directory: `cd C:\work\dotnetbulk`
3. Build: `dotnet build`
4. Run: `dotnet run`

## Capturing Packets

### Option 1: Using Wireshark
1. Open Wireshark as Administrator
2. Start capture on loopback adapter (Adapter for loopback traffic capture)
3. Filter: `tcp.port == 1433`
4. Run `dotnet run` in another window
5. Stop capture
6. Save as .pcap file

### Option 2: Using netsh (built-in)
```powershell
# Start capture
netsh trace start capture=yes tracefile=C:\work\dotnetbulk\capture.etl

# Run the program
dotnet run

# Stop capture
netsh trace stop

# Convert to pcap using Microsoft Message Analyzer or etl2pcapng
```

### Option 3: Using tcpdump (if installed via npcap)
```powershell
# Install npcap from https://npcap.com/ first
tcpdump -i 1 -w capture.pcap port 1433
```

## Connection String

The program connects to:
- Server: localhost,1433
- Database: master
- User: sa
- Password: 5ZoGCK0DomqcMAXP
- **Encrypt: False** (for unencrypted packet capture)

## What to Look For

After capturing, look for:
1. SQL Batch packet with "INSERT BULK dbo.BulkCopyTest (...)"
2. Bulk Load packet (TDS type 0x07) with column metadata and row data
3. The exact structure of column descriptors in the bulk load packet

## Analysis

Use Wireshark's "Follow TCP Stream" or filter for `tds` protocol to see the TDS packets.
The bulk copy data should be in plaintext since Encrypt=False.

Compare the captured byte structure with the Rust implementation to identify differences.
