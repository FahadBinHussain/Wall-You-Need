import logging
import random
from pathlib import Path
import os
import shutil
import time  # Import time module for sleep functionality
import threading  # Import threading module
from wallpaper_engine import (
    automate_wallpaper_update,
    close_wallpaper_engine,
)
from unsplash import fetch_unsplash_wallpapers, save_wallpapers as save_unsplash_wallpapers, set_wallpaper as set_unsplash_wallpaper
from pexels import fetch_pexels_wallpapers, save_wallpapers as save_pexels_wallpapers, set_wallpaper as set_pexels_wallpaper
from env import SAVE_LOCATION, MAX_WALLPAPERS, CHECK_INTERVAL  # Import CHECK_INTERVAL

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

def get_latest_wallpaper(directory):
    """Get the latest wallpaper file from the specified directory."""
    wallpapers = list(directory.glob("*.jpg"))
    if not wallpapers:
        return None
    latest_wallpaper = max(wallpapers, key=os.path.getctime)
    logging.info(f"Latest wallpaper identified: {latest_wallpaper}")
    return latest_wallpaper

def cleanup_old_wallpapers(directory, max_wallpapers):
    """Delete old wallpapers to save space."""
    # Clean up .jpg files
    wallpaper_files = list(directory.glob("*.jpg"))
    
    if len(wallpaper_files) > max_wallpapers:
        # Sort by creation time
        wallpaper_files.sort(key=os.path.getctime)
        
        for file_to_delete in wallpaper_files[:-max_wallpapers]:
            logging.info(f"Deleting old wallpaper file: {file_to_delete}")
            try:
                file_to_delete.unlink()
                logging.info(f"Successfully deleted: {file_to_delete}")
            except OSError as e:
                logging.error(f"Failed to delete old wallpaper {file_to_delete}: {e}")

    # Clean up old directories
    target_dir = directory / "projects" / "myprojects"
    if target_dir.exists() and target_dir.is_dir():
        wallpaper_dirs = [d for d in target_dir.iterdir() if d.is_dir()]
        if len(wallpaper_dirs) > max_wallpapers:
            # Sort by creation time
            wallpaper_dirs.sort(key=os.path.getctime)
            for dir_to_delete in wallpaper_dirs[:len(wallpaper_dirs) - max_wallpapers]:
                logging.info(f"Deleting old wallpaper directory: {dir_to_delete}")
                try:
                    # Ensure all files are writable before attempting deletion
                    for root, dirs, files in os.walk(dir_to_delete):
                        for file in files:
                            file_path = os.path.join(root, file)
                            os.chmod(file_path, 0o777)  # Make file writable
                    shutil.rmtree(dir_to_delete)
                    logging.info(f"Successfully deleted: {dir_to_delete}")
                except PermissionError as e:
                    logging.error(f"PermissionError: Could not delete {dir_to_delete}. {e}")
                except Exception as e:
                    logging.error(f"Unexpected error while deleting {dir_to_delete}: {e}")

def update_wallpaper():
    """Function to update the wallpaper based on a random choice of sources."""
    # Randomly select a source to get wallpapers from
    choices = ["unsplash", "pexels", "wallpaper_engine"]
    choice = random.choice(choices)

    if choice == "unsplash":
        logging.info("Selected Unsplash for wallpapers.")
        
        # Fetch and save Unsplash wallpapers
        unsplash_wallpapers = fetch_unsplash_wallpapers(query="landscape", count=1)
        save_unsplash_wallpapers(unsplash_wallpapers, SAVE_LOCATION / "unsplash_wallpapers")
        # Set the latest wallpaper from Unsplash
        unsplash_wallpaper_path = get_latest_wallpaper(SAVE_LOCATION / "unsplash_wallpapers")
        if unsplash_wallpaper_path:
            set_unsplash_wallpaper(unsplash_wallpaper_path)
            # Close Wallpaper Engine if running
            close_wallpaper_engine()
        # Cleanup old wallpapers
        cleanup_old_wallpapers(SAVE_LOCATION / "unsplash_wallpapers", MAX_WALLPAPERS)
    
    elif choice == "pexels":
        logging.info("Selected Pexels for wallpapers.")
        
        # Fetch and save Pexels wallpapers
        pexels_wallpapers = fetch_pexels_wallpapers(query="nature", count=1)
        save_pexels_wallpapers(pexels_wallpapers, SAVE_LOCATION / "pexels_wallpapers")
        # Set the latest wallpaper from Pexels
        pexels_wallpaper_path = get_latest_wallpaper(SAVE_LOCATION / "pexels_wallpapers")
        if pexels_wallpaper_path:
            set_pexels_wallpaper(pexels_wallpaper_path)
            # Close Wallpaper Engine if running
            close_wallpaper_engine()
        # Cleanup old wallpapers
        cleanup_old_wallpapers(SAVE_LOCATION / "pexels_wallpapers", MAX_WALLPAPERS)
    
    elif choice == "wallpaper_engine":
        logging.info("Selected Wallpaper Engine for wallpapers.")
        # Automate the full process for Wallpaper Engine
        automate_wallpaper_update()
        time.sleep(100)
        cleanup_old_wallpapers(SAVE_LOCATION, MAX_WALLPAPERS)

def schedule_wallpaper_update():
    """Function to schedule wallpaper updates at regular intervals."""
    while True:
        update_wallpaper()
        logging.info(f"Sleeping for {CHECK_INTERVAL} seconds before the next update.")
        time.sleep(CHECK_INTERVAL)

if __name__ == "__main__":
    # Run the scheduling function in a separate thread
    wallpaper_thread = threading.Thread(target=schedule_wallpaper_update)
    wallpaper_thread.daemon = True  # Ensure the thread exits when the main program exits
    wallpaper_thread.start()

    # Keep the main program running
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        logging.info("Program interrupted by the user. Exiting...")