#!/bin/bash

# Check if HopHopBuildServer already exists
if [ -d "$HOME/HopHopBuildServer" ]; then
    echo "Warning: HopHopBuildServer folder already exists in $HOME"
    echo "Please remove or rename the existing folder before running this script"
    exit 1
fi

# Function to get user input with timeout
get_input() {
    local prompt="$1"
    local input
    echo -n "$prompt"
    read -t 30 input
    echo "$input"
}

# Function to configure environment variables
configure_env() {
    echo "Would you like to review and modify the default server settings? (y/N)"
    response=$(get_input "> ")
    
    if [[ "$response" =~ ^[Yy] ]]; then
        # Create or clear .env.local
        echo "# Custom server settings" > .env.local
        
        # Read each non-comment line from .env
        while IFS='=' read -r key value; do
            # Skip empty lines and comments
            [[ -z "$key" || "$key" =~ ^[[:space:]]*# ]] && continue
            
            # Remove leading/trailing whitespace
            key=$(echo "$key" | xargs)
            value=$(echo "$value" | xargs)
            
            echo
            echo "Current $key is: $value"
            echo "Would you like to change this value? (y/N)"
            change_response=$(get_input "> ")
            
            if [[ "$change_response" =~ ^[Yy] ]]; then
                echo "Enter new value for $key:"
                new_value=$(get_input "> ")
                echo "$key=$new_value" >> .env.local
                echo "Updated $key to: $new_value"
            fi
        done < .env
        
        echo
        echo "Configuration complete! Custom settings saved to .env.local"
    else
        echo "Skipping configuration. Default values will be used."
    fi
}

# Continue with installation if folder doesn't exist
sudo apt install lib32stdc++6 python3-pip python3.12-venv git -y
cd $HOME
git clone https://github.com/nerif-tafu/HopHopBuildServer.git
cd HopHopBuildServer

# Run configuration before starting the server
configure_env

# Create virtual environment and install requirements
python3 -m venv venv
source venv/bin/activate
pip3 install -r requirements.txt

# Start the server
python3 bootstrap.py