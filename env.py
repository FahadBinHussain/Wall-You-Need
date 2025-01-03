from pathlib import Path
from dotenv import load_dotenv
import os
import random

# Load environment variables
load_dotenv()

# Environment variables
USERNAMES = os.getenv("USERNAMES", "").split(",")
PASSWORDS = os.getenv("PASSWORDS", "").split(",")
SAVE_LOCATION = Path(os.getenv("SAVE_LOCATION", ""))
COLLECTIONS_URL = random.choice(os.getenv("COLLECTIONS_URL", "").split(",") if os.getenv("COLLECTIONS_URL") else [""])
CHECK_INTERVAL = int(os.getenv("CHECK_INTERVAL", 3600))  # Default 1 hour
SCRAPE_INTERVAL = int(os.getenv("SCRAPE_INTERVAL", 86400))  # Default 24 hours
WALLPAPER_DOWNLOAD_LIMIT = int(os.getenv("WALLPAPER_DOWNLOAD_LIMIT", 3))
OUTPUT_FILE = Path(os.getenv("OUTPUT_FILE", "wallpapers.txt"))
COMMAND_TEMPLATE = "DepotdownloaderMod\\DepotDownloadermod.exe -app 431960 -pubfile {pubfileid} -verify-all -username {username} -password {password} -dir \"{directory}\" -validate"
MAX_WALLPAPERS = int(os.getenv("MAX_WALLPAPERS", 5))