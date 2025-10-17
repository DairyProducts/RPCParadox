package gamehandlers;

import de.jcm.discordgamesdk.activity.Activity;
import util.HOI4DataExtractor;
import java.time.Instant;

/**
 * Handler for Hearts of Iron IV
 * 
 * @author DairyProducts
 * @version 1.1
 * @since 1.0
 */
public class HOI4Handler implements GameHandler {
    
    private final Instant startTime;
    private final HOI4DataExtractor dataExtractor;
    private long lastUpdateCheck = 0;
    private static final long UPDATE_INTERVAL = 5000;
    
    public HOI4Handler() {
        this.startTime = Instant.now();
        this.dataExtractor = new HOI4DataExtractor();
        
        System.out.println("[HOI4Handler] Initialized, attempting to load initial data...");

        if (dataExtractor.checkForUpdates()) {
            System.out.println("[HOI4Handler] Successfully loaded initial save data");
        } else {
            System.out.println("[HOI4Handler] No save data available yet");
        }
    }
    
    @Override
    public String getProcessName() {
        return "hoi4.exe";
    }
    
    @Override
    public String getGameName() {
        return "Hearts of Iron IV";
    }
    
    @Override
    public long getClientId() {
        return 1426482535223005217L;
    }
    
    @Override
    public String getLargeImageKey() {
        return "hoi4";
    }
    
    @Override
    public String getLargeImageText() {
        return "Hearts of Iron IV";
    }

    @Override
    public void updateActivity(Activity activity) {
        long currentTime = System.currentTimeMillis();
        if (currentTime - lastUpdateCheck >= UPDATE_INTERVAL) {
            if (dataExtractor.checkForUpdates()) {
                System.out.println("[HOI4Handler] Updated HOI4 game data from save file");
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
