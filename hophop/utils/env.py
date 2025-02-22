import os
from dotenv import load_dotenv

def load_env_files():
    """Load environment variables from .env and .env.local files"""
    load_dotenv('.env')
    if os.path.exists('.env.local'):
        load_dotenv('.env.local', override=True)

def get_env_int(key, default=None):
    """Get an environment variable as integer"""
    value = os.getenv(key, default)
    return int(value) if value is not None else None

def get_env_str(key, default=None):
    """Get an environment variable as string"""
    return os.getenv(key, default)

def get_screen_name():
    """Get the screen name from environment"""
    load_env_files()
    server_name = get_env_str('SERVER_NAME', 'HopHop Build server | Main')
    return server_name.lower().replace(" ", "_").replace("|", "").replace("\"", "").strip() 