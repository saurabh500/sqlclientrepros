#!/usr/bin/env python3
"""
FastAPI Web Service to test mssql-python vs PyODBC threading behavior

This service provides two GET endpoints:
- /query/mssql-python - Uses mssql-python library
- /query/pyodbc - Uses PyODBC library

Both endpoints execute a simple SELECT 1 query and return the result.
"""

from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse
import time
from datetime import datetime
import traceback

# Import database libraries
import mssql_python
import pyodbc

app = FastAPI(title="SQL Server Threading Test API")

# Connection string configuration
CONNECTION_STRING_MSSQL = "Server=10.0.14.177,1433;Database=master;UID=sa;PWD=TestPass;TrustServerCertificate=yes;"
CONNECTION_STRING_PYODBC = "DRIVER={ODBC Driver 18 for SQL Server};SERVER=10.0.14.177,1433;DATABASE=master;UID=sa;PWD=TestPass;TrustServerCertificate=yes;"

# Query to execute
QUERY = "SELECT 1 as num, 'test' as str, GETDATE() as dt"


@app.get("/")
async def root():
    """Root endpoint with API information"""
    return {
        "service": "SQL Server Threading Test API",
        "endpoints": {
            "mssql-python": "/query/mssql-python",
            "pyodbc": "/query/pyodbc"
        },
        "description": "Test concurrent database queries with different Python libraries"
    }


@app.get("/query/mssql-python")
async def query_mssql_python():
    """
    Execute query using mssql-python library
    Known issue: Hangs with 3+ concurrent requests
    """
    start_time = time.time()
    
    try:
        # Connect to database
        conn = mssql_python.connect(CONNECTION_STRING_MSSQL)
        
        # Create cursor and execute query
        cursor = conn.cursor()
        cursor.execute(QUERY)
        
        # Fetch results
        rows = []
        for row in cursor:
            rows.append({
                "num": row[0],
                "str": row[1],
                "dt": str(row[2])
            })
        
        # Close connection
        cursor.close()
        conn.close()
        
        execution_time = time.time() - start_time
        
        return {
            "library": "mssql-python",
            "status": "success",
            "rows": rows,
            "row_count": len(rows),
            "execution_time_ms": round(execution_time * 1000, 2),
            "timestamp": datetime.now().isoformat()
        }
        
    except Exception as e:
        execution_time = time.time() - start_time
        error_detail = {
            "library": "mssql-python",
            "status": "error",
            "error": str(e),
            "error_type": type(e).__name__,
            "traceback": traceback.format_exc(),
            "execution_time_ms": round(execution_time * 1000, 2),
            "timestamp": datetime.now().isoformat()
        }
        raise HTTPException(status_code=500, detail=error_detail)


@app.get("/query/pyodbc")
async def query_pyodbc():
    """
    Execute query using PyODBC library
    Should handle concurrent requests without issues
    """
    start_time = time.time()
    
    try:
        # Connect to database
        conn = pyodbc.connect(CONNECTION_STRING_PYODBC)
        
        # Create cursor and execute query
        cursor = conn.cursor()
        cursor.execute(QUERY)
        
        # Fetch results
        rows = []
        for row in cursor:
            rows.append({
                "num": row[0],
                "str": row[1],
                "dt": str(row[2])
            })
        
        # Close connection
        cursor.close()
        conn.close()
        
        execution_time = time.time() - start_time
        
        return {
            "library": "pyodbc",
            "status": "success",
            "rows": rows,
            "row_count": len(rows),
            "execution_time_ms": round(execution_time * 1000, 2),
            "timestamp": datetime.now().isoformat()
        }
        
    except Exception as e:
        execution_time = time.time() - start_time
        error_detail = {
            "library": "pyodbc",
            "status": "error",
            "error": str(e),
            "error_type": type(e).__name__,
            "traceback": traceback.format_exc(),
            "execution_time_ms": round(execution_time * 1000, 2),
            "timestamp": datetime.now().isoformat()
        }
        raise HTTPException(status_code=500, detail=error_detail)


@app.get("/health")
async def health():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.now().isoformat()
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
