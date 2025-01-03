import time
import requests
import subprocess
import json
from pathlib import Path
from bs4 import BeautifulSoup
import random
import logging
import psutil
from env import USERNAMES, PASSWORDS, SAVE_LOCATION, COLLECTIONS_URL, CHECK_INTERVAL, SCRAPE_INTERVAL, WALLPAPER_DOWNLOAD_LIMIT, OUTPUT_FILE, COMMAND_TEMPLATE, MAX_WALLPAPERS

# Setup logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Wallpaper Engine related functions

def fetch_page_content(url):
    """Fetch a webpage's content."""
    try:
        response = requests.get(url)
        response.raise_for_status()
        return response.text
    except requests.RequestException as e:
        logging.warning(f"Failed to fetch {url}: {e}")
        return None

def get_random_credential_pair():
    """Select a random username-password pair."""
    if not USERNAMES or not PASSWORDS:
        logging.error("Usernames or passwords are not set in environment variables.")
        return None, None
    
    index = random.randint(0, len(USERNAMES) - 1)
    return USERNAMES[index], PASSWORDS[index]

def log_downloaded_wallpaper(pubfileid):
    """Log downloaded wallpaper metadata."""
    history_file = SAVE_LOCATION / "wallpaper_history.json"
    try:
        history = {}
        if history_file.exists():
            with history_file.open("r") as f:
                history = json.load(f)
        history[pubfileid] = {
            "timestamp": time.time(),
            "path": str(SAVE_LOCATION / "projects" / "myprojects" / pubfileid)
        }
        with history_file.open("w") as f:
            json.dump(history, f, indent=4)
    except (json.JSONDecodeError, IOError) as e:
        logging.error(f"Error logging wallpaper: {e}")

def set_downloaded_wallpaper(pubfileid):
    """Set the wallpaper using the provided command."""
    wallpaper_dir = SAVE_LOCATION / "projects" / "myprojects" / pubfileid
    scene_file = wallpaper_dir / "scene.pkg"
    mp4_file = next(wallpaper_dir.glob("*.mp4"), None)

    if scene_file.exists():
        logging.info(f"Setting wallpaper using scene.pkg for {pubfileid}")
        command = f'"{SAVE_LOCATION}\\wallpaper64.exe" -control openWallpaper -file "{scene_file}" play'
        try:
            subprocess.Popen(command, shell=True)
            logging.info(f"Successfully set wallpaper with scene.pkg for {pubfileid}")
        except subprocess.CalledProcessError as e:
            logging.error(f"Failed to set wallpaper with scene.pkg for {pubfileid}: {e}")
            return False
    elif mp4_file:
        logging.info(f"Setting wallpaper using {mp4_file} for {pubfileid}")
        command = f'"{SAVE_LOCATION}\\wallpaper64.exe" -control openWallpaper -file "{mp4_file}" play'
        try:
            subprocess.Popen(command, shell=True)
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
        return (time.time() - last_scrape) > SCRAPE_INTERVAL
    except (FileNotFoundError, OSError):
        return True

def update_last_scrape_time():
    """Update the last scrape timestamp."""
    with open("last_scrape_time.txt", "w") as f:
        f.write(str(time.time()))

def download_random_wallpapers():
    """Download random wallpapers using the saved links."""
    try:
        with OUTPUT_FILE.open("r", encoding="utf-8") as f:
            wallpaper_links = [line.strip() for line in f if line.startswith("https://")]
    except FileNotFoundError:
        logging.warning("No wallpaper links file found.")
        return

    if len(wallpaper_links) < WALLPAPER_DOWNLOAD_LIMIT:
        logging.warning("Not enough wallpapers to download.")
        return

    selected_links = random.sample(wallpaper_links, WALLPAPER_DOWNLOAD_LIMIT)
    for link in selected_links:
        pubfileid = link.split("id=")[1]
        logging.info(f"Extracted pubfileid: {pubfileid} from link: {link}")
        username, password = get_random_credential_pair()
        if not username or not password:
            logging.error("Username or password is missing. Skipping download.")
            continue

        # Create the directory for this wallpaper
        directory = SAVE_LOCATION / "projects" / "myprojects" / pubfileid
        directory.mkdir(parents=True, exist_ok=True)

        command = COMMAND_TEMPLATE.format(pubfileid=pubfileid, username=username, password=password, directory=directory)
        logging.info(f"Downloading wallpaper with ID {pubfileid} using {username}...")
        try:
            subprocess.run(command, shell=True, check=True)
            log_downloaded_wallpaper(pubfileid)
            if not set_downloaded_wallpaper(pubfileid):
                logging.error(f"Failed to set wallpaper with ID {pubfileid}")
        except subprocess.CalledProcessError as e:
            logging.error(f"Failed to download wallpaper with ID {pubfileid}: {e}")

def clean_and_filter_wallpaper_links(links):
    """Filter and clean wallpaper links to ensure they match the required format."""
    return [link.split("&")[0] for link in links if "steamcommunity.com/sharedfiles/filedetails/?id=" in link]

def scrape_wallpapers():
    """Scrape wallpapers and save unique links."""
    random_page = random.randint(1, 1000)
    collections_url = COLLECTIONS_URL.format(page=random_page)
    logging.info(f"Fetching wallpaper links from page {random_page}...")

    wallpapers_page = fetch_page_content(collections_url)
    if (wallpapers_page):
        logging.info("Parsing wallpapers...")
        soup = BeautifulSoup(wallpapers_page, 'html.parser')
        raw_links = [a['href'] for a in soup.select('div.workshopItem a') if a.get('href')]

        wallpaper_links = clean_and_filter_wallpaper_links(raw_links)

        if wallpaper_links:
            unique_links = set(wallpaper_links)
            logging.info(f"Found {len(unique_links)} unique wallpapers on page {random_page}.")
            
            with OUTPUT_FILE.open("w", encoding="utf-8") as f:
                for link in unique_links:
                    f.write(link + "\n")
            logging.info(f"Saved {len(unique_links)} unique wallpapers from page {random_page}.")

    update_last_scrape_time()

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
        scrape_wallpapers()
    download_random_wallpapers()
    logging.info("Completed automated wallpaper update.")