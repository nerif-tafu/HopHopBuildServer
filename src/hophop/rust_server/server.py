import os
import subprocess
import requests
import json
import tarfile
import shutil
import psutil
import signal
import sys
from pysteamcmdwrapper import SteamCMD, SteamCMDException
from dotenv import load_dotenv

def load_env_files():
    """Load environment variables from .env and .env.local files"""
    # Load default .env file
    load_dotenv('.env')
    
    # Load .env.local if it exists (overwriting any existing values)
    if os.path.exists('.env.local'):
        load_dotenv('.env.local', override=True)

def get_env_int(key, default=None):
    """Get an environment variable as integer"""
    value = os.getenv(key, default)
    return int(value) if value is not None else None

def get_env_str(key, default=None):
    """Get an environment variable as string"""
    return os.getenv(key, default)

# Load environment variables at module level
load_env_files()

# Get settings from environment
RUST_ID = get_env_int('RUST_ID', 258550)
REQUIRED_GB = get_env_int('REQUIRED_GB', 20)

# Get the git repo root directory
current_file = os.path.abspath(__file__)  # /path/to/HopHopBuildServer/src/hophop/rust_server/server.py
PATH_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(current_file))))  # /path/to/HopHopBuildServer

# All paths relative to git repo root
PATH_RUST_SERVER = os.path.join(PATH_ROOT, "rust_server")     # HopHopBuildServer/rust_server
PATH_STEAM_CMD = os.path.join(PATH_ROOT, "steam_cmd")         # HopHopBuildServer/steam_cmd
PATH_TMP = os.path.join(PATH_ROOT, "tmp")                     # HopHopBuildServer/tmp
PATH_SCRIPTS = os.path.join(PATH_ROOT, "src/hophop/rust_server/scripts")  # HopHopBuildServer/src/hophop/rust_server/scripts
PATH_RUST_PLUGINS = os.path.join(PATH_RUST_SERVER, "carbon", "plugins")   # HopHopBuildServer/rust_server/carbon/plugins

# Create runtime directories
print(f"\nSetting up server directories in: {PATH_ROOT}")
os.makedirs(PATH_RUST_SERVER, exist_ok=True)
os.makedirs(PATH_STEAM_CMD, exist_ok=True)
os.makedirs(PATH_TMP, exist_ok=True)

def check_disk_space():
    """Check if there's enough disk space available (at least 20GB)."""
    path = os.path.dirname(os.path.abspath(__file__))
    available_bytes = psutil.disk_usage(path).free
    available_gb = available_bytes / (1024**3)  # Convert to GB
    
    if available_gb < REQUIRED_GB:
        raise Exception(f"Not enough disk space! Need at least {REQUIRED_GB}GB, but only have {available_gb:.2f}GB available.")
    print(f"Disk space check passed: {available_gb:.2f}GB available")

def get_carbon_url(branch):
    """Get the appropriate Carbon download URL based on the branch"""
    base_url = "https://github.com/CarbonCommunity/Carbon/releases/download"
    
    if branch == 'staging':
        return f"{base_url}/rustbeta_staging_build/Carbon.Linux.Debug.tar.gz"
    elif branch == 'aux03':
        return f"{base_url}/rustbeta_aux03_build/Carbon.Linux.Debug.tar.gz"
    elif branch == 'aux02':
        return f"{base_url}/rustbeta_aux02_build/Carbon.Linux.Debug.tar.gz"
    elif branch == 'aux01':
        return f"{base_url}/rustbeta_aux01_build/Carbon.Linux.Debug.tar.gz"
    elif branch == 'edge':
        return f"{base_url}/edge_build/Carbon.Linux.Debug.tar.gz"
    elif branch == 'preview':
        return f"{base_url}/preview_build/Carbon.Linux.Debug.tar.gz"
    else:  # master/production
        return f"{base_url}/production_build/Carbon.Linux.Release.tar.gz"

