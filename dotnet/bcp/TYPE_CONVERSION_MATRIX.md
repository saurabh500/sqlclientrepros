# SqlClient Type Conversion Matrix

## Overview
This document maps the complete type conversion rules used by Microsoft.Data.SqlClient when converting .NET types to SQL Server types during SqlBulkCopy and parameterized queries.

## Conversion Architecture

### Flow
1. **SqlBulkCopy.ConvertValue()** - Initial conversion orchestrator
2. **SqlParameter.CoerceValue()** - Main conversion logic with special cases
3. **Convert.ChangeType()** - Fallback for standard .NET type conversions

### Special-Case Conversions (Before Convert.ChangeType)

The following conversions are handled BEFORE falling back to `Convert.ChangeType()`:

| Source Type | Target Type | Conversion Method | Notes |
|-------------|-------------|-------------------|-------|
| SqlXml | String | MetaType.GetStringFromXml() | Extracts XML content as string |
| XmlReader | String | MetaType.GetStringFromXml() | Reads XML and converts to string |
| SqlJson | String | Direct extraction | Gets JSON string value |
| char[] | String | new string(char[]) | Character array to string |
| SqlChars | String | Direct extraction | SqlChars to string |
| TextReader | TextDataFeed | Streaming | For large text data |
| String | Decimal (Currency) | decimal.Parse(NumberStyles.Currency) | Culture-aware currency parsing |
| SqlBytes | byte[] | No conversion | Passthrough |
| byte[] | SqlBytes | No conversion | Passthrough |
| String | TimeSpan (Time) | TimeSpan.Parse() | ISO 8601 time format |
| String | DateTimeOffset | DateTimeOffset.Parse() | ISO 8601 datetime with offset |
| DateTime | DateTimeOffset | new DateTimeOffset(DateTime) | Adds local offset |
| DateOnly (.NET 6+) | DateTime | DateTime(DateOnly, TimeOnly.MinValue) | Date only to datetime |
| TimeOnly (.NET 6+) | TimeSpan | TimeOnly.ToTimeSpan() | Time only to timespan |
| SqlVector\<float\> | byte[] | SqlVector payload | Vector binary representation |
| String (JSON) | float[] (Vector) | JSON deserialization | JSON array to vector |
| DataTable | No conversion | TVP passthrough | Table-valued parameter |
| DbDataReader | No conversion | TVP passthrough | Table-valued parameter |
| IEnumerable\<SqlDataRecord\> | No conversion | TVP passthrough | Table-valued parameter |
| Stream | byte[] | StreamDataFeed | Streaming binary data |

## .NET Type to SqlDbType Mapping

### Numeric Types

| .NET Type | SqlDbType | SQL Server Type | Range | Fixed Length | Notes |
|-----------|-----------|-----------------|-------|--------------|-------|
| byte | TinyInt | TINYINT | 0 to 255 | 1 byte | Unsigned |
| short | SmallInt | SMALLINT | -32,768 to 32,767 | 2 bytes | Signed |
| int | Int | INT | -2,147,483,648 to 2,147,483,647 | 4 bytes | Signed |
| long | BigInt | BIGINT | -9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 | 8 bytes | Signed |
| float | Real | REAL | -3.40E+38 to 3.40E+38 | 4 bytes | 7 digit precision |
| double | Float | FLOAT | -1.79E+308 to 1.79E+308 | 8 bytes | 15 digit precision |
| decimal | Decimal | DECIMAL(38,4) | ±10^38 -1 | 17 bytes | Max precision 38 |
| decimal (Currency) | Money | MONEY | -922,337,203,685,477.5808 to 922,337,203,685,477.5807 | 8 bytes | 4 decimal places |
| decimal (SmallMoney) | SmallMoney | SMALLMONEY | -214,748.3648 to 214,748.3647 | 4 bytes | 4 decimal places |
| bool | Bit | BIT | 0 or 1 | 1 byte | NOT convertible from string "true"/"false" |

### String Types

