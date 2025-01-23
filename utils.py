# utils.py
import os
import json
from pathlib import Path
from dotenv import load_dotenv
import logging

def load_env_vars():
    """Load environment variables from .env file."""
    dotenv_path = os.path.join(os.path.dirname(__file__), '.env')
    load_dotenv(dotenv_path)

def load_config():
    """Load configuration from config.json file."""
    config_path = os.path.join(os.path.dirname(__file__), 'config.json')
    if not os.path.exists(config_path):
        logging.error("Configuration file does not exist.")
        return {}

    try:
        with open(config_path, 'r') as config_file:
            return json.load(config_file)
    except json.JSONDecodeError as e:
        logging.error(f"Invalid JSON in configuration file: {e}")
        return {}

def save_config(config):
    """Save configuration to config.json file."""
    config_path = os.path.join(os.path.dirname(__file__), 'config.json')
    with open(config_path, 'w') as config_file:
        json.dump(config, config_file, indent=4)

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