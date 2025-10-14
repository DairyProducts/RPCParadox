import de.jcm.discordgamesdk.Core;
import de.jcm.discordgamesdk.CreateParams;
import de.jcm.discordgamesdk.activity.Activity;
import gamehandlers.GameHandler;

/**
 * Manages Discord Rich Presence connection and updates.
 * 
 * @author DairyProducts
 * @version 1.0
 * @since 1.0
 */
public class DiscordRPCManager {
    
    private Core core;
    private Activity currentActivity;
    private boolean initialized = false;
    private long currentClientId = 0;
    
    /**
     * Initialize the Discord RPC connection with a specific client ID
     * @param clientId The Discord Application ID to use
     * @return true if initialization was successful, false otherwise
     */
    public boolean initialize(long clientId) {
        try {
            if (initialized && currentClientId != clientId) {
                shutdown();
            }
            
            if (initialized && currentClientId == clientId) {
                return true;
            }
            
            CreateParams params = new CreateParams();
            params.setClientID(clientId);
            params.setFlags(CreateParams.getDefaultFlags());
            
            core = new Core(params);
            initialized = true;
            currentClientId = clientId;
            
            System.out.println("Discord RPC initialized successfully with Client ID: " + clientId);
            return true;
        } catch (Exception e) {
            System.err.println("Failed to initialize Discord RPC: " + e.getMessage());
            e.printStackTrace();
            return false;
        }
    }
    
    /**
     * Update the Discord activity with game information
     * @param handler The game handler containing the activity information
     */
    @SuppressWarnings("deprecation")
    public void updateActivity(GameHandler handler) {
        if (!initialize(handler.getClientId())) {
            System.err.println("Failed to initialize Discord RPC for " + handler.getGameName());
            return;
        }
        
        try {
            // Close previous activity if it exists
            if (currentActivity != null) {
                currentActivity.close();
            }
            
            currentActivity = new Activity();
            handler.updateActivity(currentActivity);
            core.activityManager().updateActivity(currentActivity);
            
            System.out.println("Updated Discord RPC for: " + handler.getGameName());
        } catch (Exception e) {
            System.err.println("Failed to update Discord activity: " + e.getMessage());
            e.printStackTrace();
        }
    }
    
    /**
     * Clear the current Discord activity
     */
    @SuppressWarnings("deprecation")
    public void clearActivity() {
        if (!initialized) {
            return;
        }
        
        try {
            core.activityManager().clearActivity();
            if (currentActivity != null) {
                currentActivity.close();
                currentActivity = null;
            }
            System.out.println("Cleared Discord activity");
        } catch (Exception e) {
            System.err.println("Failed to clear Discord activity: " + e.getMessage());
            e.printStackTrace();
        }
    }
    
    /**
     * Run Discord callbacks - must be called regularly (in a loop)
     */
    public void runCallbacks() {
        if (initialized && core != null) {
            core.runCallbacks();
        }
    }
    
    /**
     * Shutdown the Discord RPC connection
     */
    @SuppressWarnings("deprecation")
    public void shutdown() {
        if (!initialized) {
            return;
        }
        
        try {
            if (currentActivity != null) {
                currentActivity.close();
            }
            if (core != null) {
                core.close();
            }
            initialized = false;
            currentClientId = 0;
            System.out.println("Discord RPC shut down");
        } catch (Exception e) {
            System.err.println("Error during shutdown: " + e.getMessage());
            e.printStackTrace();
        }
    }
    
    /**
     * Check if Discord RPC is initialized
     * @return true if initialized, false otherwise
     */
    public boolean isInitialized() {
        return initialized;
    }
}
