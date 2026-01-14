package com.demo;

import com.demo.model.FoodReview;
import com.demo.model.SearchEmbedding;
import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import com.microsoft.sqlserver.jdbc.SQLServerConnection;
import com.microsoft.sqlserver.jdbc.SQLServerPreparedStatement;
import microsoft.sql.Vector;
import microsoft.sql.Vector.VectorDimensionType;

import java.io.FileReader;
import java.lang.reflect.Type;
import java.sql.*;
import java.util.ArrayList;
import java.util.List;
import java.util.Scanner;

public class VectorSearch {
    private static final String CONNECTION_STRING = 
        "jdbc:sqlserver://localhost;databaseName=demo;user=sa;password=z+/3GyAx6zUerW0r;trustServerCertificate=true;";
    private static final String TABLE_NAME = "FoodReviewEmbeddings_Local";
    private static final String EMBEDDINGS_FILE = "../search_embeddings.json";

    public static void main(String[] args) {
        System.out.println("=============================================================");
        System.out.println("SQL Server Vector Search Demo - Interactive (Java)");
        System.out.println("=============================================================\n");

        // Load pre-generated search embeddings
        System.out.println("Loading pre-generated search embeddings...");
        List<SearchEmbedding> searchEmbeddings = loadSearchEmbeddings();
        
        if (searchEmbeddings == null || searchEmbeddings.isEmpty()) {
            System.out.println("❌ No search embeddings found!");
            return;
        }
        
        System.out.println("✓ Loaded " + searchEmbeddings.size() + " search terms with embeddings\n");

        // Connect to SQL Server
        try (Connection connection = DriverManager.getConnection(CONNECTION_STRING)) {
            System.out.println("✓ Connected to SQL Server\n");

            Scanner scanner = new Scanner(System.in);
            
            // Interactive search loop
            while (true) {
                System.out.println("=============================================================");
                System.out.print("Enter search term (or 'q' to quit): ");
                
                String input = scanner.nextLine().trim();
                
                if (input.isEmpty() || input.equalsIgnoreCase("q")) {
                    System.out.println("\n✓ Goodbye!");
                    break;
                }
                
                // Look up the search term in pre-generated embeddings (case-insensitive)
                SearchEmbedding searchItem = null;
                for (SearchEmbedding embedding : searchEmbeddings) {
                    if (embedding.getTerm().trim().equalsIgnoreCase(input)) {
                        searchItem = embedding;
                        break;
                    }
                }
                
                if (searchItem == null) {
                    System.out.println("\n❌ Search term '" + input + "' not found in pre-generated embeddings.");
                    System.out.println("\nAvailable terms:");
                    for (SearchEmbedding embedding : searchEmbeddings) {
                        System.out.println("  - " + embedding.getTerm());
                    }
                    System.out.println();
                    continue;
                }
                
                System.out.println("\n" + "=".repeat(60));
                System.out.println("Searching for: '" + searchItem.getTerm() + "'");
                System.out.println("=".repeat(60) + "\n");
                
                // Perform vector search
                List<FoodReview> results = searchVectors(connection, searchItem);
                
                // Display results
                if (!results.isEmpty()) {
                    System.out.println("Top " + results.size() + " similar reviews:\n");
                    for (int i = 0; i < results.size(); i++) {
                        FoodReview result = results.get(i);
                        System.out.printf("%d. ID: %d | Score: %d/5 | Distance: %.4f%n",
                            i + 1, result.getId(), result.getScore(), result.getDistance());
                        System.out.println("   Product: " + result.getProductId());
                        System.out.println("   Summary: " + result.getSummary());
                        String text = result.getReviewText();
                        String displayText = text.length() > 100 ? text.substring(0, 100) + "..." : text;
                        System.out.println("   Text: " + displayText);
                        System.out.println();
                    }
                } else {
                    System.out.println("No results found.\n");
                }
                
                System.out.println("=".repeat(60));
                System.out.println("\nPress Enter to search again...");
                scanner.nextLine();
                System.out.println();
            }
            
        } catch (SQLException e) {
            System.err.println("Database error: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private static List<SearchEmbedding> loadSearchEmbeddings() {
        try (FileReader reader = new FileReader(EMBEDDINGS_FILE)) {
            Gson gson = new Gson();
            Type listType = new TypeToken<List<SearchEmbedding>>(){}.getType();
            return gson.fromJson(reader, listType);
        } catch (Exception e) {
            System.err.println("Error loading embeddings: " + e.getMessage());
            e.printStackTrace();
            return null;
        }
    }

    private static List<FoodReview> searchVectors(Connection connection, SearchEmbedding searchItem) throws SQLException {
        List<FoodReview> results = new ArrayList<>();
        
        String query = String.format(
            "SELECT TOP 5 " +
            "    Id, " +
            "    ProductId, " +
            "    Score, " +
            "    Summary, " +
            "    ReviewText, " +
            "    CombinedText, " +
            "    VECTOR_DISTANCE('cosine', Embedding, ?) AS Distance " +
            "FROM %s " +
            "ORDER BY Distance ASC",
            TABLE_NAME
        );
        
        try (PreparedStatement pstmt = connection.prepareStatement(query)) {
            // Create Vector from the embedding array
            float[] embeddingFloats = searchItem.getEmbeddingArray();
            Float[] embeddingArray = new Float[embeddingFloats.length];
            for (int i = 0; i < embeddingFloats.length; i++) {
                embeddingArray[i] = embeddingFloats[i];
            }
            Vector vector = new Vector(384, VectorDimensionType.FLOAT32, embeddingArray);
            
            // Set the vector parameter using the new JDBC vector type
            pstmt.setObject(1, vector, microsoft.sql.Types.VECTOR);
            
            try (ResultSet rs = pstmt.executeQuery()) {
                while (rs.next()) {
                    FoodReview review = new FoodReview(
                        rs.getInt("Id"),
                        rs.getString("ProductId"),
                        rs.getInt("Score"),
                        rs.getString("Summary"),
                        rs.getString("ReviewText"),
                        rs.getString("CombinedText"),
                        rs.getDouble("Distance")
                    );
                    results.add(review);
                }
            }
        }
        
        return results;
    }
}