| .NET Type | SqlDbType | SQL Server Type | Max Length | Encoding | Notes |
|-----------|-----------|-----------------|------------|----------|-------|
| string | NVarChar | NVARCHAR(n) | 4000 chars | Unicode (UTF-16) | Default for string |
| string | VarChar | VARCHAR(n) | 8000 chars | ANSI | Specified explicitly |
| string | NChar | NCHAR(n) | 4000 chars | Unicode | Fixed length |
| string | Char | CHAR(n) | 8000 chars | ANSI | Fixed length |
| string | NText | NTEXT | 2^30-1 chars | Unicode | Deprecated, use NVarChar(MAX) |
| string | Text | TEXT | 2^31-1 chars | ANSI | Deprecated, use VarChar(MAX) |
| string | NVarChar(MAX) | NVARCHAR(MAX) | 2^30-1 chars | Unicode | PLP (Partially Length Prefixed) |
| string | VarChar(MAX) | VARCHAR(MAX) | 2^31-1 chars | ANSI | PLP |

### Binary Types

| .NET Type | SqlDbType | SQL Server Type | Max Length | Notes |
|-----------|-----------|-----------------|------------|-------|
| byte[] | VarBinary | VARBINARY(n) | 8000 bytes | Variable length |
| byte[] | Binary | BINARY(n) | 8000 bytes | Fixed length, padded |
| byte[] | Image | IMAGE | 2^31-1 bytes | Deprecated, use VarBinary(MAX) |
| byte[] | VarBinary(MAX) | VARBINARY(MAX) | 2^31-1 bytes | PLP |
| byte[] | Timestamp | TIMESTAMP/ROWVERSION | 8 bytes | Auto-generated, read-only |
| Stream | VarBinary(MAX) | VARBINARY(MAX) | 2^31-1 bytes | Streamed via DataFeed |

### Date/Time Types

| .NET Type | SqlDbType | SQL Server Type | Range | Precision | Fixed Length |
|-----------|-----------|-----------------|-------|-----------|--------------|
| DateTime | DateTime | DATETIME | 1753-01-01 to 9999-12-31 | 3.33ms | 8 bytes |
| DateTime | SmallDateTime | SMALLDATETIME | 1900-01-01 to 2079-06-06 | 1 minute | 4 bytes |
| DateTime | DateTime2 | DATETIME2(7) | 0001-01-01 to 9999-12-31 | 100ns | 6-8 bytes |
| DateTime | Date | DATE | 0001-01-01 to 9999-12-31 | 1 day | 3 bytes |
| DateOnly (.NET 6+) | Date | DATE | 0001-01-01 to 9999-12-31 | 1 day | 3 bytes |
| TimeSpan | Time | TIME(7) | 00:00:00.0000000 to 23:59:59.9999999 | 100ns | 3-5 bytes |
| TimeOnly (.NET 6+) | Time | TIME(7) | 00:00:00.0000000 to 23:59:59.9999999 | 100ns | 3-5 bytes |
| DateTimeOffset | DateTimeOffset | DATETIMEOFFSET(7) | 0001-01-01 to 9999-12-31 ±14:00 | 100ns | 8-10 bytes |

### Special Types

| .NET Type | SqlDbType | SQL Server Type | Notes |
|-----------|-----------|-----------------|-------|
| Guid | UniqueIdentifier | UNIQUEIDENTIFIER | 16 bytes, UUID/GUID |
| object | Variant | SQL_VARIANT | Can hold any SQL type except TEXT, NTEXT, IMAGE, TIMESTAMP, SQL_VARIANT |
| XmlReader | Xml | XML | PLP, Unicode |
| string (XML) | Xml | XML | Validated XML document |
| string (JSON) | Json | JSON (SQL 2016+) | PLP, UTF-8 encoded |
| DataTable | Structured | Table-Valued Parameter | TVP for bulk inserts |
| DbDataReader | Structured | Table-Valued Parameter | Streaming TVP |
| IEnumerable\<SqlDataRecord\> | Structured | Table-Valued Parameter | Custom TVP |
| SqlVector\<float\> | Vector | VECTOR | Binary representation of float array |
| UDT classes | Udt | User-Defined Type | Custom CLR types |

## String-to-Type Conversion Rules (SqlBulkCopy Flat File Scenario)

When sending string values (e.g., from flat files) to SQL Server via SqlBulkCopy:

### ✅ Supported Conversions (via Convert.ChangeType or Special Handling)