def base_install():
    """Install and configure the Rust server"""
    # First check disk space
    check_disk_space()
    
    try:
        subprocess.run(["sudo", "add-apt-repository", "multiverse", "-y"])
        subprocess.run(["sudo", "dpkg", "--add-architecture", "i386"])
        subprocess.run(["sudo", "apt", "update"])
        
        # Accept Steam EULA and install steamcmd in a single shell command
        subprocess.run('echo "steamcmd steam/question select I AGREE" | sudo debconf-set-selections && sudo DEBIAN_FRONTEND=noninteractive apt install steamcmd -y', shell=True)
        print("SteamCMD installed successfully.")
    except Exception as e:
        print("Error occurred while installing SteamCMD:", e)

    # Update/Install Rust server.
    s = SteamCMD("steam_cmd")

    try:
        s.install()
    except SteamCMDException:
        print("Already installed, try to use the --force option to force installation")

    s.app_update(RUST_ID,PATH_RUST_SERVER,validate=True)

    # Get the branch from environment
    RUST_BRANCH = get_env_str('RUST_BRANCH', 'master')

    # Install Carbon modding framework with appropriate version
    try:
        download_url = get_carbon_url(RUST_BRANCH)
        response = requests.get(download_url)
        response.raise_for_status()

        with open(os.path.join(PATH_TMP, "carbon.tar.gz"), "wb") as f:
            f.write(response.content)

        # Extracting and installing Carbon
        print(f"Extracting and installing Carbon ({RUST_BRANCH} branch)")
        with tarfile.open(os.path.join(PATH_TMP, "carbon.tar.gz"), "r:gz") as tar_ref:
            tar_ref.extractall(PATH_RUST_SERVER)
    except Exception as e:
        print("Error occurred during Carbon update:", e)

    # Delete all scripts in PATH_RUST_PLUGINS
    print("Removing old Carbon plugins.")
    for item in os.listdir(PATH_RUST_PLUGINS):
        item_path = os.path.join(PATH_RUST_PLUGINS, item)
        try:
            if os.path.isdir(item_path):
                shutil.rmtree(item_path)
            else:
                os.remove(item_path)
        except Exception as e:
            print(f"Error occurred while deleting {item_path}: {e}")

    # Install all scripts from PATH_SCRIPTS.
    print("Installing new Carbon plugins.")
    for item in os.listdir(PATH_SCRIPTS):
        s = os.path.join(PATH_SCRIPTS, item)
        d = os.path.join(PATH_RUST_PLUGINS, item)
        try:
            if os.path.isdir(s):
                shutil.copytree(s, d, dirs_exist_ok=True)
            else:
                shutil.copy2(s, d)
        except Exception as e:
            print(f"Error occurred while copying {s} to {d}: {e}")

