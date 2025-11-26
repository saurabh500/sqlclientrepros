# SQL Server Multi-Threaded Query Runner

This repository contains scripts to test multi-threaded SQL Server query execution using different Python database libraries.

## Overview

Two parallel query runner implementations are provided:

1. **`parallel_query_runner.py`** - Uses `mssql-python` library
2. **`parallel_query_runner_pyodbc.py`** - Uses `pyodbc` library

## Key Findings

**Threading Limitations:**
- `mssql-python`: ✅ Works with 1-2 threads, ❌ **HANGS with 3+ threads**
- `pyodbc`: ✅ Works reliably with 20+ threads

## Prerequisites

### For mssql-python version:
```bash
pip install mssql-python psutil
```

### For PyODBC version:
```bash
# Install system dependencies
sudo apt-get install unixodbc-dev

# Install Microsoft ODBC Driver 18 for SQL Server
curl https://packages.microsoft.com/keys/microsoft.asc | sudo apt-key add -
curl https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list
sudo apt-get update
sudo ACCEPT_EULA=Y apt-get install -y msodbcsql18

# Install Python packages
pip install pyodbc psutil
```

## Usage

### Running with mssql-python (max 2 threads)

```bash
python parallel_query_runner.py \
  -c "Server=10.0.14.177,1433;Database=master;UID=sa;PWD=TestPass;TrustServerCertificate=yes;" \
  -t 2 \
  -i 150 \
  -o ./test_output
```

**Options:**
- `-c, --connection-string`: SQL Server connection string
- `-t, --threads`: Number of parallel threads (default: 1, **max 2 for mssql-python**)
- `-i, --iterations`: Number of iterations per thread (default: -1 for infinite)
- `-q, --query`: SQL query to execute (default: simple SELECT)
- `-d, --delay`: Delay between iterations in seconds (default: 0.0)
- `-v, --verbose`: Enable verbose output
- `-o, --output-dir`: Output directory for CSV files (default: ./query_results)
- `--disable-pooling`: Disable connection pooling

### Running with PyODBC (supports 20+ threads)

```bash
python parallel_query_runner_pyodbc.py \
  -c "DRIVER={ODBC Driver 18 for SQL Server};SERVER=10.0.14.177,1433;DATABASE=master;UID=sa;PWD=TestPass;TrustServerCertificate=yes;" \
  -t 20 \
  -i 150 \
  -o ./test_pyodbc_output
```

**Options:**
- `-c, --connection-string`: PyODBC connection string (note the DRIVER= format)
- `-t, --threads`: Number of parallel threads (default: 1, **no upper limit**)
- `-i, --iterations`: Number of iterations per thread (default: -1 for infinite)
- `-q, --query`: SQL query to execute (default: simple SELECT)
- `-d, --delay`: Delay between iterations in seconds (default: 0.0)
- `-v, --verbose`: Enable verbose output
- `-o, --output-dir`: Output directory for CSV files (default: ./query_results_pyodbc)

## Resource Monitoring

Both scripts automatically emit resource usage metrics every 100 iterations to CSV files:

**Metrics tracked:**
- Timestamp
- Iteration number
- RSS memory (MB)
- VMS memory (MB)
- CPU percent
- Number of threads
- Number of file descriptors
- Query execution time (ms)

**Output:**
- One CSV file per thread: `thread_N_resources_YYYYMMDD_HHMMSS.csv`

## Example Results

### PyODBC with 20 threads:
```
Total Iterations:  3000
Total Time:        7.5s
Avg Throughput:    400 queries/sec
Total Errors:      0
Memory Usage:      ~30MB RSS
```

### mssql-python with 2 threads:
```
Total Iterations:  300
Total Time:        1.12s
Avg Throughput:    267 queries/sec
Total Errors:      0
Memory Usage:      ~25MB RSS
```

### mssql-python with 3+ threads:
```
❌ HANGS - threads deadlock and never complete
```

## Running Infinite Mode

To run continuously until stopped (useful for long-term testing):

```bash
# PyODBC - runs forever
python parallel_query_runner_pyodbc.py \
  -c "DRIVER={ODBC Driver 18 for SQL Server};SERVER=...;..." \
  -t 5 \
  -i -1

# Stop with Ctrl+C
```

## Test Results Summary

| Scenario | Library | Threads | Result |
|----------|---------|---------|--------|
| Light load | mssql-python | 1 | ✅ Works |
| Light load | mssql-python | 2 | ✅ Works |
| **Medium load** | **mssql-python** | **3** | **❌ HANGS** |
| Medium load | mssql-python | 5 | ❌ HANGS |
| Light load | PyODBC | 1 | ✅ Works |
| Light load | PyODBC | 2 | ✅ Works |
| Medium load | PyODBC | 3 | ✅ Works |
| Medium load | PyODBC | 5 | ✅ Works |
| **Heavy load** | **PyODBC** | **20** | **✅ Works** |

## Recommendation

**For multi-threaded workloads (3+ threads):** Use `parallel_query_runner_pyodbc.py` with PyODBC.

**For single/dual-threaded workloads:** Either library works fine.

## Connection String Formats

### mssql-python format:
```
Server=hostname,port;Database=dbname;UID=username;PWD=password;TrustServerCertificate=yes;
```

### PyODBC format:
```
DRIVER={ODBC Driver 18 for SQL Server};SERVER=hostname,port;DATABASE=dbname;UID=username;PWD=password;TrustServerCertificate=yes;
```

## Troubleshooting

### "ODBC Driver 18 not found"
Run the prerequisite installation steps for PyODBC listed above.

### "Connection hangs with mssql-python and 3+ threads"
This is a known limitation. Use PyODBC instead or run multiple separate processes with 1-2 threads each.

### "Permission denied" when running scripts
Make scripts executable:
```bash
chmod +x parallel_query_runner.py parallel_query_runner_pyodbc.py
```
