from flask import Flask, render_template
from flask_socketio import SocketIO, emit
import subprocess
import threading
import time
import os
import socket
from typing import Optional
from gunicorn.app.base import BaseApplication

app = Flask(__name__)
app.config['SECRET_KEY'] = os.getenv('FLASK_SECRET_KEY', 'your-secret-key')
socketio = SocketIO(app)

class GunicornApp(BaseApplication):
    def __init__(self, app, options=None):
        self.options = options or {}
        self.application = app
        super().__init__()

    def load_config(self):
        for key, value in self.options.items():
            self.cfg.set(key.lower(), value)

    def load(self):
        return self.application

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
    print("\n" + "="*50)
    print("HopHop Web Console Server")
    print("="*50)
    print(f"\n🌐 Server running at:")
    print(f"   • Local:   http://localhost:{port}")
    print(f"   • Network: http://{host}:{port}")
    print("\n📊 Server Status:")
    print(f"   • Workers:  {workers} (gevent)")
    print(f"   • Screen:   {os.getenv('RUST_SCREEN_NAME', 'rust_server')}")
    print("\n💡 Tips:")
    print("   • Press Ctrl+C to stop the server")
    print("   • The console will auto-refresh every second")
    print("="*50 + "\n")

def get_screen_output(screen_name):
    while True:
        try:
            # Get the last 50 lines from the screen session
            output = subprocess.check_output([
                'screen', '-S', screen_name, '-X', 'hardcopy', '/tmp/screen_output.txt'
            ], stderr=subprocess.PIPE)
            
            with open('/tmp/screen_output.txt', 'r') as f:
                content = f.read()
            
            # Emit the content through websocket
            socketio.emit('screen_output', {'data': content})
            
        except subprocess.CalledProcessError:
            socketio.emit('screen_output', {'data': 'Server not running...'})
        
        time.sleep(1)

@app.route('/')
def index():
    return render_template('index.html')

@socketio.on('connect')
def handle_connect():
    print('Client connected')

@socketio.on('disconnect')
def handle_disconnect():
    print('Client disconnected')

def run_server(bind: Optional[str] = "0.0.0.0:5000", 
              workers: int = 4,
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
    
    # Start the screen output monitoring thread
    screen_name = os.getenv('RUST_SCREEN_NAME', 'rust_server')
    thread = threading.Thread(target=get_screen_output, args=(screen_name,), daemon=True)
    thread.start()
    
    # Print server information
    print_server_info(host, port, workers)
    
    # Configure Gunicorn
    options = {
        'bind': f'{host}:{port}',
        'workers': workers,
        'worker_class': 'gevent',
        'timeout': timeout,
        'preload_app': True,
        'accesslog': '-',  # Log to stdout
        'errorlog': '-',   # Log to stdout
    }
    
    # Start Gunicorn server
    GunicornApp(app, options).run()

if __name__ == "__main__":
    run_server() 