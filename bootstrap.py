import os
import subprocess
import requests
import json
import tarfile
from pysteamcmdwrapper import SteamCMD, SteamCMDException

# Variable defs.
RUST_ID = 258550

PATH_ROOT = os.path.realpath(os.path.dirname(__file__))
PATH_RUST_SERVER = os.path.join(PATH_ROOT,"rust_server")
PATH_STEAM_CMD = os.path.join(PATH_ROOT,"steam_cmd")
PATH_TMP = os.path.join(PATH_ROOT,"tmp")

os.makedirs(PATH_RUST_SERVER, exist_ok=True)
os.makedirs(PATH_STEAM_CMD, exist_ok=True)
os.makedirs(PATH_TMP, exist_ok=True)

def base_install():
    try:
        subprocess.run(["sudo", "apt", "install", "steamcmd", "-y"])
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

    try:
        download_url = "https://github.com/CarbonCommunity/Carbon/releases/download/production_build/Carbon.Linux.Release.tar.gz"
        response = requests.get(download_url)
        response.raise_for_status()

        with open(os.path.join(PATH_TMP, "carbon.tar.gz"), "wb") as f:
            f.write(response.content)

        # Extracting and installing Carbon
        print("Extracting and installing Carbon.")
        with tarfile.open(os.path.join(PATH_TMP, "carbon.tar.gz"), "r:gz") as tar_ref:
            tar_ref.extractall(PATH_RUST_SERVER)
    except Exception as e:
        print("Error occurred during Carbon update:", e)

    

def start_rust_server():
    # User settings
    SERVER_NAME = "CARBON | env:linux branch:preview"
    SERVER_MAP_SIZE = 1000
    SERVER_MAP_SEED = 12345
    SERVER_PORT = 28015
    SERVER_QUERY = 28016
    SERVER_RCON_PORT = 28017
    SERVER_RCON_PASS = "mypasslol"

    # Exporting environment variables
    os.environ["LD_LIBRARY_PATH"] = os.path.join(PATH_RUST_SERVER, "RustDedicated_Data", "Plugins", "x86_64")
    os.environ["TERM"] = "xterm"

    # Changing directory
    os.chdir(PATH_RUST_SERVER)

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
        "+server.maxplayers", "8",
        "+app.port", "1-"
    ]

    subprocess.run(command)

if __name__ == "__main__":
    base_install()
    start_rust_server()