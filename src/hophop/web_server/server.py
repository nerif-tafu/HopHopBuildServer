# Monkey patch must happen before other imports
from gevent import monkey
monkey.patch_all()

from flask import Flask, render_template
from flask_socketio import SocketIO, emit
import subprocess
import threading
import time
import os
import socket
from typing import Optional
from .rcon_client import RustRCON
import json
from functools import partial

app = Flask(__name__)
app.config['SECRET_KEY'] = os.getenv('FLASK_SECRET_KEY', 'your-secret-key')
socketio = SocketIO(app)

# Add these variables at the top with other imports
RCON_HOST = os.getenv('RCON_HOST', 'localhost')
RCON_PORT = int(os.getenv('SERVER_RCON_PORT', '28017'))  # Use SERVER_RCON_PORT from .env
RCON_PASSWORD = os.getenv('SERVER_RCON_PASS', 'avoid-unelected-thee')  # Use SERVER_RCON_PASS from .env

# Add this after app initialization
rcon_client = RustRCON(RCON_HOST, RCON_PORT, RCON_PASSWORD)

def is_port_in_use(port: int) -> bool:
    """Check if a port is already in use"""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        try:
            s.bind(('', port))
            return False
        except socket.error:
            return True

def find_available_port(start_port: int, max_attempts: int = 100) -> int:
    """Find the next available port starting from start_port"""
    port = start_port
    while port < (start_port + max_attempts):
        if not is_port_in_use(port):
            return port
        port += 1
    raise RuntimeError(f"Could not find an available port after {max_attempts} attempts")

def print_server_info(host: str, port: int, workers: int):
    """Print server information in a nice format"""
    server_name = os.getenv('SERVER_NAME', 'HopHop Build server | Main')
    screen_name = server_name.lower().replace(" ", "_").replace("|", "").replace("\"", "").strip()
    
    print("\n" + "="*50)
    print("HopHop Web Console Server")
    print("="*50)
    print(f"\n🌐 Server running at:")
    print(f"   • Local:   http://localhost:{port}")
    print(f"   • Network: http://{host}:{port}")
    print("\n📊 Server Status:")
    print(f"   • Workers:  {workers} (gevent)")
    print(f"   • Screen:   {screen_name}")
    print("\n💡 Tips:")
    print("   • Press Ctrl+C to stop the server")
    print("   • The console will auto-refresh every second")
    print("="*50 + "\n")

def get_screen_output(screen_name):
    """Get and emit screen output to connected clients"""
    last_message = ""
    check_interval = 1  # Check every second
    
    def handle_console(response):
        nonlocal last_message
        try:
            if response:
                messages = json.loads(response)
                # Combine all messages into one string, preserving newlines
                content = "\n".join(msg.get('Message', '') for msg in messages if msg.get('Message'))
                if content and content != last_message:
                    socketio.emit('screen_output', {'data': content})
                    last_message = content
        except Exception as e:
            print(f"Error handling console output: {e}")
    
    while True:
        try:
            if rcon_client.connected:
                # Use console.tail to get the last 128 lines of console output
                rcon_client.send_command('console.tail 128', handle_console)
            else:
                message = 'Waiting for server to start...'
                if message != last_message:
                    socketio.emit('screen_output', {'data': message})
                    last_message = message
        except Exception as e:
            print(f"Error in get_console_output: {e}")
        
        time.sleep(check_interval)

@app.route('/')
def index():
    return render_template('index.html')

@socketio.on('connect')
def handle_connect():
    """Handle client connection"""
    print('Client connected')
    
    # Get initial console history
    if rcon_client.connected:
        rcon_client.send_command('console.tail 128', lambda response: 
            socketio.emit('screen_output', {'data': '\n'.join(
                msg.get('Message', '') 
                for msg in json.loads(response) 
                if msg.get('Message')
            )}) if response else None
        )
    
    # Get initial status
    get_server_status()

@socketio.on('disconnect')
def handle_disconnect():
    print('Client disconnected')

def update_server_status():
    """Update and emit server status"""
    status_data = {
        'status': 'online' if rcon_client.connected else 'offline',
        'players': 'Unknown',
        'max_players': 'Unknown',
        'fps': 'Unknown',
        'entities': 'Unknown',
        'raw': ''
    }
    socketio.emit('server_status', {'data': status_data})

def get_server_status():
    """Get server status via RCON"""
    if not rcon_client.connected:
        update_server_status()
        return
        
    status_data = {
        'status': 'online',
        'players': 'Unknown',
        'max_players': 'Unknown',
        'fps': 'Unknown',
        'entities': 'Unknown',
        'raw': ''
    }
    
    def handle_serverinfo(response):
        try:
            if response:
                data = json.loads(response)
                status_data['players'] = data.get('Players', 'Unknown')
                status_data['max_players'] = data.get('MaxPlayers', 'Unknown')
                status_data['fps'] = data.get('Framerate', 'Unknown')
                status_data['entities'] = data.get('EntityCount', 'Unknown')
                status_data['raw'] = json.dumps(data, indent=2)  # Pretty print the raw data
            socketio.emit('server_status', {'data': status_data})
        except Exception as e:
            print(f"Error handling serverinfo: {e}")
            socketio.emit('server_status', {'data': status_data})
    
    rcon_client.send_command('serverinfo', handle_serverinfo)

@socketio.on('request_status')
def handle_status_request():
    """Handle client requests for server status"""
    get_server_status()

def init_rcon():
    """Initialize RCON connection in a separate thread"""
    def rcon_thread():
        try:
            # Set up connection state change callback
            rcon_client.on_state_change = update_server_status
            rcon_client.connect()
        except Exception as e:
            print(f"Failed to connect to RCON: {e}")
            update_server_status()
    
    thread = threading.Thread(target=rcon_thread, daemon=True)
    thread.start()
    return thread

def run_server(bind: Optional[str] = "0.0.0.0:5000", 
              workers: int = 1,
              timeout: int = 30):
    """Main entry point for the web server"""
    host, port_str = bind.split(':')
    initial_port = int(port_str)
    
    try:
        port = find_available_port(initial_port)
        if port != initial_port:
            print(f"Port {initial_port} was in use, using port {port} instead")
    except RuntimeError as e:
        print(f"Error: {e}")
        exit(1)
    
    # Get server name and generate screen name the same way rust server does
    server_name = os.getenv('SERVER_NAME', 'HopHop Build server | Main')
    screen_name = server_name.lower().replace(" ", "_").replace("|", "").replace("\"", "").strip()
    
    # Start the console output monitoring thread (renamed from screen_thread)
    console_thread = threading.Thread(target=get_screen_output, args=(screen_name,), daemon=True)
    console_thread.start()
    
    # Initialize RCON in a separate thread
    rcon_thread = init_rcon()
    
    # Start periodic status updates
    def status_updater():
        while True:
            if rcon_client.connected:
                get_server_status()
            time.sleep(30)  # Update every 30 seconds
    
    status_thread = threading.Thread(target=status_updater, daemon=True)
    status_thread.start()
    
    # Print server information
    print_server_info(host, port, workers)
    
    # Run with Flask-SocketIO's server instead of Gunicorn
    socketio.run(app, host=host, port=port, debug=True, allow_unsafe_werkzeug=True)

if __name__ == "__main__":
    run_server() 