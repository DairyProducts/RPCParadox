import gamehandlers.GameHandler;

/**
 * Main application for RPCParadox - Discord Rich Presence for Paradox games
 * 
 * @author DairyProducts
 * @version 1.1
 * @since 1.0
 */
public class App {
    
    private static GameDetector gameDetector;
    private static DiscordRPCManager discordRPC;
    private static SystemTrayManager systemTray;
    private static GameHandler currentGame = null;
    
    private static final int CHECK_INTERVAL = 5000; 
    private static final int CALLBACK_INTERVAL = 16;
    
    public static void main(String args[]) {
        System.out.println("=== RPCParadox - Discord Rich Presence for Paradox Games ===");
        System.out.println("Initializing...\n");

        gameDetector = new GameDetector();
        discordRPC = new DiscordRPCManager();
        systemTray = new SystemTrayManager();
        
        // Set up exit callback for system tray
        systemTray.setExitCallback(() -> {
            cleanup();
        });
        
        if (systemTray.initialize()) {
            systemTray.updateStatus("Waiting for game...");
        }
        
        System.out.println("Testing game detection...");
        testGameDetection();
        
        System.out.println("\nReady to detect games and update Discord Rich Presence...");
        
        // Add shutdown hook to clean up properly
        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            System.out.println("\nShutting down...");
            cleanup();
        }));
        
        System.out.println("\nStarting main loop...");
        System.out.println("Press Ctrl+C to exit\n");
        
        // Main loop
        long lastCheckTime = 0;
        
        while (true) {
            long currentTime = System.currentTimeMillis();
            
            // Check for running games periodically
            if (currentTime - lastCheckTime >= CHECK_INTERVAL) {
                checkForGames();
                lastCheckTime = currentTime;
            }
            
            // Run Discord callbacks
            discordRPC.runCallbacks();
            
            // Sleep to prevent CPU spinning
            try {
                Thread.sleep(CALLBACK_INTERVAL);
            } catch (InterruptedException e) {
                break;
            }
        }
    }
    
    /**
     * Test the game detection functionality
     */
    private static void testGameDetection() {
        System.out.println("Registered game handlers:");
        for (GameHandler handler : gameDetector.getHandlers()) {
            System.out.println("  - " + handler.getGameName() + " (" + handler.getProcessName() + ")");
        }
        
        System.out.println("\nChecking for running games...");
        GameHandler detectedGame = gameDetector.detectRunningGame();
        
        if (detectedGame != null) {
            System.out.println("✓ Detected: " + detectedGame.getGameName());
        } else {
            System.out.println("✗ No supported games currently running");
        }
    }
    
    /**
     * Check for running games and update Discord RPC accordingly
     */
    private static void checkForGames() {
        GameHandler detectedGame = gameDetector.detectRunningGame();
        
        if (detectedGame != currentGame) {
            if (detectedGame == null) {
                if (currentGame != null) {
                    System.out.println("Game closed: " + currentGame.getGameName());
                    discordRPC.clearActivity();
                    currentGame = null;
                    
                    if (systemTray != null) {
                        systemTray.updateStatus("Waiting for game...");
                    }
                }
            } else {
                System.out.println("Game detected: " + detectedGame.getGameName());
                currentGame = detectedGame;
                
                if (systemTray != null) {
                    systemTray.updateStatus("Playing " + detectedGame.getGameName());
                    systemTray.showNotification("RPCParadox", "Now tracking " + detectedGame.getGameName());
                }
            }
        }
        
        if (currentGame != null) {
            discordRPC.updateActivity(currentGame);
        }
    }
    
    /**
     * Clean up resources before exit
     */
    private static void cleanup() {
        System.out.println("Cleaning up resources...");
        
        if (discordRPC != null) {
            discordRPC.clearActivity();
            discordRPC.shutdown();
        }
        
        if (systemTray != null) {
            systemTray.remove();
        }
    }
}
