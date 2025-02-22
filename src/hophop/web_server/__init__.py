"""
Web console module.
Provides a web interface to monitor and control the Rust server.
"""

from .server import app, run_server

__all__ = ['app', 'run_server']
