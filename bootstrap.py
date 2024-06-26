import os
import subprocess
import requests
import json
import tarfile
import shutil
from pysteamcmdwrapper import SteamCMD, SteamCMDException

# Variable defs.
RUST_ID = 258550

PATH_ROOT = os.path.realpath(os.path.dirname(__file__))
PATH_RUST_SERVER = os.path.join(PATH_ROOT,"rust_server")
PATH_RUST_PLUGINS = os.path.join(PATH_RUST_SERVER,"carbon","plugins")
PATH_STEAM_CMD = os.path.join(PATH_ROOT,"steam_cmd")
PATH_TMP = os.path.join(PATH_ROOT,"tmp")
PATH_SCRIPTS = os.path.join(PATH_ROOT,"scripts")

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

    # Install Carbon modding framework.
    try:
        download_url = "https://github.com/CarbonCommunity/Carbon/releases/download/production_build/Carbon.Linux.Release.tar.gz"
        response = requests.get(download_url)
        response.raise_for_status()

        with open(os.path.join(PATH_TMP, "carbon.tar.gz"), "wb") as f:
            f.write(response.content)

        # Extracting and installing Carbon.
        print("Extracting and installing Carbon.")
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
    # User settings
    SERVER_NAME = "HopHop Build server | Main"
    SERVER_MAP_SIZE = 4800
    SERVER_MAP_SEED = 12345
    SERVER_PORT = 28015
    SERVER_QUERY = 28016
    SERVER_RCON_PORT = 28017
    SERVER_RCON_PASS = "avoid-unelected-thee"

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
        "+server.maxplayers", "8",
        "+app.port", "1-"
    ]

    subprocess.run(command)

if __name__ == "__main__":
    base_install()
    start_rust_server()