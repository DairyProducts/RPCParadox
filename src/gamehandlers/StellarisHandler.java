package gamehandlers;

import de.jcm.discordgamesdk.activity.Activity;
import java.time.Instant;

/**
 * Handler for Stellaris
 * 
 * @author DairyProducts
 * @version 1.0
 * @since 1.0
 */
public class StellarisHandler implements GameHandler {

    private final Instant startTime;

    public StellarisHandler() {
        this.startTime = Instant.now();
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
        activity.setDetails("Exploring the Galaxy");
        activity.timestamps().setStart(startTime);
        activity.assets().setLargeImage(getLargeImageKey());
        activity.assets().setLargeText(getLargeImageText());
    }
}
