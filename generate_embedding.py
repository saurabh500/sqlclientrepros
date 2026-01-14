import sys
import json
from sentence_transformers import SentenceTransformer

# Load model
model = SentenceTransformer('all-MiniLM-L6-v2')

# Get search term from command line argument
if len(sys.argv) < 2:
    print(json.dumps({"error": "No search term provided"}))
    sys.exit(1)

search_term = sys.argv[1]

# Generate embedding
embedding = model.encode(search_term, show_progress_bar=False)

# Output as JSON
result = {
    "term": search_term,
    "embedding": embedding.tolist()
}

print(json.dumps(result))
