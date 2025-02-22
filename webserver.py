#!/usr/bin/env python3
import os
import sys
import signal
import subprocess
from server.app import start_flask

def get_screen_name():
    """Get the screen name from environment or fallback to default"""
    from dotenv import load_dotenv
    
    # Load environment variables
    load_dotenv('.env')
    if os.path.exists('.env.local'):
        load_dotenv('.env.local', override=True)
    
    server_name = os.getenv('SERVER_NAME', 'HopHop Build server | Main')
    return server_name.lower().replace(" ", "_").replace("|", "").replace("\"", "").strip()

def signal_handler(sig, frame):
    print('\nShutting down web server...')
    sys.exit(0)

if __name__ == "__main__":
    # Setup signal handler for graceful shutdown
    signal.signal(signal.SIGINT, signal_handler)
    
    screen_name = get_screen_name()
    print(f"Starting web server for screen session: {screen_name}")
    print("Press Ctrl+C to stop the server")
    
    start_flask(screen_name) 