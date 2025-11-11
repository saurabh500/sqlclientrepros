#!/usr/bin/env python3
"""
Parallel Query Runner (PyODBC version) - Execute SQL queries with multi-threading support

This script uses PyODBC instead of mssql-python to test threading behavior.

Usage:
    python parallel_query_runner_pyodbc.py --connection-string "..." --threads 4 --iterations 10
"""

import os
import sys
import time
import argparse
import threading
from datetime import datetime
from typing import Optional, List, Dict, Any
from collections import defaultdict
import csv

import pyodbc
import psutil


class QueryRunner:
    """Handles SQL query execution with threading support using PyODBC"""
    
    def __init__(self, connection_string: str, query: str, output_dir: str, verbose: bool = False):
        """
        Initialize the QueryRunner
        
        Args:
            connection_string: SQL Server connection string
            query: SQL query to execute
            output_dir: Directory to store CSV files with resource usage
            verbose: Enable verbose output
        """
        self.connection_string = connection_string
        self.query = query
        self.output_dir = output_dir
        self.verbose = verbose
        self.stats_lock = threading.Lock()
        self.stats = defaultdict(lambda: {
            'iterations': 0,
            'total_time': 0.0,
            'total_rows': 0,
            'errors': 0,
            'min_time': float('inf'),
            'max_time': 0.0
        })
        self.process = psutil.Process()
        self.cpu_lock = threading.Lock()
        
        # Create output directory
        os.makedirs(self.output_dir, exist_ok=True)
        
        # Generate timestamp for this run
        self.timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    
    def execute_single_query(self, thread_id: int, iteration: int) -> Dict[str, Any]:
        """
        Execute a single query cycle: connect -> query -> read results -> disconnect
        
        Args:
            thread_id: ID of the thread executing the query
            iteration: Iteration number
            
        Returns:
            Dictionary with execution statistics
        """
        start_time = time.time()
        result = {
            'thread_id': thread_id,
            'iteration': iteration,
            'success': False,
            'rows_read': 0,
            'execution_time': 0.0,
            'error': None
        }
        
        try:
            # Connect to database
            if self.verbose:
                print(f"[Thread-{thread_id}] Iteration {iteration}: Connecting...")
            
            conn = pyodbc.connect(self.connection_string)
            
            # Create cursor and execute query
            if self.verbose:
                print(f"[Thread-{thread_id}] Iteration {iteration}: Executing query...")
            
            cursor = conn.cursor()
            cursor.execute(self.query)
            
            # Read all results
            if self.verbose:
                print(f"[Thread-{thread_id}] Iteration {iteration}: Reading results...")
            
            rows_read = 0
            for row in cursor:
                rows_read += 1
                if self.verbose and rows_read % 1000 == 0:
                    print(f"[Thread-{thread_id}] Read {rows_read} rows...")
            
            # Close cursor and connection
            cursor.close()
            conn.close()
            
            result['success'] = True
            result['rows_read'] = rows_read
            
            if self.verbose:
                print(f"[Thread-{thread_id}] Iteration {iteration}: Completed "
                      f"({rows_read} rows in {time.time() - start_time:.3f}s)")
        
        except Exception as e:
            result['error'] = str(e)
            print(f"[Thread-{thread_id}] Iteration {iteration}: ERROR - {e}")
        
        finally:
            result['execution_time'] = time.time() - start_time
        
        return result
    
    def worker_thread(self, thread_id: int, iterations: int, delay: float):
        """
        Worker thread that executes multiple query iterations
        
        Args:
            thread_id: Unique identifier for this thread
            iterations: Number of query iterations to execute (-1 for infinite)
            delay: Delay between iterations (seconds)
        """
        if iterations < 0:
            print(f"[Thread-{thread_id}] Started - running infinitely (Ctrl+C to stop)")
        else:
            print(f"[Thread-{thread_id}] Started - will run {iterations} iterations")
        
        # Create CSV file for this thread
        csv_filename = os.path.join(
            self.output_dir, 
            f"thread_{thread_id}_resources_{self.timestamp}.csv"
        )
        
        with open(csv_filename, 'w', newline='') as csvfile:
            csv_writer = csv.writer(csvfile)
            # Write header
            csv_writer.writerow([
                'timestamp',
                'iteration',
                'rss_mb',
                'vms_mb',
                'cpu_percent',
                'num_threads',
                'num_fds',
                'execution_time_ms'
            ])
            csvfile.flush()
            
            i = 0
            while True:
                # Check if we should stop (for finite iterations)
                if iterations >= 0 and i >= iterations:
                    break
                
                # Execute query
                result = self.execute_single_query(thread_id, i + 1)
                
                # Update statistics
                with self.stats_lock:
                    stats = self.stats[thread_id]
                    stats['iterations'] += 1
                    stats['total_time'] += result['execution_time']
                    stats['total_rows'] += result['rows_read']
                    
                    if result['success']:
                        stats['min_time'] = min(stats['min_time'], result['execution_time'])
                        stats['max_time'] = max(stats['max_time'], result['execution_time'])
                    else:
                        stats['errors'] += 1
                
                i += 1
                
                # Emit resource usage every 100 iterations or on last iteration
                should_emit = False
                
                if i % 100 == 0:
                    should_emit = True
                elif iterations >= 0 and i >= iterations:
                    should_emit = True
                
                if should_emit:
                    try:
                        mem_info = self.process.memory_info()
                        
                        with self.cpu_lock:
                            cpu_percent = self.process.cpu_percent(interval=0.1)
                        
                        num_threads = self.process.num_threads()
                        num_fds = self.process.num_fds() if hasattr(self.process, 'num_fds') else 0
                        
                        # Write to CSV
                        csv_writer.writerow([
                            datetime.now().isoformat(),
                            i,
                            round(mem_info.rss / (1024 * 1024), 2),
                            round(mem_info.vms / (1024 * 1024), 2),
                            round(cpu_percent, 2),
                            num_threads,
                            num_fds,
                            round(result['execution_time'] * 1000, 2)
                        ])
                        csvfile.flush()
                        
                        if self.verbose:
                            print(f"[Thread-{thread_id}] Iteration {i}: "
                                  f"RSS={mem_info.rss / (1024 * 1024):.2f}MB, "
                                  f"CPU={cpu_percent:.1f}%, FDs={num_fds}")
                        
                    except Exception as e:
                        if self.verbose:
                            print(f"[Thread-{thread_id}] Error collecting metrics: {e}")
                
                # Delay between iterations
                if delay > 0:
                    time.sleep(delay)
        
        print(f"[Thread-{thread_id}] Completed all iterations")
        print(f"[Thread-{thread_id}] Resource data saved to: {csv_filename}")
    
    def run_parallel(self, num_threads: int, iterations_per_thread: int, delay: float = 0.0):
        """Run queries in parallel using multiple threads"""
        print("=" * 80)
        print(f"Parallel Query Runner (PyODBC)")
        print("=" * 80)
        print(f"Start Time:       {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"PID:              {os.getpid()}")
        print(f"Output Dir:       {self.output_dir}")
        print(f"Driver:           PyODBC")
        print(f"Threads:          {num_threads}")
        if iterations_per_thread < 0:
            print(f"Iterations/Thread: INFINITE (Ctrl+C to stop)")
            print(f"Total Iterations: INFINITE")
        else:
            print(f"Iterations/Thread: {iterations_per_thread}")
            print(f"Total Iterations: {num_threads * iterations_per_thread}")
        print(f"Delay:            {delay}s")
        print(f"Query:            {self.query[:100]}{'...' if len(self.query) > 100 else ''}")
        print("=" * 80)
        
        start_time = time.time()
        
        # Create and start threads
        threads: List[threading.Thread] = []
        for i in range(num_threads):
            thread = threading.Thread(
                target=self.worker_thread,
                args=(i + 1, iterations_per_thread, delay),
                name=f"QueryWorker-{i + 1}"
            )
            threads.append(thread)
            thread.start()
        
        # Wait for all threads to complete
        for thread in threads:
            thread.join()
        
        total_time = time.time() - start_time
        
        # Print statistics
        self.print_statistics(total_time)
    
    def print_statistics(self, total_time: float):
        """Print execution statistics"""
        print("\n" + "=" * 80)
        print("Execution Statistics")
        print("=" * 80)
        
        total_iterations = 0
        total_rows = 0
        total_errors = 0
        
        for thread_id in sorted(self.stats.keys()):
            stats = self.stats[thread_id]
            total_iterations += stats['iterations']
            total_rows += stats['total_rows']
            total_errors += stats['errors']
            
            avg_time = stats['total_time'] / stats['iterations'] if stats['iterations'] > 0 else 0
            
            print(f"\nThread-{thread_id}:")
            print(f"  Iterations:    {stats['iterations']}")
            print(f"  Rows Read:     {stats['total_rows']:,}")
            print(f"  Total Time:    {stats['total_time']:.3f}s")
            print(f"  Avg Time:      {avg_time:.3f}s")
            print(f"  Min Time:      {stats['min_time']:.3f}s")
            print(f"  Max Time:      {stats['max_time']:.3f}s")
            print(f"  Errors:        {stats['errors']}")
        
        print("\n" + "-" * 80)
        print("Overall Statistics:")
        print(f"  Total Time:        {total_time:.3f}s")
        print(f"  Total Iterations:  {total_iterations}")
        print(f"  Total Rows:        {total_rows:,}")
        print(f"  Total Errors:      {total_errors}")
        print(f"  Avg Throughput:    {total_iterations / total_time:.2f} queries/sec")
        print(f"  Avg Rows/sec:      {total_rows / total_time:.2f} rows/sec")
        print("=" * 80)


def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(
        description='Parallel SQL Query Runner (PyODBC) - Execute queries with multi-threading',
    )
    
    parser.add_argument(
        '-c', '--connection-string',
        type=str,
        required=True,
        help='SQL Server connection string for PyODBC (e.g., "DRIVER={ODBC Driver 18 for SQL Server};SERVER=...;...")'
    )
    
    parser.add_argument(
        '-t', '--threads',
        type=int,
        default=1,
        help='Number of parallel threads (default: 1)'
    )
    
    parser.add_argument(
        '-i', '--iterations',
        type=int,
        default=-1,
        help='Number of iterations per thread (default: -1 for infinite)'
    )
    
    parser.add_argument(
        '-q', '--query',
        type=str,
        default="SELECT 1 as num, 'test' as str, GETDATE() as dt",
        help='SQL query to execute'
    )
    
    parser.add_argument(
        '-d', '--delay',
        type=float,
        default=0.0,
        help='Delay between iterations in seconds (default: 0.0)'
    )
    
    parser.add_argument(
        '-v', '--verbose',
        action='store_true',
        help='Enable verbose output'
    )
    
    parser.add_argument(
        '-o', '--output-dir',
        type=str,
        default='./query_results_pyodbc',
        help='Output directory for CSV files (default: ./query_results_pyodbc)'
    )
    
    args = parser.parse_args()
    
    # Validate arguments
    if args.threads < 1:
        print("Error: Number of threads must be at least 1")
        return 1
    
    if args.iterations < -1 or args.iterations == 0:
        print("Error: Number of iterations must be -1 (infinite) or positive")
        return 1
    
    # Create runner and execute
    try:
        runner = QueryRunner(
            connection_string=args.connection_string,
            query=args.query,
            output_dir=args.output_dir,
            verbose=args.verbose
        )
        runner.run_parallel(args.threads, args.iterations, args.delay)
        return 0
    
    except KeyboardInterrupt:
        print("\n\nInterrupted by user")
        return 130
    
    except Exception as e:
        print(f"\nFatal error: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == '__main__':
    sys.exit(main())
