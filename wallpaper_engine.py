import time
import requests
import subprocess
import json
from pathlib import Path
from bs4 import BeautifulSoup
import random
import logging
import psutil
from dotenv import load_dotenv
import os
from utils import load_env_vars, load_config, save_config  # Import utility functions

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Load environment variables
load_env_vars()

# Load configuration
config = load_config()

# Wallpaper Engine related functions

def printlog(log):
    """Log messages to the console."""
    print(log)
    logging.info(log)

def fetch_page_content(url):
    """Fetch a webpage's content."""
    try:
        config = load_config()
        response = requests.get(url)
        response.raise_for_status()
        return response.text
    except requests.RequestException as e:
        logging.warning(f"Failed to fetch {url}: {e}")
        return None

def get_random_credential_pair():
    """Select a random username-password pair."""
    usernames = os.getenv('USERNAMES').split(',')
    passwords = os.getenv('PASSWORDS').split(',')
    
    if not usernames or not passwords:
        logging.error("Usernames or passwords are not set in environment variables.")
        return None, None
    
    index = random.randint(0, len(usernames) - 1)
    return usernames[index], passwords[index]

def log_downloaded_wallpaper(pubfileid):
    """Log downloaded wallpaper metadata."""
    save_location = Path(config['SAVE_LOCATION'])
    history_file = save_location / "wallpaper_history.json"
    try:
        history = {}
        if history_file.exists():
            with history_file.open("r") as f:
                history = json.load(f)
        history[pubfileid] = {
            "timestamp": time.time(),
            "path": str(save_location / "projects" / "myprojects" / pubfileid)
        }
        with history_file.open("w") as f:
            json.dump(history, f, indent=4)
    except (json.JSONDecodeError, IOError) as e:
        logging.error(f"Error logging wallpaper: {e}")

def set_downloaded_wallpaper(pubfileid):
    """Set the wallpaper using the provided command."""
    save_location = Path(config['SAVE_LOCATION'])
    wallpaper_dir = save_location / "projects" / "myprojects" / pubfileid
    scene_file = wallpaper_dir / "scene.pkg"
    mp4_file = next(wallpaper_dir.glob("*.mp4"), None)

    if scene_file.exists():
        logging.info(f"Setting wallpaper using scene.pkg for {pubfileid}")
        command = f'"{save_location}\\wallpaper64.exe" -control openWallpaper -file "{scene_file}" play'
        try:
            subprocess.Popen(command, shell=True)
            time.sleep(2.5)  # Wait for the wallpaper to load
            logging.info(f"Successfully set wallpaper with scene.pkg for {pubfileid}")
        except subprocess.CalledProcessError as e:
            logging.error(f"Failed to set wallpaper with scene.pkg for {pubfileid}: {e}")
            return False
    elif mp4_file:
        logging.info(f"Setting wallpaper using {mp4_file} for {pubfileid}")
        command = f'"{save_location}\\wallpaper64.exe" -control openWallpaper -file "{mp4_file}" play'
        try:
            subprocess.Popen(command, shell=True)
            time.sleep(2.5)  # Wait for the wallpaper to load
            logging.info(f"Successfully set wallpaper with {mp4_file} for {pubfileid}")
        except subprocess.CalledProcessError as e:
            logging.error(f"Failed to set wallpaper with {mp4_file} for {pubfileid}: {e}")
            return False
    else:
        logging.warning(f"No valid wallpaper file (scene.pkg or .mp4) found for {pubfileid}")
        return False

    # Log the wallpaper change
    log_downloaded_wallpaper(pubfileid)
    return True

def should_perform_scrape():
    """Determine if scraping should be performed based on interval."""
    try:
        last_scrape = Path("last_scrape_time.txt").stat().st_mtime
        return (time.time() - last_scrape) > int(config['SCRAPE_INTERVAL'])
    except (FileNotFoundError, OSError):
        return True

def update_last_scrape_time():
    """Update the last scrape timestamp."""
    with open("last_scrape_time.txt", "w") as f:
        f.write(str(time.time()))

