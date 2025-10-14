package gamehandlers;

import de.jcm.discordgamesdk.activity.Activity;

/**
 * Interface for handling game-specific Discord Rich Presence updates
 * 
 * @author DairyProducts
 * @version 1.0
 * @since 1.0
 */
public interface GameHandler {
    
    /**
     * Get the process name of the game to detect
     * @return The process name (e.g., "stellaris.exe")
     */
    String getProcessName();
    
    /**
     * Get the display name of the game
     * @return The game's display name (e.g., "Stellaris")
     */
    String getGameName();
    
    /**
     * Get the Discord Application ID (Client ID) for this game
     * @return The Discord Application ID as a long
     */
    long getClientId();
    
    /**
     * Update the Discord activity with game-specific information
     * @param activity The Discord activity object to update
     */
    void updateActivity(Activity activity);
    
    /**
     * Get the large image key for Discord Rich Presence
     * @return The image key
     */
    String getLargeImageKey();
    
    /**
     * Get the large image text for Discord Rich Presence
     * @return The image tooltip text
     */
    String getLargeImageText();
}
