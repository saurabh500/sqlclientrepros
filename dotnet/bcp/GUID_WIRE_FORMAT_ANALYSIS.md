# .NET Bulk Copy GUID/UniqueIdentifier Wire Format Analysis

## Key Packet: Bulk Copy ROW Data

```
[TDS OUT] ObjectID=1, PacketNum=1, MsgType=0x07, Status=0x01, Length=121 bytes
[TDS OUT] Hex Data: 
07 01 00 79 00 00 01 00 81 02 00 00 00 00 00 08 00 24 10 02 69 00 64 00 00 00 00 00 08 00 38 07 63 00 6F 00 75 00 6E 00 74 00 65 00 72 00 
D1 10 00 84 0E 55 9B E2 D4 41 A7 16 44 66 55 44 00 00 01 00 00 00 
D1 10 10 B8 A7 6B AD 9D D1 11 80 B4 00 C0 4F D4 30 C8 02 00 00 00 
D1 10 11 B8 A7 6B AD 9D D1 11 80 B4 00 C0 4F D4 30 C8 03 00 00 00 
FD 00 00 00 00 00 00 00 00
```

## Breakdown:

### TDS Packet Header (8 bytes):
- `07` - Message Type: BULK_LOAD (0x07)
- `01` - Status: EOM (End of Message)
- `00 79` - Length: 121 bytes  
- `00 00 01 00` - Packet sequence and window

### COLMETADATA Token (0x81) - Column Definitions:
```
81                    - Token: SQLCOLMETADATA (0x81)
02 00                 - Column count: 2

Column 0 (id UNIQUEIDENTIFIER):
  00 00 00 00         - UserType: 0 (4 bytes)
  08 00               - Flags: 0x0008 (updatable, not nullable)
  24                  - TDS Type: 0x24 (GUIDTYPE)
  10                  - Length: 16 bytes  ← **KEY: Type info includes length!**
  02                  - Column name length: 2 chars
  69 00 64 00         - Column name: "id" (UTF-16LE)

Column 1 (counter INT):
  00 00 00 00         - UserType: 0 (4 bytes)
  08 00               - Flags: 0x0008 (updatable, not nullable)
  38                  - TDS Type: 0x38 (INT4)
  07                  - Column name length: 7 chars
  63 00 6F 00 75 00 6E 00 74 00 65 00 72 00  - Column name: "counter" (UTF-16LE)
```

### ROW Token (0xD1) - Row 1 Data:
```
D1                    - Token: SQLROW (0xD1)
10                    - ← **LENGTH PREFIX FOR GUID: 0x10 (16 bytes)**
00 84 0E 55 9B E2 D4 41 A7 16 44 66 55 44 00 00  - GUID bytes (little-endian)
                        ↑ This is 550e8400-e29b-41d4-a716-446655440000
01 00 00 00           - INT value: 1
```

### ROW Token (0xD1) - Row 2 Data:
```
D1                    - Token: SQLROW (0xD1)
10                    - ← **LENGTH PREFIX FOR GUID: 0x10 (16 bytes)**
10 B8 A7 6B AD 9D D1 11 80 B4 00 C0 4F D4 30 C8  - GUID bytes (little-endian)
                        ↑ This is 6ba7b810-9dad-11d1-80b4-00c04fd430c8
02 00 00 00           - INT value: 2
```

### ROW Token (0xD1) - Row 3 Data:
```
D1                    - Token: SQLROW (0xD1)
10                    - ← **LENGTH PREFIX FOR GUID: 0x10 (16 bytes)**
11 B8 A7 6B AD 9D D1 11 80 B4 00 C0 4F D4 30 C8  - GUID bytes (little-endian)
                        ↑ This is 6ba7b811-9dad-11d1-80b4-00c04fd430c8
03 00 00 00           - INT value: 3
```

### DONE Token (0xFD):
```
FD                    - Token: SQLDONE (0xFD)
00 00 00 00 00 00 00 00  - Status and row count
```

## CRITICAL FINDING:

**GUIDTYPE (0x24) in bulk copy ROW tokens DOES require a 1-byte length prefix!**

Even though the COLMETADATA specifies TDS type 0x24 with length 16, each ROW still contains:
- 1 byte: length (0x10 = 16)
- 16 bytes: GUID data in little-endian format

This is **DIFFERENT** from truly fixed-length types like INT4 (0x38), which do NOT have a length prefix in the ROW token.

The format is:
```
COLMETADATA:
  ... Type: 0x24, Length: 16 ...

ROW:
  0x10                                     ← 1-byte length prefix
  <16 bytes of GUID in little-endian>      ← actual GUID data
```

## Comparison with INT (fixed-length type):

INT4 (0x38) does NOT have type info in COLMETADATA and does NOT have length prefix in ROW:
```
COLMETADATA:
  Type: 0x38 (INT4)
  (no length byte)

ROW:
  01 00 00 00  ← 4 bytes directly, no length prefix
```

## Conclusion:

GUIDTYPE (0x24) is a **pseudo-fixed-length type** that:
1. Has a length field (16) in COLMETADATA type info
2. Requires a 1-byte length prefix (0x10) before the data in each ROW token
3. Is NOT truly "fixed-length" like INT4/FLOAT8/etc. which have no length prefix

Therefore, `TdsValueSerializer::serialize_uuid` MUST write:
1. A 1-byte length prefix (0x10 = 16)
2. Followed by 16 bytes of GUID data in little-endian format

The `is_fixed_type()` check should NOT include 0x24!
