import tkinter as tk
from tkinter import messagebox, scrolledtext
from tkinter import ttk
import threading
import random
import logging
import os
import time
import sys
import winreg as reg
from utils import load_env_vars, load_config, save_config
import main
from pathlib import Path

# Load environment variables
load_env_vars()

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Global variables
stop_event = threading.Event()
selected_sources = []
update_thread = None  # Initialize update_thread

class TextHandler(logging.Handler):
    """Class to handle logging messages and display them in a Tkinter Text widget."""
    def __init__(self, text_widget):
        logging.Handler.__init__(self)
        self.text_widget = text_widget

    def emit(self, record):
        msg = self.format(record)
        def append():
            self.text_widget.configure(state='normal')
            self.text_widget.insert(tk.END, msg + '\n')
            self.text_widget.configure(state='disabled')
            self.text_widget.yview(tk.END)
        self.text_widget.after(0, append)

def set_startup(enable):
    s_name = "WallpaperUpdater"
    exe_path = sys.executable

    key = reg.HKEY_CURRENT_USER
    key_value = r"Software\Microsoft\Windows\CurrentVersion\Run"

    if enable:
        open_key = reg.OpenKey(key, key_value, 0, reg.KEY_ALL_ACCESS)
        reg.SetValueEx(open_key, s_name, 0, reg.REG_SZ, exe_path)
        reg.CloseKey(open_key)
        logging.info("Added to startup")
    else:
        try:
            open_key = reg.OpenKey(key, key_value, 0, reg.KEY_ALL_ACCESS)
            reg.DeleteValue(open_key, s_name)
            reg.CloseKey(open_key)
            logging.info("Removed from startup")
        except FileNotFoundError:
            logging.info("Already removed from startup")

def is_startup_enabled():
    s_name = "WallpaperUpdater"
    key = reg.HKEY_CURRENT_USER
    key_value = r"Software\Microsoft\Windows\CurrentVersion\Run"
    try:
        open_key = reg.OpenKey(key, key_value, 0, reg.KEY_READ)
        value, _ = reg.QueryValueEx(open_key, s_name)
        reg.CloseKey(open_key)
        return value == sys.executable
    except FileNotFoundError:
        return False

def start_wallpaper_update():
    global selected_sources
    while not stop_event.is_set():
        config = load_config()
        current_sources = list(selected_sources)
        if not current_sources:
            logging.info("No sources selected. Exiting wallpaper update process.")
            return

        source = random.choice(current_sources)
        logging.info(f"Randomly chosen source: {source} from {current_sources}")

        if stop_event.is_set():
            break

        save_location_path = Path(config['SAVE_LOCATION'])

        if source == "unsplash":
            unsplash_wallpapers = main.fetch_unsplash_wallpapers(query="landscape", count=1)
            main.save_unsplash_wallpapers(unsplash_wallpapers, save_location_path / "unsplash_wallpapers")
            unsplash_wallpaper_path = main.get_latest_wallpaper(save_location_path / "unsplash_wallpapers")
            if unsplash_wallpaper_path:
                main.set_unsplash_wallpaper(unsplash_wallpaper_path)
                main.close_wallpaper_engine()
            main.cleanup_old_wallpapers(save_location_path / "unsplash_wallpapers", int(config['MAX_WALLPAPERS']))
        
        elif source == "pexels":
            pexels_wallpapers = main.fetch_pexels_wallpapers(query="nature", count=1)
            main.save_pexels_wallpapers(pexels_wallpapers, save_location_path / "pexels_wallpapers")
            pexels_wallpaper_path = main.get_latest_wallpaper(save_location_path / "pexels_wallpapers")
            if pexels_wallpaper_path:
                main.set_pexels_wallpaper(pexels_wallpaper_path)
                main.close_wallpaper_engine()
            main.cleanup_old_wallpapers(save_location_path / "pexels_wallpapers", int(config['MAX_WALLPAPERS']))
        
        elif source == "wallpaper_engine":
            main.automate_wallpaper_update()
            main.cleanup_old_wallpapers(save_location_path, int(config['MAX_WALLPAPERS']))
        
        # Check stop_event periodically during sleep
        interval = int(config['CHECK_INTERVAL'])
        for _ in range(interval):
            if stop_event.is_set():
                return
            time.sleep(1)

        logging.info(f"Sleeping for {interval} seconds before the next update.")