def start_rust_server():
    """Main entry point for the rust server"""
    try:
        # Load settings from environment
        SERVER_NAME = get_env_str('SERVER_NAME', 'HopHop Build server | Main')
        SERVER_MAP_SIZE = get_env_int('SERVER_MAP_SIZE', 4800)
        SERVER_MAP_SEED = get_env_int('SERVER_MAP_SEED', 12345)
        SERVER_PORT = get_env_int('SERVER_PORT', 28015)
        SERVER_QUERY = get_env_int('SERVER_QUERY', 28016)
        SERVER_RCON_PORT = get_env_int('SERVER_RCON_PORT', 28017)
        SERVER_RCON_PASS = get_env_str('SERVER_RCON_PASS', 'avoid-unelected-thee')
        SERVER_MAX_PLAYERS = get_env_int('SERVER_MAX_PLAYERS', 8)
        RUST_BRANCH = get_env_str('RUST_BRANCH', 'master')

        base_install()
        
        # Update/Install Rust server with appropriate branch
        s = SteamCMD("steam_cmd")
        try:
            s.install()
        except SteamCMDException:
            print("Already installed, try to use the --force option to force installation")

        # Set beta branch if not master
        beta_branch = None
        if RUST_BRANCH in ['staging', 'aux01', 'aux02', 'aux03', 'edge', 'preview']:
            beta_branch = RUST_BRANCH
        
        # Update app with specified branch
        print(f"Installing Rust server ({RUST_BRANCH} branch)")
        s.app_update(
            RUST_ID,
            PATH_RUST_SERVER,
            validate=True,
            beta=beta_branch
        )

        # Define the list of users
        users = [
            "ownerid 76561198183150138 \"Clayton (Rust)\"",
            "ownerid 76561198091394287 \"Demonic\"",
            "ownerid 76561198804286062 \"Ed\"",
            "ownerid 76561198056409776 \"Finn\"",
            "ownerid 76561198299291090 \"Gleb\"",
            "ownerid 76561198017536117 \"Gringo\"",
            "ownerid 76561198110905826 \"Jamie\"",
            "ownerid 76561198009503041 \"Kaas\"",
            "ownerid 76561198043994008 \"Kristian\"",
            "ownerid 76561198072387032 \"Maze\"",
            "ownerid 76561198398810414 \"Nora\"",
            "ownerid 76561197996896290 \"Padzor\"",
            "ownerid 76561198287027907 \"Pidge\"",
            "ownerid 76561198227557712 \"Razzey\"",
            "ownerid 76561197972768339 \"Robbin\"",
            "ownerid 76561198215723943 \"Tom\"",
            "ownerid 76561199003344794 \"Zapio\""
        ]

        file_path = os.path.join(PATH_RUST_SERVER, "server", "carbon", "cfg", "users.cfg")
        os.makedirs(os.path.dirname(file_path), exist_ok=True)

        with open(file_path, "w") as file:
            file.write("\n".join(users))

        # Changing directory
        os.chdir(PATH_RUST_SERVER)
        
        os.environ["TERM"] = "xterm"
        os.environ["DOORSTOP_ENABLED"] = "1"
        os.environ["DOORSTOP_TARGET_ASSEMBLY"] = os.path.join(PATH_RUST_SERVER, "carbon/managed/Carbon.Preloader.dll")
        os.environ["LD_PRELOAD"] = os.path.join(PATH_RUST_SERVER, "libdoorstop.so")
        os.environ["LD_LIBRARY_PATH"] = os.path.join(PATH_RUST_SERVER, "RustDedicated_Data", "Plugins", "x86_64")
        
        # Running RustDedicated server
        command = [
            os.path.join(PATH_RUST_SERVER, "RustDedicated"),
            "-batchmode",
            "+server.secure", "1",
            "+server.tickrate", "30",
            "+server.identity", "carbon",
            "+server.port", str(SERVER_PORT),
            "+server.queryport", str(SERVER_QUERY),
            "+rcon.port", str(SERVER_RCON_PORT),
            "+server.hostname", SERVER_NAME,
            "+server.seed", str(SERVER_MAP_SEED),
            "+server.worldsize", str(SERVER_MAP_SIZE),
            "+rcon.password", SERVER_RCON_PASS,
            "+rcon.web", "true",
            "+server.maxplayers", str(SERVER_MAX_PLAYERS),
            "+app.port", "1-"
        ]

        print("Starting Rust server...")
        
        # Set up signal handlers
        def handle_signal(signum, frame):
            print(f"\nReceived signal {signum}, shutting down...")
            if rust_process:
                rust_process.terminate()
                try:
                    rust_process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    rust_process.kill()
            sys.exit(0)

        signal.signal(signal.SIGTERM, handle_signal)
        signal.signal(signal.SIGINT, handle_signal)

        # Start the server process
        rust_process = subprocess.Popen(
            command,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            universal_newlines=True
        )

        print(f"Rust server started with PID: {rust_process.pid}")

        # Monitor the output
        while True:
            line = rust_process.stdout.readline()
            if not line and rust_process.poll() is not None:
                break
            if line:
                print(line.rstrip())
                sys.stdout.flush()

        # Check exit status
        exit_code = rust_process.wait()
        if exit_code != 0:
            print(f"Server exited with code: {exit_code}")
            sys.exit(exit_code)

    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    start_rust_server() 