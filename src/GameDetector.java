import gamehandlers.GameHandler;
import gamehandlers.HOI4Handler;
import gamehandlers.StellarisHandler;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.util.ArrayList;
import java.util.List;

/**
 * Detects running Paradox games on the system.
 * 
 * @author DairyProducts
 * @version 1.0
 * @since 1.0
 */
public class GameDetector {
    
    private List<GameHandler> handlers;
    
    public GameDetector() {
        handlers = new ArrayList<>();
        // Register all game handlers
        handlers.add(new StellarisHandler());
        handlers.add(new HOI4Handler());
    }
    
    /**
     * Check all running processes and detect if any supported game is running
     * @return The GameHandler for the detected game, or null if no game is running
     */
    public GameHandler detectRunningGame() {
        try {
            // Use tasklist command on Windows to get running processes
            ProcessBuilder processBuilder = new ProcessBuilder("tasklist.exe");
            Process process = processBuilder.start();
            BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));
            
            String line;
            while ((line = reader.readLine()) != null) {
                // Check each handler to see if its process is running
                for (GameHandler handler : handlers) {
                    if (line.toLowerCase().contains(handler.getProcessName().toLowerCase())) {
                        return handler;
                    }
                }
            }
            reader.close();
        } catch (Exception e) {
            System.err.println("Error detecting running games: " + e.getMessage());
            e.printStackTrace();
        }
        
        return null;
    }
    
    /**
     * Get all registered game handlers
     * @return List of game handlers
     */
    public List<GameHandler> getHandlers() {
        return handlers;
    }
}
