package com.demo.model;

public class FoodReview {
    private int id;
    private String productId;
    private int score;
    private String summary;
    private String reviewText;
    private String combinedText;
    private double distance;

    public FoodReview(int id, String productId, int score, String summary, 
                      String reviewText, String combinedText, double distance) {
        this.id = id;
        this.productId = productId;
        this.score = score;
        this.summary = summary;
        this.reviewText = reviewText;
        this.combinedText = combinedText;
        this.distance = distance;
    }

    public int getId() {
        return id;
    }

    public String getProductId() {
        return productId;
    }

    public int getScore() {
        return score;
    }

    public String getSummary() {
        return summary;
    }

    public String getReviewText() {
        return reviewText;
    }

    public String getCombinedText() {
        return combinedText;
    }

    public double getDistance() {
        return distance;
    }
}
