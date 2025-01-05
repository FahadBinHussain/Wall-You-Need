# utils.py
import os
import json
from pathlib import Path
from dotenv import load_dotenv

def load_env_vars():
    """Load environment variables from .env file."""
    dotenv_path = os.path.join(os.path.dirname(__file__), '.env')
    load_dotenv(dotenv_path)

def load_config():
    """Load configuration from config.json file."""
    config_path = os.path.join(os.path.dirname(__file__), 'config.json')
    with open(config_path, 'r') as config_file:
        return json.load(config_file)

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
    print("Loaded configuration:", config)
    print("Loaded usernames:", os.getenv('USERNAMES'))