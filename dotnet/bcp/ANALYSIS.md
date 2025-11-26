# Bulk Copy Packet Analysis Guide

## What We're Looking For

The Rust implementation is getting "Invalid column type from bcp client for colid 1" error.
We need to compare the exact byte structure .NET sends vs what Rust sends.

## Expected Packet Sequence

1. **SQL Batch (Type 0x01)**: `INSERT BULK dbo.BulkCopyTest ([id] int, [name] nvarchar(100), [age] smallint, [active] bit)`

2. **Bulk Load Packet (Type 0x07)**:
   - COLMETADATA token (0x81)? Or no token?
   - Column count (2 bytes)
   - CEK table count (2 bytes, should be 0x0000)
   - For each column:
     * UserType (4 bytes)? Or skip?
     * Flags (2 bytes)? Or skip?
     * TDS type byte (1 byte): 0x38 for INT, 0xE7 for NVARCHAR, 0x34 for SMALLINT, 0x32 for BIT
     * Type-specific info (varies by type)
     * Column name (B_VARCHAR: 1 byte length + UTF-16LE string)
   - ROW tokens (0xD1) with data
   - DONE token (0xFD)

## Key Questions to Answer

1. **Does .NET send COLMETADATA token (0x81) in the bulk load packet?**
   - YES → We need it
   - NO → We should remove it

2. **Does .NET send UserType (4 bytes) and Flags (2 bytes) before each TDS type?**
   - YES → We need them
   - NO → We should remove them

3. **For fixed-length types (0x38 INT4, 0x34 INT2, 0x32 BIT), does .NET write a length byte after the type?**
   - YES → We need to write it
   - NO → We should NOT write it

4. **What exact bytes does .NET write for INT column (id)?**
   - Expected: `[UserType?] [Flags?] 0x38 [length?] [name length] [name in UTF-16LE]`
   - We need the exact sequence

5. **For NVARCHAR(100), what length does .NET write?**
   - 100 (characters)
   - 200 (bytes = 100 * 2)
   - Or something else?

## Wireshark Filters

```
tds                          # Show all TDS packets
tds.type == 7                # Show only bulk load packets
tds.type == 1                # Show only SQL batch packets
tcp.stream eq 0              # Follow first TCP connection
```

## Hex Dump Analysis

Look for these key bytes in the bulk load packet:
- `81 00 04 00 00 00` - COLMETADATA token + 4 columns + 0 CEK entries
- `38` - INT4 type for 'id' column
- `E7` - NVARCHAR type for 'name' column
- `34` - INT2 type for 'age' column
- `32` - BIT type for 'active' column
- `D1` - ROW token
- `FD` - DONE token

## Current Rust Implementation Issues

The Rust code currently writes:
```
0x81              # COLMETADATA token
0x04 0x00         # Column count: 4
0x00 0x00         # CEK count: 0
# For each column:
0x00 0x00 0x00 0x00  # UserType (removed in latest attempt)
0x00 0x00            # Flags (removed in latest attempt)
0x38              # TDS type (INT4 for id)
0x04              # Length byte (recently added)
[column name]     # UTF-16LE name
```

But this still fails. We need to see what .NET actually sends!
