#!/usr/bin/env python3
"""
Concurrent API Client to test FastAPI endpoints

This script makes concurrent HTTP requests to test threading behavior
of mssql-python vs PyODBC endpoints.
"""

import asyncio
import aiohttp
import time
from datetime import datetime
import argparse
import sys


async def make_request(session, url, request_id):
    """Make a single HTTP request"""
    start_time = time.time()
    
    try:
        async with session.get(url, timeout=aiohttp.ClientTimeout(total=30)) as response:
            data = await response.json()
            execution_time = time.time() - start_time
            
            return {
                "request_id": request_id,
                "status": "success",
                "status_code": response.status,
                "execution_time_ms": round(execution_time * 1000, 2),
                "response": data
            }
    except asyncio.TimeoutError:
        execution_time = time.time() - start_time
        return {
            "request_id": request_id,
            "status": "timeout",
            "execution_time_ms": round(execution_time * 1000, 2),
            "error": "Request timed out after 30 seconds"
        }
    except Exception as e:
        execution_time = time.time() - start_time
        return {
            "request_id": request_id,
            "status": "error",
            "execution_time_ms": round(execution_time * 1000, 2),
            "error": str(e)
        }


async def test_endpoint(url, num_concurrent, num_iterations):
    """Test an endpoint with concurrent requests"""
    print(f"\n{'='*80}")
    print(f"Testing: {url}")
    print(f"Concurrent requests: {num_concurrent}")
    print(f"Iterations: {num_iterations}")
    print(f"Total requests: {num_concurrent * num_iterations}")
    print(f"{'='*80}\n")
    
    start_time = time.time()
    all_results = []
    
    async with aiohttp.ClientSession() as session:
        for iteration in range(num_iterations):
            print(f"[Iteration {iteration + 1}/{num_iterations}] Starting {num_concurrent} concurrent requests...")
            
            # Create concurrent tasks
            tasks = []
            for i in range(num_concurrent):
                request_id = iteration * num_concurrent + i + 1
                tasks.append(make_request(session, url, request_id))
            
            # Execute all requests concurrently
            iteration_start = time.time()
            results = await asyncio.gather(*tasks)
            iteration_time = time.time() - iteration_start
            
            all_results.extend(results)
            
            # Print iteration summary
            successes = sum(1 for r in results if r["status"] == "success")
            errors = sum(1 for r in results if r["status"] == "error")
            timeouts = sum(1 for r in results if r["status"] == "timeout")
            
            print(f"  ✓ Completed in {iteration_time:.2f}s - Success: {successes}, Errors: {errors}, Timeouts: {timeouts}")
            
            # Small delay between iterations
            if iteration < num_iterations - 1:
                await asyncio.sleep(0.1)
    
    total_time = time.time() - start_time
    
    # Print summary
    print(f"\n{'-'*80}")
    print(f"Summary for {url}")
    print(f"{'-'*80}")
    
    total_requests = len(all_results)
    successful = sum(1 for r in all_results if r["status"] == "success")
    failed = sum(1 for r in all_results if r["status"] == "error")
    timed_out = sum(1 for r in all_results if r["status"] == "timeout")
    
    print(f"Total Requests:    {total_requests}")
    print(f"Successful:        {successful}")
    print(f"Failed:            {failed}")
    print(f"Timed Out:         {timed_out}")
    print(f"Total Time:        {total_time:.2f}s")
    print(f"Avg Throughput:    {total_requests / total_time:.2f} req/sec")
    
    if successful > 0:
        avg_time = sum(r["execution_time_ms"] for r in all_results if r["status"] == "success") / successful
        min_time = min(r["execution_time_ms"] for r in all_results if r["status"] == "success")
        max_time = max(r["execution_time_ms"] for r in all_results if r["status"] == "success")
        print(f"Avg Response Time: {avg_time:.2f}ms")
        print(f"Min Response Time: {min_time:.2f}ms")
        print(f"Max Response Time: {max_time:.2f}ms")
    
    print(f"{'='*80}\n")
    
    return all_results


async def main():
    parser = argparse.ArgumentParser(description='Test FastAPI endpoints with concurrent requests')
    
    parser.add_argument(
        '--host',
        type=str,
        default='localhost',
        help='FastAPI server host (default: localhost)'
    )
    
    parser.add_argument(
        '--port',
        type=int,
        default=8000,
        help='FastAPI server port (default: 8000)'
    )
    
    parser.add_argument(
        '--concurrent',
        type=int,
        default=10,
        help='Number of concurrent requests per iteration (default: 10)'
    )
    
    parser.add_argument(
        '--iterations',
        type=int,
        default=5,
        help='Number of iterations (default: 5)'
    )
    
    parser.add_argument(
        '--endpoint',
        type=str,
        choices=['mssql-python', 'pyodbc', 'both'],
        default='both',
        help='Which endpoint to test (default: both)'
    )
    
    args = parser.parse_args()
    
    base_url = f"http://{args.host}:{args.port}"
    
    print(f"\n{'='*80}")
    print(f"FastAPI Concurrent Request Tester")
    print(f"{'='*80}")
    print(f"Server: {base_url}")
    print(f"Concurrent requests per iteration: {args.concurrent}")
    print(f"Iterations: {args.iterations}")
    print(f"Total requests per endpoint: {args.concurrent * args.iterations}")
    print(f"{'='*80}\n")
    
    # Test health endpoint first
    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{base_url}/health", timeout=aiohttp.ClientTimeout(total=5)) as response:
                if response.status == 200:
                    print("✓ Server is healthy\n")
                else:
                    print(f"⚠ Server returned status {response.status}\n")
    except Exception as e:
        print(f"✗ Cannot connect to server: {e}")
        print(f"  Make sure the FastAPI server is running at {base_url}\n")
        return 1
    
    # Test endpoints
    if args.endpoint in ['mssql-python', 'both']:
        print("\n" + "="*80)
        print("TESTING MSSQL-PYTHON ENDPOINT")
        print("="*80)
        mssql_url = f"{base_url}/query/mssql-python"
        await test_endpoint(mssql_url, args.concurrent, args.iterations)
    
    if args.endpoint in ['pyodbc', 'both']:
        print("\n" + "="*80)
        print("TESTING PYODBC ENDPOINT")
        print("="*80)
        pyodbc_url = f"{base_url}/query/pyodbc"
        await test_endpoint(pyodbc_url, args.concurrent, args.iterations)
    
    print("\n✓ Testing complete!\n")
    return 0


if __name__ == '__main__':
    sys.exit(asyncio.run(main()))
