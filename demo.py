import mssql_python
import csv
import json
from rich.progress import Progress, BarColumn, TextColumn, TimeElapsedColumn, TimeRemainingColumn

CONNECTION_STRING = "Server=localhost;Database=master;UID=sa;PWD={z+/3GyAx6zUerW0r};TrustServerCertificate=yes;"

class EmbeddingIterator:
    """Iterator that reads embeddings from CSV and converts to f32 lists"""
    
    def __init__(self, csv_file, max_rows=None, progress_bar=None, task_id=None):
        self.csv_file = csv_file
        self.max_rows = max_rows
        self.progress_bar = progress_bar
        self.task_id = task_id
        
    def __iter__(self):
        with open(self.csv_file, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            
            for i, row in enumerate(reader):
                if self.max_rows is not None and i >= self.max_rows:
                    break
                
                # Parse ID
                row_id = int(row.get('Id', 0))
                
                # Get text fields
                product_id = row.get('ProductId', '')
                score = int(row.get('Score', 0))
                summary = row.get('Summary', '')
                text = row.get('Text', '')
                combined = row.get('combined', '')
                
                # Parse embedding from JSON string and convert to list of floats (f32)
                embedding_str = row.get('vector', '[]')
                embedding = json.loads(embedding_str)
                # Ensure all values are float32
                embedding_f32 = [float(val) for val in embedding]
                
                # Update progress bar if provided
                if self.progress_bar and self.task_id is not None:
                    self.progress_bar.update(self.task_id, advance=1)
                
                # Yield tuple of (id, product_id, score, summary, text, combined, embedding_vector)
                yield (row_id, product_id, score, summary, text, combined, embedding_f32)


# Create table in SQL Server
csv_file = "finefoodembeddings.csv"
table_name = "FoodReviewEmbeddings"

print(f"Connecting to SQL Server...")
conn = mssql_python.connect(CONNECTION_STRING)
cursor = conn.cursor()

print(f"Creating table '{table_name}'...")


print(f"✓ Table '{table_name}' created successfully!\n")
print(f"Table schema:")
print(f"  - Id: INT PRIMARY KEY")
print(f"  - ProductId: NVARCHAR(50)")
print(f"  - Score: INT")
print(f"  - Summary: NVARCHAR(500)")
print(f"  - ReviewText: NVARCHAR(MAX)")
print(f"  - CombinedText: NVARCHAR(MAX)")
print(f"  - Embedding: VECTOR(1536)")

# Drop table if exists
drop_table_sql = f"""
IF OBJECT_ID('{table_name}', 'U') IS NOT NULL
    DROP TABLE {table_name};
"""
cursor.execute(drop_table_sql)

# Create table with vector support
create_table_sql = f"""
CREATE TABLE {table_name} (
    Id INT PRIMARY KEY,
    ProductId NVARCHAR(50),
    Score INT,
    Summary NVARCHAR(500),
    ReviewText NVARCHAR(MAX),
    CombinedText NVARCHAR(MAX),
    Embedding VECTOR(1536)
);
"""
cursor.execute(create_table_sql)
conn.commit()


csv_file = "finefoodembeddings.csv"

# Count total rows for progress bar
total_rows = sum(1 for _ in open(csv_file, 'r', encoding='utf-8')) - 1  # Subtract header

print(f"\nStarting bulk copy of {total_rows:,} rows...\n")

# Create progress bar
with Progress(
    TextColumn("[bold blue]{task.description}"),
    BarColumn(),
    TextColumn("[progress.percentage]{task.percentage:>3.0f}%"),
    TextColumn("•"),
    TextColumn("{task.completed}/{task.total} rows"),
    TextColumn("•"),
    TimeElapsedColumn(),
    TextColumn("•"),
    TimeRemainingColumn(),
) as progress:
    task = progress.add_task("[cyan]Copying data...", total=total_rows)
    
    # Create iterator with progress tracking
    embedding_data = EmbeddingIterator(csv_file, progress_bar=progress, task_id=task)
    
    try:
        conn = mssql_python.connect(CONNECTION_STRING)
        cursor = conn.cursor()
        result = cursor.bulk_copy(
            table_name,
            embedding_data
        )
        print("\n✓ Bulk copy completed!")
        print("Result:")
        print(json.dumps(result, indent=2))
    except Exception as e:
        print("\n✗ Bulk copy failed!")
        print(f"Error type: {type(e).__name__}")
        print(f"Error message: {e}")
        import traceback
        traceback.print_exc()

cursor.close()
conn.close()