import mssql_python
import csv
from sentence_transformers import SentenceTransformer

CONNECTION_STRING = "Server=localhost;Database=demo;UID=sa;PWD={z+/3GyAx6zUerW0r};TrustServerCertificate=yes;"

class LocalEmbeddingIterator:
    """Iterator that generates embeddings locally and streams to bulk copy"""
    
    def __init__(self, csv_file, model_name='all-MiniLM-L6-v2', max_rows=None, batch_size=32):
        """
        Args:
            csv_file: Path to CSV file with text data
            model_name: Sentence transformer model to use
            max_rows: Optional limit on number of rows
            batch_size: Number of texts to encode at once (for efficiency)
        """
        self.csv_file = csv_file
        self.max_rows = max_rows
        self.batch_size = batch_size
        
        print(f"Loading embedding model: {model_name}...")
        self.model = SentenceTransformer(model_name)
        self.embedding_dim = self.model.get_sentence_embedding_dimension()
        print(f"✓ Model loaded! Embedding dimensions: {self.embedding_dim}")
        
    def __iter__(self):
        with open(self.csv_file, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            
            batch_texts = []
            batch_rows = []
            
            for i, row in enumerate(reader):
                if self.max_rows is not None and i >= self.max_rows:
                    break
                
                # Parse row data
                row_id = int(row.get('Id', 0))
                product_id = row.get('ProductId', '')
                score = int(row.get('Score', 0))
                summary = row.get('Summary', '')
                text = row.get('Text', '')
                combined = row.get('combined', '')
                
                # Collect texts for batch encoding
                batch_texts.append(combined)
                batch_rows.append((row_id, product_id, score, summary, text, combined))
                
                # When batch is full, encode and yield
                if len(batch_texts) >= self.batch_size:
                    embeddings = self.model.encode(batch_texts, show_progress_bar=False)
                    
                    for row_data, embedding in zip(batch_rows, embeddings):
                        row_id, product_id, score, summary, text, combined = row_data
                        embedding_list = embedding.tolist()  # Convert numpy array to list
                        yield (row_id, product_id, score, summary, text, combined, embedding_list)
                    
                    batch_texts = []
                    batch_rows = []
            
            # Process remaining items
            if batch_texts:
                embeddings = self.model.encode(batch_texts, show_progress_bar=False)
                
                for row_data, embedding in zip(batch_rows, embeddings):
                    row_id, product_id, score, summary, text, combined = row_data
                    embedding_list = embedding.tolist()
                    yield (row_id, product_id, score, summary, text, combined, embedding_list)


def main():
    # Configuration
    csv_file = "finefoodembeddings.csv"
    table_name = "FoodReviewEmbeddings_Local"
    model_name = 'all-MiniLM-L6-v2'  # 384 dimensions, fast
    # model_name = 'all-mpnet-base-v2'  # 768 dimensions, better quality
    max_rows = 1000  # Start with 1000 rows for testing
    
    print(f"{'='*60}")
    print(f"Local Embedding Generation & Bulk Copy Demo")
    print(f"{'='*60}\n")
    print(f"Model: {model_name}")
    print(f"Max rows: {max_rows if max_rows else 'all'}")
    
    # Initialize the iterator (this will load the model)
    embedding_iter = LocalEmbeddingIterator(
        csv_file, 
        model_name=model_name,
        max_rows=max_rows,
        batch_size=32
    )
    embedding_dim = embedding_iter.embedding_dim
    
    # Connect to SQL Server
    print(f"\nConnecting to SQL Server...")
    conn = mssql_python.connect(CONNECTION_STRING)
    cursor = conn.cursor()
    
    # Drop table if exists
    print(f"Creating table '{table_name}'...")
    drop_table_sql = f"""
    IF OBJECT_ID('{table_name}', 'U') IS NOT NULL
        DROP TABLE {table_name};
    """
    cursor.execute(drop_table_sql)
    
    # Create table with appropriate vector dimension
    create_table_sql = f"""
    CREATE TABLE {table_name} (
        Id INT PRIMARY KEY,
        ProductId NVARCHAR(50),
        Score INT,
        Summary NVARCHAR(500),
        ReviewText NVARCHAR(MAX),
        CombinedText NVARCHAR(MAX),
        Embedding VECTOR({embedding_dim})
    );
    """
    cursor.execute(create_table_sql)
    conn.commit()
    
    print(f"✓ Table '{table_name}' created successfully!")
    print(f"  Vector dimension: {embedding_dim}\n")
    
    # Test with first few rows
    print(f"Testing embedding generation with first 3 rows...")
    test_iter = LocalEmbeddingIterator(csv_file, model_name=model_name, max_rows=3, batch_size=3)
    
    for i, (row_id, product_id, score, summary, text, combined, embedding) in enumerate(test_iter):
        print(f"\nRow {i}:")
        print(f"  ID: {row_id}")
        print(f"  Product ID: {product_id}")
        print(f"  Score: {score}")
        print(f"  Summary: {summary[:50]}...")
        print(f"  Text length: {len(combined)} chars")
        print(f"  Embedding dimensions: {len(embedding)}")
        print(f"  Embedding sample: [{embedding[0]:.4f}, {embedding[1]:.4f}, {embedding[2]:.4f}, ...]")
    
    print(f"\n✓ Embedding generation working!\n")
    
    # Perform bulk copy
    print(f"{'='*60}")
    print(f"Starting bulk copy with locally-generated embeddings...")
    print(f"{'='*60}\n")
    
    # Create fresh iterator for bulk copy
    embedding_data = LocalEmbeddingIterator(
        csv_file,
        model_name=model_name,
        max_rows=max_rows,
        batch_size=32
    )
    
    try:
        result = cursor.bulk_copy(
            table_name,
            embedding_data
        )
        print(f"\n✓ Bulk copy completed!")
        print(f"Result: {result}")
        print(f"\nRows copied: {result['rows_copied']}")
        print(f"Elapsed time: {result['elapsed_time']:.2f} seconds")
        print(f"Throughput: {result['rows_per_second']:.2f} rows/second")
    except Exception as e:
        print(f"\n✗ Bulk copy failed!")
        print(f"Error type: {type(e).__name__}")
        print(f"Error message: {e}")
        import traceback
        traceback.print_exc()
    
    cursor.close()
    conn.close()


if __name__ == "__main__":
    main()
