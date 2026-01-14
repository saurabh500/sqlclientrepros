package com.demo.model;

import java.util.List;

public class SearchEmbedding {
    private String term;
    private List<Float> embedding;

    public String getTerm() {
        return term;
    }

    public void setTerm(String term) {
        this.term = term;
    }

    public List<Float> getEmbedding() {
        return embedding;
    }

    public void setEmbedding(List<Float> embedding) {
        this.embedding = embedding;
    }

    public float[] getEmbeddingArray() {
        float[] array = new float[embedding.size()];
        for (int i = 0; i < embedding.size(); i++) {
            array[i] = embedding.get(i);
        }
        return array;
    }
}
