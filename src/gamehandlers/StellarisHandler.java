package gamehandlers;

import de.jcm.discordgamesdk.activity.Activity;
import util.StellarisDataExtractor;
import java.time.Instant;

/**
 * Handler for Stellaris
 * 
 * @author DairyProducts
 * @version 1.1
 * @since 1.0
 */
public class StellarisHandler implements GameHandler {

    private final Instant startTime;
    private final StellarisDataExtractor dataExtractor;
    private long lastUpdateCheck = 0;
    private static final long UPDATE_INTERVAL = 5000;

    public StellarisHandler() {
        this.startTime = Instant.now();
        this.dataExtractor = new StellarisDataExtractor();
        
        System.out.println("[StellarisHandler] Initialized, attempting to load initial data...");

        if (dataExtractor.checkForUpdates()) {
            System.out.println("[StellarisHandler] Successfully loaded initial save data");
        } else {
            System.out.println("[StellarisHandler] No save data available yet");
        }
    }

    @Override
    public String getProcessName() {
        return "stellaris.exe";
    }

    @Override
    public String getGameName() {
        return "Stellaris";
    }

    @Override
    public long getClientId() {
        return 1426478074278580318L;
    }

    @Override
    public String getLargeImageKey() {
        return "stellaris";
    }

    @Override
    public String getLargeImageText() {
        return "Stellaris";
    }

    @Override
    public void updateActivity(Activity activity) {
        long currentTime = System.currentTimeMillis();
        if (currentTime - lastUpdateCheck >= UPDATE_INTERVAL) {
            if (dataExtractor.checkForUpdates()) {
                System.out.println("[StellarisHandler] Updated Stellaris game data from save file");
            }
            lastUpdateCheck = currentTime;
        }
        
        String details = dataExtractor.getDetailsText();
        activity.setDetails(details);
        
        String state = dataExtractor.getStateText();
        if (state != null) {
            activity.setState(state);
        }
        
        activity.timestamps().setStart(startTime);
        activity.assets().setLargeImage(getLargeImageKey());
        activity.assets().setLargeText(getLargeImageText());
    }
}
