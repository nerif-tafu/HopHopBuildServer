import websocket
import json
import threading
import time
from typing import Optional, Callable

class RustRCON:
    def __init__(self, host: str, port: int, password: str):
        self.host = host
        self.port = port
        self.password = password
        self.ws: Optional[websocket.WebSocketApp] = None
        self.connected = False
        self.message_id = 0
        self.callbacks = {}
        self.reconnect_delay = 30  # Initial delay of 30 seconds
        self.max_reconnect_delay = 300  # Maximum delay of 5 minutes
        self.should_reconnect = True
        self.last_attempt = 0
        websocket.enableTrace(False)  # Disable verbose logging
        self.on_state_change = None  # Callback for connection state changes
        
    def connect(self):
        """Connect to the Rust RCON WebSocket"""
        while self.should_reconnect:
            current_time = time.time()
            # Ensure we wait at least reconnect_delay seconds between attempts
            if current_time - self.last_attempt < self.reconnect_delay:
                time.sleep(1)
                continue
                
            self.last_attempt = current_time
            try:
                if self.ws:
                    self.ws.close()
                
                self.ws = websocket.WebSocketApp(
                    f"ws://{self.host}:{self.port}/{self.password}",
                    on_open=self._on_open,
                    on_message=self._on_message,
                    on_error=self._on_error,
                    on_close=self._on_close
                )
                
                # Start WebSocket connection in a separate thread
                self.ws_thread = threading.Thread(target=self.ws.run_forever)
                self.ws_thread.daemon = True
                self.ws_thread.start()
                
                # Wait for connection or error
                timeout = time.time() + 10  # 10 second timeout
                while not self.connected and time.time() < timeout:
                    time.sleep(0.5)  # Increased sleep time
                
                if self.connected:
                    self.reconnect_delay = 30  # Reset delay on successful connection
                    break
                
            except Exception as e:
                print(f"Failed to connect to RCON: {e}")
            
            # If we get here, connection failed
            print(f"Will retry RCON connection in {self.reconnect_delay} seconds")
            time.sleep(self.reconnect_delay)
            # Increase delay for next attempt, up to max_reconnect_delay
            self.reconnect_delay = min(self.reconnect_delay * 2, self.max_reconnect_delay)
    
    def _on_open(self, ws):
        """Called when connection is established"""
        self.connected = True
        if self.on_state_change:
            self.on_state_change()
    
    def _on_message(self, ws, message):
        """Handle incoming messages"""
        try:
            data = json.loads(message)
            if 'Message' in data and 'Identifier' in data:
                callback = self.callbacks.get(data['Identifier'])
                if callback:
                    callback(data['Message'])
                    del self.callbacks[data['Identifier']]
        except json.JSONDecodeError:
            print(f"Invalid JSON received: {message}")
    
    def _on_error(self, ws, error):
        """Handle WebSocket errors"""
        error_str = str(error)
        print(f"RCON WebSocket error: {error_str}")
        
        # Only handle fatal errors that require reconnection
        if any(err in error_str for err in [
            "Connection refused",
            "Connection reset by peer",
            "Connection timed out",
            "Name or service not known"
        ]):
            print("Server appears to be offline")
            was_connected = self.connected
            self.connected = False
            if was_connected and self.on_state_change:
                self.on_state_change()
    
    def _on_close(self, ws, close_status_code, close_msg):
        """Handle WebSocket connection close"""
        was_connected = self.connected
        self.connected = False
        
        # Only notify of state change if we were previously connected
        if was_connected:
            if self.on_state_change:
                self.on_state_change()
        
        # Only attempt reconnection if:
        # 1. We should reconnect
        # 2. We were previously connected (avoid reconnect loops)
        # 3. The close wasn't initiated by us (close_status_code would be 1000)
        if (self.should_reconnect and 
            was_connected and 
            close_status_code != 1000):
            print(f"Will retry RCON connection in {self.reconnect_delay} seconds")
            threading.Thread(target=self.connect, daemon=True).start()
    
    def send_command(self, command: str, callback: Callable[[str], None] = None):
        """Send a command to the server"""
        if not self.connected or not self.ws:
            print("Not connected to RCON - command not sent")
            if callback:
                callback("")  # Call callback with empty response
            return
        
        try:
            self.message_id += 1
            message = {
                "Identifier": self.message_id,
                "Message": command,
                "Name": "WebRcon"
            }
            
            if callback:
                self.callbacks[self.message_id] = callback
            
            self.ws.send(json.dumps(message))
            return self.message_id
        except Exception as e:
            print(f"Error sending command: {e}")
            if callback:
                callback("")
    
    def disconnect(self):
        """Cleanly disconnect from the RCON server"""
        self.should_reconnect = False
        if self.ws:
            self.ws.close() 