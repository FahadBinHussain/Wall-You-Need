import logging
import random
from pathlib import Path
import time
import threading
from dotenv import load_dotenv
from wallpaper_engine import automate_wallpaper_update, close_wallpaper_engine
from unsplash import fetch_unsplash_wallpapers, save_wallpapers as save_unsplash_wallpapers, set_wallpaper as set_unsplash_wallpaper
from pexels import fetch_pexels_wallpapers, save_wallpapers as save_pexels_wallpapers, set_wallpaper as set_pexels_wallpaper
from utils import load_env_vars, load_config  # Import utility functions
from wallpaper_utils import get_latest_wallpaper, terminate_depotdownloader, cleanup_old_wallpapers
from registry_utils import set_wallpaper_style, set_lock_screen_wallpaper, set_lock_screen_wallpaper_style

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Load environment variables
load_env_vars()

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
            set_wallpaper_style()  # Set desktop wallpaper style to 'fit'
            set_lock_screen_wallpaper(unsplash_wallpaper_path)  # Set lock screen wallpaper
            set_lock_screen_wallpaper_style()  # Set lock screen wallpaper style to 'fit'
            close_wallpaper_engine()
        cleanup_old_wallpapers(save_location / "unsplash_wallpapers", max_wallpapers)
    
    elif choice == "pexels":
        logging.info("Selected Pexels for wallpapers.")
        pexels_wallpapers = fetch_pexels_wallpapers(query="nature", count=1)
        save_pexels_wallpapers(pexels_wallpapers, save_location / "pexels_wallpapers")
        pexels_wallpaper_path = get_latest_wallpaper(save_location / "pexels_wallpapers")
        if pexels_wallpaper_path:
            set_pexels_wallpaper(pexels_wallpaper_path)
            set_wallpaper_style()  # Set desktop wallpaper style to 'fit'
            set_lock_screen_wallpaper(pexels_wallpaper_path)  # Set lock screen wallpaper
            # set_lock_screen_wallpaper_style()  # Set lock screen wallpaper style to 'fit'
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