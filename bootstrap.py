import os
import subprocess
import requests
import json
import zipfile
from pysteamcmdwrapper import SteamCMD, SteamCMDException

# Variable defs.
RUST_ID = 258550

PATH_ROOT = os.getcwd()
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
        # Fetching latest uMod version
        response = requests.get("https://assets.umod.org/games/rust.json")
        response.raise_for_status()
        umod_data = response.json()
        latest_umod_ver = umod_data["latest_release_version"]

        # Check if uMod is already updated to the latest version
        umod_update_path = os.path.join(PATH_TMP, latest_umod_ver)

        if os.path.isfile(umod_update_path):
            print("uMod already updated to the latest version:", latest_umod_ver)
        else:
            print("Downloading new uMod update:", latest_umod_ver)
            # Downloading uMod update
            download_url = f"https://umod.org/games/rust/download/{latest_umod_ver}"
            download_path = os.path.join(PATH_TMP, latest_umod_ver)
            response = requests.get(download_url)
            response.raise_for_status()
            with open(download_path, "wb") as f:
                f.write(response.content)
                
            # Extracting and installing uMod
            print("Extracting and installing uMod.")
            with zipfile.ZipFile(download_path, "r") as zip_ref:
                zip_ref.extractall(PATH_RUST_SERVER)

    except Exception as e:
        print("Error occurred during uMod update:", e)


if __name__ == "__main__":
    base_install()