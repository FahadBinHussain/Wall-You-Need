import os
import json
from pathlib import Path
from dotenv import load_dotenv
import logging
import threading
import sys

config_lock = threading.Lock()

def load_env_vars():
    """Load environment variables from .env file."""
    dotenv_path = os.path.join(os.path.dirname(__file__), '.env')
    load_dotenv(dotenv_path)

def load_config():
    with config_lock:
        try:
            # Determine the base path for the config file
            if hasattr(sys, '_MEIPASS'):
                # If running as a PyInstaller bundle
                base_path = sys._MEIPASS
            else:
                # If running as a plain Python script
                base_path = os.path.dirname(os.path.abspath(__file__))

            config_path = os.path.join(base_path, 'config.json')
            with open(config_path, 'r') as f:
                config = json.load(f)
            logging.info(f"Configuration loaded successfully: {config}")
        except FileNotFoundError:
            logging.error("Configuration file not found.")
            config = {}
        except json.JSONDecodeError as e:
            logging.error(f"Error decoding JSON: {e}")
            config = {}

        if 'SAVE_LOCATION' not in config:
            logging.error("'SAVE_LOCATION' key is missing in the configuration.")
        else:
            logging.info(f"'SAVE_LOCATION' found: {config['SAVE_LOCATION']}")

        return config

def save_config(config):
    with config_lock:
        try:
            with open('config.json', 'w') as f:
                json.dump(config, f, indent=4)
            logging.info("Configuration saved successfully.")
        except Exception as e:
            logging.error(f"Error saving configuration: {e}")

# Load environment variables at the start of the script
load_env_vars()

# Example usage:
if __name__ == "__main__":
    config = load_config()
    if config:
        print("Loaded configuration:", config)
    else:
        print("Failed to load configuration.")
    print("Loaded usernames:", os.getenv('USERNAMES'))