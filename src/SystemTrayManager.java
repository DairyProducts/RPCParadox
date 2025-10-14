import java.awt.*;
import java.awt.image.BufferedImage;
import javax.swing.SwingUtilities;

/**
 * Manages the system tray icon for RPCParadox
 * 
 * @author DairyProducts
 * @version 1.0
 * @since 1.0
 */
public class SystemTrayManager {
    
    private TrayIcon trayIcon;
    private String currentStatus = "Waiting for game...";
    private Runnable exitCallback;
    
    /**
     * Set the callback to run when exit is requested
     * @param callback The callback to execute on exit
     */
    public void setExitCallback(Runnable callback) {
        this.exitCallback = callback;
    }
    
    /**
     * Initialize and display the system tray icon
     * @return true if successful, false if system tray is not supported
     */
    public boolean initialize() {
        if (!SystemTray.isSupported()) {
            System.err.println("System tray is not supported on this platform");
            return false;
        }
        
        try {
            SystemTray tray = SystemTray.getSystemTray();
            
            // Create icon image
            BufferedImage icon = createIcon();
            
            // Create popup menu
            PopupMenu popup = new PopupMenu();
            
            // Menu item: Status
            MenuItem statusItem = new MenuItem(currentStatus);
            statusItem.setEnabled(false);
            popup.add(statusItem);
            
            popup.addSeparator();
            
            // Menu item: About
            MenuItem aboutItem = new MenuItem("About RPCParadox");
            aboutItem.addActionListener(e -> showAboutDialog());
            popup.add(aboutItem);
            
            popup.addSeparator();
            
            // Menu item: Exit
            MenuItem exitItem = new MenuItem("Exit");
            exitItem.addActionListener(e -> {
                System.out.println("Exiting from system tray...");
                
                // Remove tray icon first
                remove();
                
                // Run exit callback if set
                if (exitCallback != null) {
                    exitCallback.run();
                }
                
                // Force exit
                new Thread(() -> {
                    try {
                        Thread.sleep(100); // Give time for cleanup
                    } catch (InterruptedException ex) {
                        // Ignore
                    }
                    System.exit(0);
                }).start();
            });
            popup.add(exitItem);
            
            // Create tray icon
            trayIcon = new TrayIcon(icon, "RPCParadox - " + currentStatus, popup);
            trayIcon.setImageAutoSize(true);
            
            // Add double-click listener to show status
            trayIcon.addActionListener(e -> showStatusDialog());
            
            // Add to system tray
            tray.add(trayIcon);
            
            System.out.println("System tray icon initialized successfully");
            return true;
            
        } catch (AWTException e) {
            System.err.println("Failed to add system tray icon: " + e.getMessage());
            return false;
        }
    }
    
    /**
     * Update the status displayed in the system tray
     * @param status The new status message
     */
    public void updateStatus(String status) {
        this.currentStatus = status;
        if (trayIcon != null) {
            trayIcon.setToolTip("RPCParadox - " + status);
            
            // Update the status menu item
            PopupMenu popup = trayIcon.getPopupMenu();
            if (popup.getItemCount() > 0) {
                MenuItem statusItem = popup.getItem(0);
                statusItem.setLabel(status);
            }
        }
    }
    
    /**
     * Display a notification in the system tray
     * @param title Notification title
     * @param message Notification message
     */
    public void showNotification(String title, String message) {
        if (trayIcon != null) {
            trayIcon.displayMessage(title, message, TrayIcon.MessageType.INFO);
        }
    }
    
    /**
     * Remove the system tray icon
     */
    public void remove() {
        if (trayIcon != null) {
            SystemTray.getSystemTray().remove(trayIcon);
            trayIcon = null;
        }
    }
    
    /**
     * Create a simple icon for the system tray
     * @return BufferedImage icon
     */
    private BufferedImage createIcon() {
        int size = 16;
        BufferedImage image = new BufferedImage(size, size, BufferedImage.TYPE_INT_ARGB);
        Graphics2D g = image.createGraphics();
        
        // Enable anti-aliasing
        g.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
        
        // Draw a simple "P" icon (for Paradox)
        g.setColor(new Color(88, 101, 242)); // Discord blurple color
        g.fillRoundRect(0, 0, size, size, 4, 4);
        
        g.setColor(Color.WHITE);
        g.setFont(new Font("Arial", Font.BOLD, 12));
        g.drawString("P", 3, 13);
        
        g.dispose();
        return image;
    }
    
    /**
     * Show status dialog
     */
    private void showStatusDialog() {
        SwingUtilities.invokeLater(() -> {
            Frame frame = new Frame();
            Dialog dialog = new Dialog(frame, "RPCParadox Status", true);
            dialog.setLayout(new BorderLayout(10, 10));
            
            Label statusLabel = new Label("Current Status: " + currentStatus, Label.CENTER);
            statusLabel.setFont(new Font("Arial", Font.PLAIN, 14));
            
            Panel buttonPanel = new Panel();
            Button okButton = new Button("OK");
            okButton.addActionListener(e -> dialog.dispose());
            buttonPanel.add(okButton);
            
            dialog.add(statusLabel, BorderLayout.CENTER);
            dialog.add(buttonPanel, BorderLayout.SOUTH);
            
            dialog.setSize(300, 120);
            dialog.setLocationRelativeTo(null);
            dialog.setVisible(true);
            frame.dispose();
        });
    }
    
    /**
     * Show about dialog
     */
    private void showAboutDialog() {
        SwingUtilities.invokeLater(() -> {
            Frame frame = new Frame();
            Dialog dialog = new Dialog(frame, "About RPCParadox", true);
            dialog.setLayout(new BorderLayout(10, 10));
            
            Panel textPanel = new Panel();
            textPanel.setLayout(new GridLayout(4, 1, 5, 5));
            
            Label titleLabel = new Label("RPCParadox", Label.CENTER);
            titleLabel.setFont(new Font("Arial", Font.BOLD, 16));
            
            Label descLabel = new Label("Discord Rich Presence for Paradox Games", Label.CENTER);
            Label versionLabel = new Label("Version 1.0", Label.CENTER);
            Label creditLabel = new Label("by DairyProducts", Label.CENTER);
            
            textPanel.add(titleLabel);
            textPanel.add(descLabel);
            textPanel.add(versionLabel);
            textPanel.add(creditLabel);
            
            Panel buttonPanel = new Panel();
            Button okButton = new Button("OK");
            okButton.addActionListener(e -> dialog.dispose());
            buttonPanel.add(okButton);
            
            dialog.add(textPanel, BorderLayout.CENTER);
            dialog.add(buttonPanel, BorderLayout.SOUTH);
            
            dialog.setSize(350, 180);
            dialog.setLocationRelativeTo(null);
            dialog.setVisible(true);
            frame.dispose();
        });
    }
}
