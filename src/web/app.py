from flask import Flask, render_template
from flask_socketio import SocketIO, emit
import subprocess
import threading
import time
import os
import argparse

app = Flask(__name__)
app.config['SECRET_KEY'] = 'your-secret-key'
socketio = SocketIO(app)

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
        
        time.sleep(1)  # Update every second

@app.route('/')
def index():
    return render_template('index.html')

@socketio.on('connect')
def handle_connect():
    print('Client connected')

@socketio.on('disconnect')
def handle_disconnect():
    print('Client disconnected')

def start_flask(screen_name):
    # Start the screen output monitoring thread
    thread = threading.Thread(target=get_screen_output, args=(screen_name,), daemon=True)
    thread.start()
    
    # Start Flask server
    socketio.run(app, host='0.0.0.0', port=5000, debug=False, use_reloader=False)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Start the Rust server web console')
    parser.add_argument('screen_name', help='Name of the screen session to monitor')
    parser.add_argument('--port', type=int, default=5000, help='Port to run the web server on (default: 5000)')
    parser.add_argument('--host', default='0.0.0.0', help='Host to run the web server on (default: 0.0.0.0)')
    
    args = parser.parse_args()
    
    start_flask(args.screen_name) 