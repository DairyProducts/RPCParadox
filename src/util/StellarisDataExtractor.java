package util;

import java.io.*;
import java.nio.file.*;
import java.util.zip.ZipFile;
import java.util.zip.ZipEntry;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Extracts game data from Stellaris save files
 * 
 * @author DairyProducts
 * @version 1.1
 * @since 1.1
 */
public class StellarisDataExtractor {
    
    private static final Pattern DATE_PATTERN = Pattern.compile("date=\"([^\"]+)\"");
    private static final Pattern NAME_PATTERN = Pattern.compile("name=\"([^\"]+)\"");
    
    private String lastSaveFile = null;
    private String gameDate = null;
    private String empireName = null;
    private long lastModified = 0;
    
    /**
     * Get the Stellaris save game directory
     * @return Path to the save directory or null if not found
     */
    public Path getSaveDirectory() {
        String userHome = System.getProperty("user.home");
        Path savePath = Paths.get(userHome, "Documents", "Paradox Interactive", "Stellaris", "save games");
        
        System.out.println("[StellarisDataExtractor] Checking save directory: " + savePath);
        
        if (Files.exists(savePath)) {
            System.out.println("[StellarisDataExtractor] Save directory found!");
            return savePath;
        }
        System.out.println("[StellarisDataExtractor] Save directory not found");
        return null;
    }
    
    /**
     * Find the most recent save file in the save directory
     * @return Path to the most recent save file or null if none found
     */
    public Path getMostRecentSaveFile() {
        Path saveDir = getSaveDirectory();
        if (saveDir == null || !Files.exists(saveDir)) {
            System.out.println("[StellarisDataExtractor] Save directory is null or doesn't exist");
            return null;
        }
        
        try {
            Path result = Files.walk(saveDir, 2)
                .filter(p -> p.toString().endsWith(".sav"))
                .filter(p -> !p.getFileName().toString().startsWith("ironman"))
                .max((p1, p2) -> {
                    try {
                        long t1 = Files.getLastModifiedTime(p1).toMillis();
                        long t2 = Files.getLastModifiedTime(p2).toMillis();
                        return Long.compare(t1, t2);
                    } catch (IOException e) {
                        return 0;
                    }
                })
                .orElse(null);
            
            if (result != null) {
                System.out.println("[StellarisDataExtractor] Found most recent save: " + result);
            } else {
                System.out.println("[StellarisDataExtractor] No .sav files found in directory");
            }
            
            return result;
        } catch (IOException e) {
            System.err.println("[StellarisDataExtractor] Error finding save files: " + e.getMessage());
            e.printStackTrace();
            return null;
        }
    }
    
    /**
     * Check if there's a new save file or if the current one has been updated
     * @return true if data was updated, false otherwise
     */
    public boolean checkForUpdates() {
        Path mostRecent = getMostRecentSaveFile();
        if (mostRecent == null) {
            System.out.println("[StellarisDataExtractor] No save file available for update check");
            return false;
        }
        
        try {
            long modified = Files.getLastModifiedTime(mostRecent).toMillis();
            String filename = mostRecent.toString();
            
            if (!filename.equals(lastSaveFile) || modified > lastModified) {
                System.out.println("[StellarisDataExtractor] New or updated save file detected: " + mostRecent.getFileName());
                lastSaveFile = filename;
                lastModified = modified;
                extractData(mostRecent);
                return true;
            }
        } catch (IOException e) {
            System.err.println("[StellarisDataExtractor] Error checking file modification time: " + e.getMessage());
        }
        
        return false;
    }
    
    /**
     * Extract game data from a save file
     * @param saveFile Path to the save file
     */
    private void extractData(Path saveFile) {
        System.out.println("[StellarisDataExtractor] Extracting data from: " + saveFile.getFileName());
        try (ZipFile zipFile = new ZipFile(saveFile.toFile())) {
            // Extract from meta file first (contains basic info)
            ZipEntry metaEntry = zipFile.getEntry("meta");
            if (metaEntry != null) {
                System.out.println("[StellarisDataExtractor] Found 'meta' entry in save file");
                String metaContent = readZipEntry(zipFile, metaEntry);
                extractFromMeta(metaContent);
            } else {
                System.out.println("[StellarisDataExtractor] No 'meta' entry found in save file");
            }
            
            ZipEntry gamestateEntry = zipFile.getEntry("gamestate");
            if (gamestateEntry != null) {
                System.out.println("[StellarisDataExtractor] Found 'gamestate' entry in save file");
                String gamestateContent = readZipEntry(zipFile, gamestateEntry);
                extractFromGamestate(gamestateContent);
            } else {
                System.out.println("[StellarisDataExtractor] No 'gamestate' entry found in save file");
            }
            
            System.out.println("[StellarisDataExtractor] Extracted data - Date: " + gameDate + ", Empire: " + empireName);
        } catch (IOException e) {
            System.err.println("[StellarisDataExtractor] Error extracting data from save file: " + e.getMessage());
            e.printStackTrace();
        }
    }
    
    /**
     * Read content from a zip entry
     */
    private String readZipEntry(ZipFile zipFile, ZipEntry entry) throws IOException {
        try (InputStream is = zipFile.getInputStream(entry);
             BufferedReader reader = new BufferedReader(new InputStreamReader(is))) {
            StringBuilder content = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) {
                content.append(line).append("\n");
            }
            return content.toString();
        }
    }
    
    /**
     * Extract basic information from meta file
     */
    private void extractFromMeta(String metaContent) {
        // Extract date
        Matcher dateMatcher = DATE_PATTERN.matcher(metaContent);
        if (dateMatcher.find()) {
            gameDate = dateMatcher.group(1);
        }
        
        // Extract empire name from meta
        Matcher nameMatcher = NAME_PATTERN.matcher(metaContent);
        if (nameMatcher.find()) {
            empireName = nameMatcher.group(1);
        }
    }
    
    /**
     * Extract detailed information from gamestate file
     */
    private void extractFromGamestate(String gamestateContent) {
        // Simple extraction - just get empire name if not already set from meta
        if (empireName == null || empireName.isEmpty()) {
            Pattern namePattern = Pattern.compile("name=\"([^\"]+)\"");
            Matcher nameMatcher = namePattern.matcher(gamestateContent);
            if (nameMatcher.find()) {
                empireName = nameMatcher.group(1);
            }
        }
    }
    
    /**
     * Get the current game date
     * @return Game date string (e.g., "2200.01.01") or null if not available
     */
    public String getGameDate() {
        return gameDate;
    }
    
    /**
     * Get the empire name
     * @return Empire name or null if not available
     */
    public String getEmpireName() {
        return empireName;
    }
    
    /**
     * Get a formatted details string for Discord (line 1)
     * @return Formatted string with empire name
     */
    public String getDetailsText() {
        if (empireName != null) {
            return "Playing as " + empireName;
        }
        return "Exploring the Galaxy";
    }
    
    /**
     * Get a formatted state string for Discord (line 2)
     * @return Formatted string with year
     */
    public String getStateText() {
        if (gameDate != null) {
            // Extract year from date (format: YYYY.MM.DD)
            String year = gameDate.split("\\.")[0];
            return "Year: " + year;
        }
        return null;
    }
}
