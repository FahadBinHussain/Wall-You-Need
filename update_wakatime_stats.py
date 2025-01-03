import requests
import os
from datetime import datetime

# Your WakaTime API key
API_KEY = os.getenv('WAKATIME_API_KEY')
PROJECT_NAME = os.getenv('PROJECT_NAME')
README_PATH = 'README.md'

def get_cumulative_time(api_key, project_name):
    url = f'https://wakatime.com/api/v1/users/current/summaries'

    # Get the current date in YYYY-MM-DD format
    current_date = datetime.utcnow().strftime('%Y-%m-%d')

    params = {
        'api_key': api_key,
        'project': project_name,
        'start': '2024-12-31',  # Replace with your desired start date
        'end': current_date
    }
    response = requests.get(url, params=params)
    
    try:
        data = response.json()
    except ValueError:
        print("Error: Unable to parse JSON response.")
        print("Response content:", response.text)
        return None
    
    if 'cumulative_total' not in data:
        print("Error: 'cumulative_total' key not found in the response.")
        print("Response content:", data)
        return None
    
    total_text = data['cumulative_total']['text']
    return total_text

def update_readme(readme_path, total_text):
    with open(readme_path, 'r') as file:
        content = file.read()

    start_marker = '<!--START_SECTION:waka-->'
    end_marker = '<!--END_SECTION:waka-->'
    start_index = content.find(start_marker) + len(start_marker)
    end_index = content.find(end_marker)
    new_content = content[:start_index] + f'\nTotal Time Spent: {total_text}\n' + content[end_index:]

    with open(readme_path, 'w') as file:
        file.write(new_content)

if __name__ == '__main__':
    total_text = get_cumulative_time(API_KEY, PROJECT_NAME)
    if total_text:
        update_readme(README_PATH, total_text)