| Target Type | Example Values | Notes |
|-------------|----------------|-------|
| TINYINT | "0", "255" | Range: 0-255, throws on overflow |
| SMALLINT | "-32768", "32767" | Signed 16-bit |
| INT | "-2147483648", "2147483647" | Signed 32-bit |
| BIGINT | "-9223372036854775808", "9223372036854775807" | Signed 64-bit |
| REAL | "3.14", "1.23E+10" | Float parsing |
| FLOAT | "3.14159265359", "1.23E+308" | Double parsing |
| DECIMAL | "123.45", "1234567890.1234" | Decimal parsing |
| MONEY | "1234.56", "$1,234.56" | Currency parsing with NumberStyles.Currency |
| SMALLMONEY | "123.45" | Same as MONEY, smaller range |
| DATETIME | "2024-01-15 14:30:00" | DateTime parsing |
| SMALLDATETIME | "2024-01-15 14:30" | Rounded to minute |
| DATETIME2 | "2024-01-15 14:30:00.1234567" | High precision |
| DATE | "2024-01-15" | Date only |
| TIME | "14:30:00.1234567" | TimeSpan.Parse() |
| DATETIMEOFFSET | "2024-01-15 14:30:00.1234567 -08:00" | DateTimeOffset.Parse() |
| VARCHAR | "any string" | Direct assignment |
| NVARCHAR | "any string" | Direct assignment |
| CHAR | "abc" | Padded to fixed length |
| NCHAR | "abc" | Padded to fixed length |
| XML | "\<root\>content\</root\>" | Validated XML |
| JSON | "{\"key\": \"value\"}" | JSON validation (SQL 2016+) |

### ❌ NOT Supported Conversions

| Target Type | Issue | Workaround |
|-------------|-------|------------|
| BIT | String "true"/"false" or "0"/"1" not convertible | Send as bool or int, then convert |
| UNIQUEIDENTIFIER | String GUID not convertible (Guid not IConvertible) | Send as Guid object in DataTable |
| BINARY/VARBINARY | String cannot convert to byte[] | Send as byte[] or use Base64 encoding then convert |
| TIMESTAMP | Read-only, auto-generated | Cannot insert values |

## Overflow/Underflow Behavior

SqlBulkCopy validates ranges **client-side** before sending to SQL Server:

| Scenario | Behavior | Exception Type |
|----------|----------|----------------|
| Value > Max | Throws InvalidOperationException | Inner: OverflowException |
| Value < Min | Throws InvalidOperationException | Inner: OverflowException |
| Invalid format | Throws InvalidOperationException | Inner: FormatException |
| NULL string | Treated as DBNull.Value | No exception |

**Example**: Sending int value 256 to TINYINT column (max 255)
```
InvalidOperationException: The given value '256' of type Int32 from the data source cannot be converted to type tinyint for Column 1 [tiny_col] Row 3
  Inner: OverflowException: Failed to convert parameter value from a Int32 to a Byte
```

## Precision and Scale

### DECIMAL/NUMERIC
- **Precision**: Total number of digits (1-38)
- **Scale**: Digits after decimal point (0-precision)
- **Default**: DECIMAL(18, 0) for integer, DECIMAL(38, 4) for decimal values
- **Storage**: 5-17 bytes depending on precision

### TIME/DATETIME2/DATETIMEOFFSET
- **Scale**: Fractional seconds precision (0-7)
- **Default**: 7 (100 nanosecond precision)
- **Storage**: Varies by scale
  - TIME: 3-5 bytes
  - DATETIME2: 6-8 bytes
  - DATETIMEOFFSET: 8-10 bytes

## Code Locations

- **MetaType class**: `/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlEnums.cs` (lines 24-1197)
- **SqlParameter.CoerceValue()**: `/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlParameter.cs` (lines 2278-2450)
- **SqlBulkCopy.ConvertValue()**: `/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlBulkCopy.cs` (lines 1507-1690)

## References

- TDS Protocol: TDS 7.4 and 8.0 supported
- SQL Server Versions: 2012+ for all types, 2016+ for JSON, 2022+ for VECTOR
- .NET Versions: .NET Framework 4.6.2+, .NET Core 2.1+, .NET 5+