def update_config_file():
    config = load_config()
    config['SAVE_LOCATION'] = entry_save_location.get()
    config['CHECK_INTERVAL'] = entry_check_interval.get()
    config['SCRAPE_INTERVAL'] = entry_scrape_interval.get()
    config['COLLECTIONS_URL'] = entry_collections_url.get()
    config['WALLPAPER_DOWNLOAD_LIMIT'] = entry_wallpaper_download_limit.get()
    config['MAX_WALLPAPERS'] = entry_max_wallpapers.get()
    config['SAVE_OLD_WALLPAPERS'] = str(var_save_old_wallpapers.get())
    
    # Save the state of the source buttons
    config['SOURCE_UNSPLASH'] = var_unsplash.get()
    config['SOURCE_PEXELS'] = var_pexels.get()
    config['SOURCE_WALLPAPER_ENGINE'] = var_wallpaper_engine.get()
    
    # Save the state of the update process
    config['UPDATE_RUNNING'] = update_thread is not None and update_thread.is_alive()
    
    save_config(config)

def on_start():
    global stop_event, selected_sources, update_thread
    # Check if update_thread is already running and stop it
    if update_thread and update_thread.is_alive():
        logging.info("Stopping the previous update thread.")
        stop_event.set()
        update_thread.join()  # Wait for the thread to finish

    selected_sources = []
    if var_unsplash.get():
        selected_sources.append("unsplash")
    if var_pexels.get():
        selected_sources.append("pexels")
    if var_wallpaper_engine.get():
        selected_sources.append("wallpaper_engine")
    
    if not selected_sources:
        messagebox.showinfo("No Selection", "No sources selected. Stopping wallpaper updates.")
        logging.info("No sources selected. Stopping wallpaper updates.")
        return

    # Update the configuration settings in the config.json file
    update_config_file()

    # Reload configuration settings from config.json
    config = load_config()

    # Debugging: Log the updated values
    logging.info(f"Updated SAVE_LOCATION: {config['SAVE_LOCATION']}")
    logging.info(f"Updated CHECK_INTERVAL: {config['CHECK_INTERVAL']}")
    logging.info(f"Updated SCRAPE_INTERVAL: {config['SCRAPE_INTERVAL']}")
    logging.info(f"Updated COLLECTIONS_URL: {config['COLLECTIONS_URL']}")
    logging.info(f"Updated WALLPAPER_DOWNLOAD_LIMIT: {config['WALLPAPER_DOWNLOAD_LIMIT']}")
    logging.info(f"Updated MAX_WALLPAPERS: {config['MAX_WALLPAPERS']}")
    logging.info(f"Updated SAVE_OLD_WALLPAPERS: {config['SAVE_OLD_WALLPAPERS']}")

    stop_event = threading.Event()

    num_sources = len(selected_sources)
    if num_sources == 1:
        logging.info(f"Only one source selected: {selected_sources[0]}")
    elif num_sources == 2:
        logging.info(f"Two sources selected: {selected_sources}")
    elif num_sources == 3:
        logging.info(f"All three sources selected: {selected_sources}")

    update_thread = threading.Thread(target=start_wallpaper_update)
    update_thread.start()

    # Save the running state
    config['UPDATE_RUNNING'] = True
    save_config(config)

def on_startup_checkbox_change():
    set_startup(var_startup.get())

def on_close():
    """Handle the window close event."""
    update_config_file()  # Save the configuration before closing
    if update_thread and update_thread.is_alive():
        logging.info("Stopping the update thread before closing.")
        stop_event.set()  # Signal the thread to stop
        update_thread.join()  # Wait for the thread to finish
    root.destroy()  # Destroy the main window and exit the application

root = tk.Tk()
root.title("Wallpaper Updater")
root.geometry("800x600")  # Set window size larger to accommodate all widgets
root.resizable(True, True)  # Allow window resizing

# Apply a theme
style = ttk.Style(root)
style.theme_use('clam')  # Choose a modern theme like 'clam' or 'alt'

# Set a consistent font
default_font = ("Helvetica", 10)
root.option_add("*Font", default_font)

var_unsplash = tk.BooleanVar()
var_pexels = tk.BooleanVar()
var_wallpaper_engine = tk.BooleanVar()
var_startup = tk.BooleanVar(value=is_startup_enabled())
var_save_old_wallpapers = tk.BooleanVar()

# Create frames for better layout management
frame_sources = ttk.LabelFrame(root, text="Sources", padding="10")
frame_sources.grid(row=0, column=0, padx=10, pady=10, sticky="nw")

chk_unsplash = ttk.Checkbutton(frame_sources, text="Unsplash", variable=var_unsplash)
chk_pexels = ttk.Checkbutton(frame_sources, text="Pexels", variable=var_pexels)
chk_wallpaper_engine = ttk.Checkbutton(frame_sources, text="Wallpaper Engine", variable=var_wallpaper_engine)

