# HopHop-Build-Server
This is a bootstrap script for a Rust server. It will install the server and configure it with the given settings.

## Usage

For a quick install run the following command:
```
curl -sSL https://raw.githubusercontent.com/nerif-tafu/HopHopBuildServer/main/install.sh | bash
```

Or to semi manually install the server run the following commands:
```
sudo apt install lib32stdc++6 python3-pip python3-virtualenv git screen debconf-utils -y
git clone https://github.com/nerif-tafu/HopHopBuildServer.git
cd HopHopBuildServer
python3 -m venv venv
source venv/bin/activate
pip3 install -r requirements.txt
python3 bootstrap.py
```

## Requirements
- 30GB of free disk space
- 12GB of RAM
- 4 CPU Cores

## Environment Variables
To configure the server, create a `.env.local` file, any variables set in this file will override the defaults in the `.env` file.
