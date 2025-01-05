import logging
import random
from pathlib import Path
import os
import shutil
import time
import threading
import subprocess
from dotenv import load_dotenv
from wallpaper_engine import (
    automate_wallpaper_update,
    close_wallpaper_engine,
)
from unsplash import fetch_unsplash_wallpapers, save_wallpapers as save_unsplash_wallpapers, set_wallpaper as set_unsplash_wallpaper
from pexels import fetch_pexels_wallpapers, save_wallpapers as save_pexels_wallpapers, set_wallpaper as set_pexels_wallpaper
from utils import load_env_vars, load_config, save_config  # Import utility functions

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Load environment variables
load_env_vars()

def get_latest_wallpaper(directory):
    """Get the latest wallpaper file from the specified directory."""
    wallpapers = list(directory.glob("*.jpg"))
    if not wallpapers:
        return None
    latest_wallpaper = max(wallpapers, key=os.path.getctime)
    logging.info(f"Latest wallpaper identified: {latest_wallpaper}")
    return latest_wallpaper

def terminate_depotdownloader():
    """Terminate DepotDownloadermod.exe if it is running."""
    try:
        subprocess.run(["taskkill", "/f", "/im", "DepotDownloadermod.exe"], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        logging.info("Successfully terminated DepotDownloadermod.exe")
    except subprocess.CalledProcessError as e:
        logging.error(f"Failed to terminate DepotDownloadermod.exe: {e}")

def cleanup_old_wallpapers(directory, max_wallpapers):
    """Delete old wallpapers to save space."""
    wallpaper_files = list(directory.glob("*.jpg"))

    if len(wallpaper_files) > max_wallpapers:
        wallpaper_files.sort(key=os.path.getctime)
        
        for file_to_delete in wallpaper_files[:-max_wallpapers]:
            logging.info(f"Deleting old wallpaper file: {file_to_delete}")
            try:
                file_to_delete.unlink()
                logging.info(f"Successfully deleted: {file_to_delete}")
            except OSError as e:
                logging.error(f"Failed to delete old wallpaper {file_to_delete}: {e}")

    target_dir = directory / "projects" / "myprojects"
    if target_dir.exists() and target_dir.is_dir():
        wallpaper_dirs = [d for d in target_dir.iterdir() if d.is_dir()]
        if len(wallpaper_dirs) > max_wallpapers:
            wallpaper_dirs.sort(key=os.path.getctime)
            for dir_to_delete in wallpaper_dirs[:len(wallpaper_dirs) - max_wallpapers]:
                logging.info(f"Deleting old wallpaper directory: {dir_to_delete}")
                try:
                    for root, dirs, files in os.walk(dir_to_delete):
                        for file in files:
                            file_path = os.path.join(root, file)
                            os.chmod(file_path, 0o777)
                    shutil.rmtree(dir_to_delete)
                    logging.info(f"Successfully deleted: {dir_to_delete}")
                except PermissionError as e:
                    logging.error(f"PermissionError: Could not delete {dir_to_delete}. {e}")
                except Exception as e:
                    logging.error(f"Unexpected error while deleting {dir_to_delete}: {e}")

def update_wallpaper():
    """Function to update the wallpaper based on a random choice of sources."""
    config = load_config()  # Reload configuration
    save_location = Path(config['SAVE_LOCATION'])
    max_wallpapers = int(config['MAX_WALLPAPERS'])
    check_interval = int(config['CHECK_INTERVAL'])

    choices = ["unsplash", "pexels", "wallpaper_engine"]
    choice = random.choice(choices)

    if choice == "unsplash":
        logging.info("Selected Unsplash for wallpapers.")
        unsplash_wallpapers = fetch_unsplash_wallpapers(query="landscape", count=1)
        save_unsplash_wallpapers(unsplash_wallpapers, save_location / "unsplash_wallpapers")
        unsplash_wallpaper_path = get_latest_wallpaper(save_location / "unsplash_wallpapers")
        if unsplash_wallpaper_path:
            set_unsplash_wallpaper(unsplash_wallpaper_path)
            close_wallpaper_engine()
        cleanup_old_wallpapers(save_location / "unsplash_wallpapers", max_wallpapers)
    
    elif choice == "pexels":
        logging.info("Selected Pexels for wallpapers.")
        pexels_wallpapers = fetch_pexels_wallpapers(query="nature", count=1)
        save_pexels_wallpapers(pexels_wallpapers, save_location / "pexels_wallpapers")
        pexels_wallpaper_path = get_latest_wallpaper(save_location / "pexels_wallpapers")
        if pexels_wallpaper_path:
            set_pexels_wallpaper(pexels_wallpaper_path)
            close_wallpaper_engine()
        cleanup_old_wallpapers(save_location / "pexels_wallpapers", max_wallpapers)
    
    elif choice == "wallpaper_engine":
        logging.info("Selected Wallpaper Engine for wallpapers.")
        automate_wallpaper_update()
        time.sleep(2.5) # Wait for the wallpaper update to finish
            
        # Terminate DepotDownloadermod.exe before attempting to delete files
        terminate_depotdownloader()
        
        cleanup_old_wallpapers(save_location, max_wallpapers)

def schedule_wallpaper_update():
    """Function to schedule wallpaper updates at regular intervals."""
    while True:
        update_wallpaper()
        config = load_config()  # Reload configuration
        check_interval = int(config['CHECK_INTERVAL'])
        logging.info(f"Sleeping for {check_interval} seconds before the next update.")
        time.sleep(check_interval)

if __name__ == "__main__":
    wallpaper_thread = threading.Thread(target=schedule_wallpaper_update)
    wallpaper_thread.daemon = True
    wallpaper_thread.start()

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        logging.info("Program interrupted by the user. Exiting...")