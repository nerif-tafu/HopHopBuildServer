from flask import Flask, render_template
from flask_socketio import SocketIO, emit
import subprocess
import threading
import time
import os
from ..utils.env import get_screen_name

app = Flask(__name__)
app.config['SECRET_KEY'] = 'your-secret-key'
socketio = SocketIO(app)

def get_screen_output(screen_name):
    while True:
        try:
            output = subprocess.check_output([
                'screen', '-S', screen_name, '-X', 'hardcopy', '/tmp/screen_output.txt'
            ], stderr=subprocess.PIPE)
            
            with open('/tmp/screen_output.txt', 'r') as f:
                content = f.read()
            
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

def run_server():
    """Entry point for the web server"""
    import sys
    host = sys.argv[1] if len(sys.argv) > 1 else '0.0.0.0:5000'
    
    # Start monitoring thread
    screen_name = get_screen_name()
    thread = threading.Thread(target=get_screen_output, args=(screen_name,), daemon=True)
    thread.start()
    
    # Use talisker+gunicorn to run the server
    cmd = [
        'talisker.gunicorn.gevent',
        'hophop.webapp.app:app',
        f'--bind={host}',
        '--worker-class=gevent',
        f'--name=talisker-{os.uname().nodename}'
    ]
    os.execvp(cmd[0], cmd) 