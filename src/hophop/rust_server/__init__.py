"""
Rust server management module.
Handles installation, configuration and running of the Rust game server.
"""

from .server import start_rust_server, base_install

__all__ = ['start_rust_server', 'base_install']
