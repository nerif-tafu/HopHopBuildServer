# HopHop-Build-Server

Run this to bootstrap the rust server.
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