chk_unsplash.grid(row=0, column=0, sticky="w", pady=2)
chk_pexels.grid(row=1, column=0, sticky="w", pady=2)
chk_wallpaper_engine.grid(row=2, column=0, sticky="w", pady=2)

frame_other_settings = ttk.LabelFrame(root, text="Other Settings", padding="10")
frame_other_settings.grid(row=1, column=0, padx=10, pady=10, sticky="nw")

chk_startup = ttk.Checkbutton(frame_other_settings, text="Start with Windows", variable=var_startup, command=on_startup_checkbox_change)
chk_startup.grid(row=0, column=0, sticky="w", pady=2)

chk_save_old_wallpapers = ttk.Checkbutton(frame_other_settings, text="Save Old Wallpapers", variable=var_save_old_wallpapers)
chk_save_old_wallpapers.grid(row=1, column=0, sticky="w", pady=2)

frame_settings = ttk.LabelFrame(root, text="Settings", padding="10")
frame_settings.grid(row=0, column=1, rowspan=2, padx=10, pady=10, sticky="nw")

ttk.Label(frame_settings, text="Save Location:").grid(row=0, column=0, sticky="e")
entry_save_location = ttk.Entry(frame_settings, width=40)
entry_save_location.grid(row=0, column=1, padx=5, pady=5)

ttk.Label(frame_settings, text="Check Interval (seconds):").grid(row=1, column=0, sticky="e")
entry_check_interval = ttk.Entry(frame_settings, width=20)
entry_check_interval.grid(row=1, column=1, padx=5, pady=5)

ttk.Label(frame_settings, text="Scrape Interval (seconds):").grid(row=2, column=0, sticky="e")
entry_scrape_interval = ttk.Entry(frame_settings, width=20)
entry_scrape_interval.grid(row=2, column=1, padx=5, pady=5)

ttk.Label(frame_settings, text="Collections URL:").grid(row=3, column=0, sticky="e")
entry_collections_url = ttk.Entry(frame_settings, width=40)
entry_collections_url.grid(row=3, column=1, padx=5, pady=5)

ttk.Label(frame_settings, text="Wallpaper Download Limit:").grid(row=4, column=0, sticky="e")
entry_wallpaper_download_limit = ttk.Entry(frame_settings, width=20)
entry_wallpaper_download_limit.grid(row=4, column=1, padx=5, pady=5)

ttk.Label(frame_settings, text="Max Wallpapers:").grid(row=5, column=0, sticky="e")
entry_max_wallpapers = ttk.Entry(frame_settings, width=20)
entry_max_wallpapers.grid(row=5, column=1, padx=5, pady=5)

frame_logs = ttk.LabelFrame(root, text="Logs", padding="10")
frame_logs.grid(row=2, column=0, columnspan=2, padx=10, pady=10, sticky="nsew")

log_text = scrolledtext.ScrolledText(frame_logs, state='disabled', width=70, height=10)
log_text.grid(row=0, column=0, padx=5, pady=5)

# Set up logging to use the TextHandler
text_handler = TextHandler(log_text)
logging.getLogger().addHandler(text_handler)

frame_actions = ttk.Frame(root, padding="10")
frame_actions.grid(row=3, column=0, columnspan=2, padx=10, pady=10, sticky="ew")

btn_start = ttk.Button(frame_actions, text="Start", command=on_start)
btn_start.grid(row=0, column=0, padx=5, pady=5, sticky="ew")

root.grid_rowconfigure(2, weight=1)
root.grid_columnconfigure(0, weight=1)

# Insert initial values into the entries
config = load_config()
entry_save_location.insert(0, config['SAVE_LOCATION'])
entry_check_interval.insert(0, config['CHECK_INTERVAL'])
entry_scrape_interval.insert(0, config['SCRAPE_INTERVAL'])
entry_collections_url.insert(0, config['COLLECTIONS_URL'])
entry_wallpaper_download_limit.insert(0, config['WALLPAPER_DOWNLOAD_LIMIT'])
entry_max_wallpapers.insert(0, config['MAX_WALLPAPERS'])

# Set the state of the source buttons
if 'SOURCE_UNSPLASH' in config:
    var_unsplash.set(config['SOURCE_UNSPLASH'])
if 'SOURCE_PEXELS' in config:
    var_pexels.set(config['SOURCE_PEXELS'])
if 'SOURCE_WALLPAPER_ENGINE' in config:
    var_wallpaper_engine.set(config['SOURCE_WALLPAPER_ENGINE'])

# Automatically start the update process if it was running previously
if config.get('UPDATE_RUNNING', False):
    on_start()

# Bind the on_close function to the window close event
root.protocol("WM_DELETE_WINDOW", on_close)

# Run the Tkinter event loop
root.mainloop()