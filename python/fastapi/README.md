# FastAPI SQL Server Threading Test

This FastAPI web service tests the threading behavior of `mssql-python` vs `PyODBC` libraries under concurrent load.

## Endpoints

- `GET /` - API information
- `GET /query/mssql-python` - Execute query using mssql-python (expected to hang with 3+ concurrent requests)
- `GET /query/pyodbc` - Execute query using PyODBC (should handle concurrent requests)
- `GET /health` - Health check

## Setup

1. **Install dependencies:**
```bash
cd python/fastapi
pip install -r requirements.txt
```

2. **Make sure mssql-python is available:**
```bash
# Add the mssql-python library to Python path or install it
cd ../../mssql-python
pip install -e .
```

## Running the Server

```bash
cd python/fastapi
python main.py
```

Or using uvicorn directly:
```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

The server will start on `http://localhost:8000`

## Testing Concurrent Requests

Use the provided test client to make concurrent requests:

### Test both endpoints with 10 concurrent requests, 5 iterations:
```bash
python test_client.py --concurrent 10 --iterations 5
```

### Test only mssql-python endpoint:
```bash
python test_client.py --endpoint mssql-python --concurrent 10 --iterations 5
```

### Test only PyODBC endpoint:
```bash
python test_client.py --endpoint pyodbc --concurrent 10 --iterations 5
```

### Custom server and concurrency:
```bash
python test_client.py --host localhost --port 8000 --concurrent 20 --iterations 3
```

## Expected Results

### mssql-python endpoint (`/query/mssql-python`):
- **1-2 concurrent requests:** ✅ Works fine
- **3+ concurrent requests:** ❌ Expected to hang or timeout
- Requests may start timing out after 30 seconds
- Server may become unresponsive

### PyODBC endpoint (`/query/pyodbc`):
- **10+ concurrent requests:** ✅ Should work fine
- All requests complete successfully
- Consistent response times
- Server remains responsive

## Test Client Options

```
--host HOST           FastAPI server host (default: localhost)
--port PORT           FastAPI server port (default: 8000)
--concurrent NUM      Number of concurrent requests per iteration (default: 10)
--iterations NUM      Number of iterations (default: 5)
--endpoint ENDPOINT   Which endpoint to test: mssql-python, pyodbc, or both (default: both)
```

## Manual Testing with curl

Test individual requests:

```bash
# Test mssql-python endpoint
curl http://localhost:8000/query/mssql-python

# Test PyODBC endpoint
curl http://localhost:8000/query/pyodbc

# Health check
curl http://localhost:8000/health
```

## Connection String Configuration

The connection strings are configured in `main.py`:

- **mssql-python:** `Server=10.0.14.177,1433;Database=master;UID=sa;PWD=TestPass;TrustServerCertificate=yes;`
- **PyODBC:** `DRIVER={ODBC Driver 18 for SQL Server};SERVER=10.0.14.177,1433;DATABASE=master;UID=sa;PWD=TestPass;TrustServerCertificate=yes;`

Modify these if you need to connect to a different server.

## Troubleshooting

### Server won't start
- Make sure port 8000 is not already in use
- Check that mssql-python library is importable: `python -c "import mssql_python"`
- Check that PyODBC is installed: `python -c "import pyodbc"`

### ODBC Driver not found
Install Microsoft ODBC Driver 18:
```bash
sudo ACCEPT_EULA=Y apt-get install -y msodbcsql18
```

### Requests timing out
- This is expected behavior for mssql-python with 3+ concurrent requests
- Try reducing `--concurrent` to 2 or test the PyODBC endpoint instead

### Server becomes unresponsive
- This confirms the mssql-python threading issue
- Restart the server: Ctrl+C and run `python main.py` again
- Test the PyODBC endpoint which should remain responsive
