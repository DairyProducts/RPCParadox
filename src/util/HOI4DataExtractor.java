package util;

import java.io.*;
import java.nio.file.*;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

// I'm gonna be honest most of this file is vibe coded
// Idk how HoI4 Save Files work

/**
 * Extracts game data from Hearts of Iron IV save files
 * 
 * @author DairyProducts
 * @version 1.1
 * @since 1.1
 */
public class HOI4DataExtractor {
    
    private static final byte[] TXT_HEADER = "HOI4txt".getBytes();
    private static final byte[] BIN_HEADER = "HOI4bin".getBytes();
    
    private static final Pattern TAG_PATTERN = Pattern.compile("player=\"([A-Z]{3})\"");
    private static final Pattern DATE_PATTERN = Pattern.compile("date=\"(\\d{4})\\.(\\d+)\\.(\\d+)\"");
    
    private String lastSaveFile = null;
    private String countryTag = null;
    private String countryName = null;
    private String year = null;
    private boolean isBinary = false;
    private long lastModified = 0;
    
    /**
     * Get the HOI4 save game directory
     * @return Path to the save directory or null if not found
     */
    public Path getSaveDirectory() {
        String userHome = System.getProperty("user.home");
        Path savePath = Paths.get(userHome, "Documents", "Paradox Interactive", "Hearts of Iron IV", "save games");
        
        System.out.println("[HOI4DataExtractor] Checking save directory: " + savePath);
        
        if (Files.exists(savePath)) {
            System.out.println("[HOI4DataExtractor] Save directory found!");
            return savePath;
        }
        System.out.println("[HOI4DataExtractor] Save directory not found");
        return null;
    }
    
    /**
     * Find the most recent save file in the save directory
     * @return Path to the most recent save file or null if none found
     */
    public Path getMostRecentSaveFile() {
        Path saveDir = getSaveDirectory();
        if (saveDir == null || !Files.exists(saveDir)) {
            System.out.println("[HOI4DataExtractor] Save directory is null or doesn't exist");
            return null;
        }
        
        try {
            Path result = Files.walk(saveDir, 2)
                .filter(p -> p.toString().endsWith(".hoi4"))
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
                System.out.println("[HOI4DataExtractor] Found most recent save: " + result);
            } else {
                System.out.println("[HOI4DataExtractor] No .hoi4 files found in directory");
            }
            
            return result;
        } catch (IOException e) {
            System.err.println("[HOI4DataExtractor] Error finding save files: " + e.getMessage());
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
            System.out.println("[HOI4DataExtractor] No save file available for update check");
            return false;
        }
        
        try {
            long modified = Files.getLastModifiedTime(mostRecent).toMillis();
            String filename = mostRecent.toString();
            
            if (!filename.equals(lastSaveFile) || modified > lastModified) {
                System.out.println("[HOI4DataExtractor] New or updated save file detected: " + mostRecent.getFileName());
                lastSaveFile = filename;
                lastModified = modified;
                extractData(mostRecent);
                return true;
            }
        } catch (IOException e) {
            System.err.println("[HOI4DataExtractor] Error checking file modification time: " + e.getMessage());
        }
        
