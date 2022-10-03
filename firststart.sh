#!/bin/bash

#if [[ $(/usr/bin/id -u) -ne 0 ]]; then
#    echo "Not running as root, please run with sudo."
#    exit
#fi
sudo echo "Elevated"

STEAMCMD=./steamcmd.sh
INSTALLDIR=$(realpath "$0" | sed 's|\(.*\)/.*|\1|')
UUID="NONE"
FIRSTRUN=false
RCONPASS=""
HOSTNAME=""
SERVERDESC=""
SERVERIMG=""
SERVERURL=""

generateConfig () {
    UUID=$(uuidgen)
    RCONPASS=$(date +%s | sha256sum | base64 | head -c 32)
    if [ ! -f "config.json" ]; then
        echo "New install, generating configuration file for server $UUID."
        FIRSTRUN=true
        echo "Please enter the hostname of the server."
        read HOSTNAME
        echo "Please enter the description for the server"
        read SERVERDESC
        echo "Please enter the server image URL"
        read SERVERIMG
        echo "Please enter the server website URL"
        read SERVERURL

        
        jq -n \
        --arg tempUUID "$UUID" \
        --arg tempRCONPASS "$RCONPASS" \
        --arg tempHOSTNAME "$HOSTNAME" \
        --arg tempSERVERDESC "$SERVERDESC" \
        --arg tempSERVERIMG "$SERVERIMG" \
        --arg tempSERVERURL "$SERVERURL" \
        '{
            "ID":($tempUUID), 
            "rconpass":($tempRCONPASS), 
            "hostname":($tempHOSTNAME), 
            "serverdesc":($tempSERVERDESC), 
            "serverimg":($tempSERVERIMG), 
            "serverurl":($tempSERVERURL)
        }' > config.json
        touch rustLogs.txt
    else
        UUID=$(cat config.json | jq .ID -r)
        RCONPASS=$(cat config.json | jq .rconpass -r)
        HOSTNAME=$(cat config.json | jq .hostname -r)
        SERVERDESC=$(cat config.json | jq .serverdesc -r)
        SERVERIMG=$(cat config.json | jq .serverimg -r)
        SERVERURL=$(cat config.json | jq .serverurl -r)
        FIRSTRUN=false
        echo "Running existing server: $UUID"
    fi
}

installSteamCMD () {
    echo "New install, downloading SteamCMD."
    sudo apt install lib32gcc-s1 ubuntu-desktop screen -y
    curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar zxvf -
}

updateRustSteamCMD () {
    echo "Checking for Rust update."
    mkdir -p $INSTALLDIR/rustServer
    $INSTALLDIR/steamcmd.sh +force_install_dir $INSTALLDIR/rustServer +login anonymous +app_update 258550 validate +quit
}

updateuMod () {
    sudo apt install jq wget unzip e2fsprogs -y -qqq
    LATESTUMODVER=$(curl -s https://assets.umod.org/games/rust.json | jq -r .latest_release_version)
    # TODO: Add error checking for failed version query.
    mkdir -p tmp

    if [ -f "$INSTALLDIR/tmp/$LATESTUMODVER" ]; then
        echo "uMod already updated to latest version."
    else
        echo "Downloading new uMod update."
        wget https://umod.org/games/rust/download/develop -O "$INSTALLDIR/tmp/$LATESTUMODVER" -q 
        if [[ $? -ne 0 ]]; then
            echo "Could not download uMod update. Exiting"
            echo "${date}: Failed uMod download" >> /var/log/hophopbuildserver$UUID
            exit 1; 
        fi
        echo "Extracting and installing uMod."
        unzip -f -q "$INSTALLDIR/tmp/$LATESTUMODVER" -d $INSTALLDIR/rustServer
    fi
}

setupService () {
    if [ ! -f "/etc/systemd/system/hophopbuildserver-${UUID}.service" ]; then
        echo "Creating and enabling systemd service for /etc/systemd/system/hophopbuildserver-${UUID}."
        
        sudo tee -a /etc/systemd/system/hophopbuildserver-${UUID}.service <<-EOF >/dev/null
[Unit]
After=network.target

[Service]
ExecStart=${INSTALLDIR}/firststart.sh
Type=simple 
User=root
Group=root
TimeoutStopUSec=infinity

[Install]
WantedBy=multi-user.target
EOF
        sudo systemctl daemon-reload 1>/dev/null
        sudo systemctl enable "hophopbuildserver-${UUID}.service" 1>/dev/null
    else
        echo "Systemd service already enabled."
    fi
}

setupFirewall() {
    echo "Checking firewall settings."
    echo "y" | sudo ufw enable 1>/dev/null
    sudo ufw allow 28015:28016/tcp 1>/dev/null
    sudo ufw allow 28015:28016/udp 1>/dev/null
    sudo ufw allow 28082/tcp 1>/dev/null
    sudo ufw reload
}

startRustServer() {

    if [ "${FIRSTRUN}" = true ] ; then
        echo "Please run 'sudo systemctl start hophopbuildserver-${UUID}.service' to start the server."
        echo "You will not need to run this command in the future, the server will start automatically on boot/crash"
    else
        echo "Starting server!"
        # screen -S hophopbuildserver-${UUID} -dm bash -c "export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:${INSTALLDIR}/rustServer/RustDedicated_Data/Plugins/x86_64; export TERM=xterm; cd ${INSTALLDIR}/rustServer; exec ${INSTALLDIR}/rustServer/RustDedicated -batchmode -nographics -server.port 28015 -rcon.port 28016 -rcon.password "${RCONPASS}" -server.maxplayers 75 -server.hostname "${HOSTNAME}" -server.identity "ident1" -server.level "Procedural Map" -server.seed 123453353324673 -server.worldsize 4200 -server.saveinterval 300 -server.globalchat true -server.description "${SERVERDESC}" -server.headerimage "${SERVERIMG}" -server.url "${SERVERURL}"; sleep 10"
        export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:${INSTALLDIR}/rustServer/RustDedicated_Data/Plugins/x86_64 
        export TERM=xterm
        cd ${INSTALLDIR}/rustServer
        exec ${INSTALLDIR}/rustServer/RustDedicated -batchmode -nographics -server.port 28015 -rcon.port 28016 -rcon.password "${RCONPASS}" -server.maxplayers 75 -server.hostname "${HOSTNAME}" -server.identity "ident1" -server.level "Procedural Map" -server.seed 123453353324673 -server.worldsize 4200 -server.saveinterval 300 -server.globalchat true -server.description "${SERVERDESC}" -server.headerimage "${SERVERIMG}" -server.url "${SERVERURL}"
    fi
}
    
# Begin orchestration

if [ ! -f "$STEAMCMD" ]; then
    installSteamCMD
fi

generateConfig
updateRustSteamCMD
updateuMod
setupService
setupFirewall
startRustServer