sudo apt install lib32stdc++6 python3-pip python3.12-venv git -y
cd $HOME
git clone https://github.com/nerif-tafu/HopHopBuildServer.git
cd HopHopBuildServer
python3 -m venv venv
source venv/bin/activate
pip3 install -r requirements.txt
python3 bootstrap.py