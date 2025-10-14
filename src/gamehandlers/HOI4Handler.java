package gamehandlers;

import de.jcm.discordgamesdk.activity.Activity;
import java.time.Instant;

/**
 * Handler for Hearts of Iron IV
 * 
 * @author DairyProducts
 * @version 1.0
 * @since 1.0
 */
public class HOI4Handler implements GameHandler {
    
    private Instant startTime;
    
    public HOI4Handler() {
        this.startTime = Instant.now();
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
        activity.setDetails("Conquering the World");
        activity.timestamps().setStart(startTime);
        activity.assets().setLargeImage(getLargeImageKey());
        activity.assets().setLargeText(getLargeImageText());
    }
}