        return false;
    }
    
    /**
     * Detect if a save file is binary or plaintext by reading the header
     * @param saveFile Path to the save file
     * @return true if binary, false if plaintext
     */
    private boolean detectBinaryFormat(Path saveFile) throws IOException {
        try (InputStream is = Files.newInputStream(saveFile)) {
            byte[] header = new byte[7];
            int bytesRead = is.read(header);
            
            if (bytesRead < 7) {
                System.out.println("[HOI4DataExtractor] File too short to have valid header");
                return false;
            }
            
            if (java.util.Arrays.equals(header, BIN_HEADER)) {
                System.out.println("[HOI4DataExtractor] Detected BINARY save file (Ironman mode)");
                return true;
            } else if (java.util.Arrays.equals(header, TXT_HEADER)) {
                System.out.println("[HOI4DataExtractor] Detected PLAINTEXT save file");
                return false;
            } else {
                System.out.println("[HOI4DataExtractor] Unknown header format");
                return false;
            }
        }
    }
    
    /**
     * Extract game data from a save file
     * @param saveFile Path to the save file
     */
    private void extractData(Path saveFile) {
        System.out.println("[HOI4DataExtractor] Extracting data from: " + saveFile.getFileName());
        
        try {
            isBinary = detectBinaryFormat(saveFile);
            
            if (isBinary) {
                // Binary save - we can't parse it, just mark it as Ironman
                System.out.println("[HOI4DataExtractor] Binary save detected - Ironman mode");
                countryTag = null;
                countryName = null;
                year = null;
            } else {
                // Plaintext save - parse it
                System.out.println("[HOI4DataExtractor] Parsing plaintext save file");
                extractFromPlaintext(saveFile);
            }
            
            System.out.println("[HOI4DataExtractor] Extracted data - Binary: " + isBinary + 
                             ", Country: " + countryName + ", Year: " + year);
        } catch (IOException e) {
            System.err.println("[HOI4DataExtractor] Error extracting data from save file: " + e.getMessage());
            e.printStackTrace();
        }
    }
    
    /**
     * Extract data from plaintext save file
     */
    private void extractFromPlaintext(Path saveFile) throws IOException {
        try (BufferedReader reader = Files.newBufferedReader(saveFile)) {
            // Skip the header line (HOI4txt)
            reader.readLine();
            
            // Read first ~1000 lines to find player tag and date
            StringBuilder content = new StringBuilder();
            String line;
            int linesRead = 0;
            int maxLines = 1000;
            
            while ((line = reader.readLine()) != null && linesRead < maxLines) {
                content.append(line).append("\n");
                linesRead++;
            }
            
            String fileContent = content.toString();
            
            // Extract player tag
            Matcher tagMatcher = TAG_PATTERN.matcher(fileContent);
            if (tagMatcher.find()) {
                countryTag = tagMatcher.group(1);
                countryName = getCountryName(countryTag);
                System.out.println("[HOI4DataExtractor] Found player tag: " + countryTag + " (" + countryName + ")");
            }
            
            // Extract date
            Matcher dateMatcher = DATE_PATTERN.matcher(fileContent);
            if (dateMatcher.find()) {
                year = dateMatcher.group(1);
                System.out.println("[HOI4DataExtractor] Found year: " + year);
            }
        }
    }
    
    /**
     * Convert country tag to full country name
     * @param tag Three-letter country tag
     * @return Full country name
     */
    private String getCountryName(String tag) {
        // Common country mappings - expand as needed
        switch (tag) {
            case "GER": return "German Reich";
            case "SOV": return "Soviet Union";
            case "USA": return "United States";
            case "ENG": return "United Kingdom";
            case "FRA": return "France";
            case "ITA": return "Italy";
            case "JAP": return "Japan";
            case "CHI": return "China";
            case "POL": return "Poland";
            case "CAN": return "Canada";
            case "AST": return "Australia";
            case "NZL": return "New Zealand";
            case "SAF": return "South Africa";
            case "RAJ": return "British Raj";
            case "HUN": return "Hungary";
            case "ROM": return "Romania";
            case "YUG": return "Yugoslavia";
            case "SWE": return "Sweden";
            case "NOR": return "Norway";
            case "FIN": return "Finland";
            case "SPR": return "Republican Spain";
            case "SPA": return "Nationalist Spain";
            case "POR": return "Portugal";
            case "BEL": return "Belgium";
            case "HOL": return "Netherlands";
            case "LUX": return "Luxembourg";
            case "DEN": return "Denmark";
            case "GRE": return "Greece";
            case "TUR": return "Turkey";
            case "BUL": return "Bulgaria";
            case "MEX": return "Mexico";
            case "BRA": return "Brazil";
            case "ARG": return "Argentina";
            default: return tag; // Return tag if unknown
        }
    }
    
    /**
     * Check if the current save is in binary (Ironman) format
     * @return true if binary/Ironman, false otherwise
     */
    public boolean isBinary() {
        return isBinary;
    }
    
    /**
     * Get the country name
     * @return Country name or null if not available
     */
    public String getCountryName() {
        return countryName;
    }
    
    /**
     * Get the current game year
     * @return Year string or null if not available
     */
    public String getYear() {
        return year;
    }
    
    /**
     * Get a formatted details string for Discord (line 1)
     * @return Formatted string
     */
    public String getDetailsText() {
        if (isBinary) {
            return "Conquering the World";
        } else if (countryName != null) {
            return "Playing as " + countryName;
        }
        return "Conquering the World";
    }
    
    /**
     * Get a formatted state string for Discord (line 2)
     * @return Formatted string or null
     */
    public String getStateText() {
        if (isBinary) {
            return "Ironman Mode";
        } else if (year != null) {
            return "Year: " + year;
        }
        return null;
    }
}