def clean_and_filter_wallpaper_links(links):
    """Filter and clean wallpaper links to ensure they match the required format."""
    filtered_links = [link.split("&")[0] for link in links if "steamcommunity.com/sharedfiles/filedetails/?id=" in link]
    logging.info(f"Filtered {len(filtered_links)} valid wallpaper links.")
    return filtered_links

def scrape_wallpapers():
    """Scrape wallpapers and return unique links."""
    random_page = random.randint(1, 1000)
    config = load_config()
    collections_url = config['COLLECTIONS_URL'].format(page=random_page)
    logging.info(f"Fetching wallpaper links from page {random_page}...")

    wallpapers_page = fetch_page_content(collections_url)
    if wallpapers_page:
        logging.info("Parsing wallpapers...")
        soup = BeautifulSoup(wallpapers_page, 'html.parser')
        raw_links = [a['href'] for a in soup.select('div.workshopItem a') if a.get('href')]

        wallpaper_links = clean_and_filter_wallpaper_links(raw_links)
        unique_links = set(wallpaper_links)
        logging.info(f"Found {len(unique_links)} unique wallpapers on page {random_page}.")
        return list(unique_links)
    else:
        logging.warning(f"Failed to fetch or parse page {random_page}.")
    return []

def download_random_wallpapers(wallpaper_links):
    """Download random wallpapers using the provided links."""
    wallpaper_download_limit = int(config['WALLPAPER_DOWNLOAD_LIMIT'])
    if len(wallpaper_links) < wallpaper_download_limit:
        logging.warning("Not enough wallpapers to download.")
        return

    selected_links = random.sample(wallpaper_links, wallpaper_download_limit)
    for link in selected_links:
        pubfileid = link.split("id=")[1]
        logging.info(f"Extracted pubfileid: {pubfileid} from link: {link}")
        username, password = get_random_credential_pair()
        if not username or not password:
            logging.error("Username or password is missing. Skipping download.")
            continue

        # Create the directory for this wallpaper
        save_location = Path(config['SAVE_LOCATION'])
        directory = save_location / "projects" / "myprojects" / pubfileid
        directory.mkdir(parents=True, exist_ok=True)

        command_template = "DepotdownloaderMod\\DepotDownloadermod.exe -app 431960 -pubfile {pubfileid} -verify-all -username {username} -password {password} -dir \"{directory}\""
        command = command_template.format(pubfileid=pubfileid, username=username, password=password, directory=directory)
        logging.info(f"Downloading wallpaper with ID {pubfileid} using {username}...")

        try:
            # Run the command and capture output
            process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
            for line in process.stdout:
                printlog(line)
            process.stdout.close()
            process.wait()

            if process.returncode != 0:
                logging.error(f"Command failed with return code {process.returncode}")
                return False

            log_downloaded_wallpaper(pubfileid)
            if not set_downloaded_wallpaper(pubfileid):
                logging.error(f"Failed to set wallpaper with ID {pubfileid}")
        except subprocess.CalledProcessError as e:
            logging.error(f"Failed to download wallpaper with ID {pubfileid}: {e}")

def close_wallpaper_engine():
    """Close Wallpaper Engine if it is running."""
    for process in psutil.process_iter(attrs=["name"]):
        if process.info["name"] == "wallpaper32.exe" or process.info["name"] == "wallpaper64.exe":
            logging.info(f"Terminating {process.info['name']} (PID: {process.pid})")
            process.terminate()
            process.wait()  # Wait for the process to be terminated
            logging.info(f"{process.info['name']} terminated.")
            return

def automate_wallpaper_update():
    """Automate the process of scraping, downloading, and setting wallpapers."""
    logging.info("Starting automated wallpaper update.")
    if should_perform_scrape():
        wallpaper_links = scrape_wallpapers()
        update_last_scrape_time()
    else:
        wallpaper_links = []

    if wallpaper_links:
        download_random_wallpapers(wallpaper_links)
    else:
        logging.warning("No new wallpapers to download.")
    logging.info("Completed automated wallpaper update.")

# Example usage
# if __name__ == "__main__":
#     automate_wallpaper_update()