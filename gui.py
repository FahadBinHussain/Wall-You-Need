import tkinter as tk
from tkinter import messagebox
import threading
import random
import logging
import main  # Import your main module

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Global variable to control the running state of the wallpaper update process
stop_event = threading.Event()
selected_sources = []

# Function to start the wallpaper update process based on selected sources
def start_wallpaper_update():
    global selected_sources
    while not stop_event.is_set():
        current_sources = list(selected_sources)  # Take a snapshot of the current sources
        if not current_sources:
            logging.info("No sources selected. Exiting wallpaper update process.")
            return  # Exit immediately if no sources are selected

        # Randomly select one source from the current sources
        source = random.choice(current_sources)
        
        logging.info(f"Randomly chosen source: {source} from {current_sources}")

        if stop_event.is_set():
            break  # Exit the loop if stop_event is set
        
        if source == "unsplash":
            
            unsplash_wallpapers = main.fetch_unsplash_wallpapers(query="landscape", count=1)
            main.save_unsplash_wallpapers(unsplash_wallpapers, main.SAVE_LOCATION / "unsplash_wallpapers")
            unsplash_wallpaper_path = main.get_latest_wallpaper(main.SAVE_LOCATION / "unsplash_wallpapers")
            if unsplash_wallpaper_path:
                main.set_unsplash_wallpaper(unsplash_wallpaper_path)
                main.close_wallpaper_engine()
            main.cleanup_old_wallpapers(main.SAVE_LOCATION / "unsplash_wallpapers", main.MAX_WALLPAPERS)
        
        elif source == "pexels":
            
            pexels_wallpapers = main.fetch_pexels_wallpapers(query="nature", count=1)
            main.save_pexels_wallpapers(pexels_wallpapers, main.SAVE_LOCATION / "pexels_wallpapers")
            pexels_wallpaper_path = main.get_latest_wallpaper(main.SAVE_LOCATION / "pexels_wallpapers")
            if pexels_wallpaper_path:
                main.set_pexels_wallpaper(pexels_wallpaper_path)
                main.close_wallpaper_engine()
            main.cleanup_old_wallpapers(main.SAVE_LOCATION / "pexels_wallpapers", main.MAX_WALLPAPERS)
        
        elif source == "wallpaper_engine":
            main.automate_wallpaper_update()
            main.cleanup_old_wallpapers(main.SAVE_LOCATION, main.MAX_WALLPAPERS)
        
        if not stop_event.is_set():
            main.time.sleep(main.CHECK_INTERVAL)

# Function to handle the start button click
def on_start():
    global stop_event, selected_sources
    stop_event.set()  # Stop any existing wallpaper update process

    # Update the selected sources based on checkbox states
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

    # Create a new stop event for the new thread
    stop_event = threading.Event()

    # Log the number of selected sources
    num_sources = len(selected_sources)
    if num_sources == 1:
        logging.info(f"Only one source selected: {selected_sources[0]}")
    elif num_sources == 2:
        logging.info(f"Two sources selected: {selected_sources}")
    elif num_sources == 3:
        logging.info(f"All three sources selected: {selected_sources}")

    # Run the wallpaper update process in a separate thread
    threading.Thread(target=start_wallpaper_update).start()

# Create the main Tkinter window
root = tk.Tk()
root.title("Wallpaper Updater")

# Create checkboxes for each source
var_unsplash = tk.BooleanVar()
var_pexels = tk.BooleanVar()
var_wallpaper_engine = tk.BooleanVar()

chk_unsplash = tk.Checkbutton(root, text="Unsplash", variable=var_unsplash)
chk_pexels = tk.Checkbutton(root, text="Pexels", variable=var_pexels)
chk_wallpaper_engine = tk.Checkbutton(root, text="Wallpaper Engine", variable=var_wallpaper_engine)

chk_unsplash.pack()
chk_pexels.pack()
chk_wallpaper_engine.pack()

# Create a start button
btn_start = tk.Button(root, text="Start", command=on_start)
btn_start.pack()

# Run the Tkinter event loop
root.mainloop()